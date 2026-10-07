namespace Aetherphone.Apps.Games.Framework.Physics;

internal sealed partial class PhysicsWorld
{
    private const int BulletCandidateCapacity = 64;
    private const int MaxBulletSubsteps = 32;
    private const float BulletTravelPerRadius = 0.5f;
    private const ulong PairHashMultiplier = 0x9E3779B97F4A7C15UL;
    private const float MaxConditionNumber = 1000f;
    private const float WarmStartReach = 4f * LinearSlop;
    private const float WarmStartAlignment = 0.98f;

    private struct ContactPair
    {
        public ulong Key;
        public int BodyA;
        public int BodyB;
        public int FirstPoint;
        public int PointCount;
        public float SubstepImpulse;
        public Vector2 EventPoint;
        public Vector2 EventNormal;
        public float MassXX;
        public float MassXY;
        public float MassYY;
        public float InverseXX;
        public float InverseXY;
        public float InverseYY;
        public bool Sensor;
        public bool Touching;
        public bool WasTouching;
        public bool Carried;
        public bool Matched;
        public bool SubstepTouching;
        public bool Block;
    }

    private struct ContactPoint
    {
        public int BodyA;
        public int BodyB;
        public Vector2 Point;
        public Vector2 Normal;
        public Vector2 AnchorA;
        public Vector2 AnchorB;
        public float Separation;
        public float NormalMass;
        public float TangentMass;
        public float NormalImpulse;
        public float TangentImpulse;
        public float PseudoImpulse;
        public float MaxNormalImpulse;
        public float RelativeVelocity;
        public float Friction;
        public float Restitution;
        public uint Id;
    }

    private readonly int[] pairTable;
    private readonly int pairTableShift;
    private readonly int[] activePairs;
    private readonly int[] bulletCandidates;
    private ContactPair[] pairs;
    private ContactPair[] previousPairs;
    private ContactPoint[] points;
    private ContactPoint[] previousPoints;
    private int pairCount;
    private int previousPairCount;
    private int pointCount;
    private int activePairCount;

    private static int TableBits(int capacity)
    {
        var bits = 1;
        while ((1 << bits) < capacity * 2)
        {
            bits++;
        }

        return bits;
    }

    private static ulong PairKey(int first, int second)
    {
        var low = Math.Min(first, second);
        var high = Math.Max(first, second);
        return ((ulong)(uint)low << 32) | (uint)high;
    }

    private int HashSlot(ulong key) => (int)((key * PairHashMultiplier) >> pairTableShift);

    private void ClearContacts()
    {
        pairCount = 0;
        previousPairCount = 0;
        pointCount = 0;
        activePairCount = 0;
    }

    private void BeginContacts()
    {
        (pairs, previousPairs) = (previousPairs, pairs);
        (points, previousPoints) = (previousPoints, points);
        previousPairCount = pairCount;
        pairCount = 0;
        pointCount = 0;
        Array.Fill(pairTable, -1);
        var tableMask = pairTable.Length - 1;
        for (var pairIndex = 0; pairIndex < previousPairCount; pairIndex++)
        {
            ref var pair = ref previousPairs[pairIndex];
            pair.Matched = false;
            if (pair.BodyA < 0)
            {
                continue;
            }

            var slot = HashSlot(pair.Key);
            while (pairTable[slot] >= 0)
            {
                slot = (slot + 1) & tableMask;
            }

            pairTable[slot] = pairIndex;
        }
    }

    private int FindPreviousPair(ulong key)
    {
        var tableMask = pairTable.Length - 1;
        var slot = HashSlot(key);
        while (true)
        {
            var pairIndex = pairTable[slot];
            if (pairIndex < 0 || previousPairs[pairIndex].Key == key)
            {
                return pairIndex;
            }

            slot = (slot + 1) & tableMask;
        }
    }

    private void FindPairs()
    {
        SortSweepOrder();
        for (var orderIndex = 0; orderIndex < sweepCount; orderIndex++)
        {
            var first = sweepOrder[orderIndex];
            var reachX = boundsMax[first].X;
            for (var otherIndex = orderIndex + 1; otherIndex < sweepCount; otherIndex++)
            {
                var second = sweepOrder[otherIndex];
                if (boundsMin[second].X > reachX)
                {
                    break;
                }

                if (boundsMin[second].Y > boundsMax[first].Y || boundsMax[second].Y < boundsMin[first].Y)
                {
                    continue;
                }

                ConsiderPair(first, second);
            }
        }
    }

    private void SortSweepOrder()
    {
        for (var index = 1; index < sweepCount; index++)
        {
            var body = sweepOrder[index];
            var key = boundsMin[body].X;
            var slot = index - 1;
            while (slot >= 0 && boundsMin[sweepOrder[slot]].X > key)
            {
                sweepOrder[slot + 1] = sweepOrder[slot];
                slot--;
            }

            sweepOrder[slot + 1] = body;
        }
    }

    private bool CanCollide(int first, int second)
    {
        if ((categories[first] & masks[second]) == 0 || (categories[second] & masks[first]) == 0)
        {
            return false;
        }

        return jointCounts[first] == 0 || jointCounts[second] == 0 || !JointConnects(first, second);
    }

    private void ConsiderPair(int first, int second)
    {
        if (types[first] != BodyType.Dynamic && types[second] != BodyType.Dynamic)
        {
            return;
        }

        var sensorFirst = (flags[first] & BodyFlags.Sensor) != 0;
        var sensorSecond = (flags[second] & BodyFlags.Sensor) != 0;
        if ((sensorFirst && sensorSecond) || !CanCollide(first, second))
        {
            return;
        }

        var bodyA = OrderFirst(first, second);
        var bodyB = bodyA == first ? second : first;
        var key = PairKey(first, second);
        var previous = FindPreviousPair(key);
        if (!IsActive(bodyA) && !IsActive(bodyB))
        {
            if (previous >= 0)
            {
                CarryPair(previous);
            }

            return;
        }

        var sensor = sensorFirst || sensorSecond;
        var manifold = default(Manifold);
        Collide(bodyA, bodyB, sensor ? 0f : SpeculativeDistance, ref manifold);
        if (manifold.Count == 0)
        {
            return;
        }

        if (!sensor)
        {
            AddSolidPair(key, bodyA, bodyB, previous, ref manifold);
            return;
        }

        ref readonly var deepest = ref manifold.Points[manifold.DeepestSlot()];
        if (deepest.Separation >= 0f)
        {
            return;
        }

        var pairIndex = AddPair(key, bodyA, bodyB, previous, true);
        if (pairIndex < 0)
        {
            return;
        }

        pairs[pairIndex].Touching = true;
        pairs[pairIndex].EventPoint = deepest.Point;
        pairs[pairIndex].EventNormal = deepest.Normal;
    }

    private int OrderFirst(int first, int second)
    {
        if (shapes[first] == shapes[second])
        {
            return Math.Min(first, second);
        }

        return shapes[first] > shapes[second] ? first : second;
    }

    private void Collide(int bodyA, int bodyB, float margin, ref Manifold manifold)
    {
        if (shapes[bodyB] == ShapeKind.Circle)
        {
            CollideWithCircle(bodyA, positions[bodyA], rotations[bodyA], positions[bodyB], radii[bodyB], margin,
                ref manifold);
            return;
        }

        if (shapes[bodyA] == ShapeKind.Box)
        {
            ShapeCollision.BoxBox(positions[bodyA], rotations[bodyA], halfExtents[bodyA], positions[bodyB],
                rotations[bodyB], halfExtents[bodyB], margin, ref manifold);
            return;
        }

        ShapeCollision.ChainBox(ChainPoints(bodyA), chainClosed[bodyA], positions[bodyB], rotations[bodyB],
            halfExtents[bodyB], margin, ref manifold);
    }

    private void CollideWithCircle(int body, Vector2 position, Vector2 rotation, Vector2 center, float radius,
        float margin, ref Manifold manifold)
    {
        switch (shapes[body])
        {
            case ShapeKind.Circle:
                ShapeCollision.CircleCircle(position, radii[body], center, radius, margin, ref manifold);
                return;
            case ShapeKind.Box:
                ShapeCollision.BoxCircle(position, rotation, halfExtents[body], center, radius, margin, ref manifold);
                return;
            case ShapeKind.Segment:
            case ShapeKind.Polyline:
                ShapeCollision.ChainCircle(ChainPoints(body), chainClosed[body], center, radius, margin, ref manifold);
                return;
        }
    }

    private int AddPair(ulong key, int bodyA, int bodyB, int previous, bool sensor)
    {
        if (pairCount >= pairs.Length)
        {
            return -1;
        }

        var pairIndex = pairCount;
        pairCount++;
        ref var pair = ref pairs[pairIndex];
        pair = default;
        pair.Key = key;
        pair.BodyA = bodyA;
        pair.BodyB = bodyB;
        pair.FirstPoint = pointCount;
        pair.Sensor = sensor;
        if (previous >= 0)
        {
            pair.WasTouching = previousPairs[previous].Touching;
            previousPairs[previous].Matched = true;
        }

        return pairIndex;
    }

    private void AddSolidPair(ulong key, int bodyA, int bodyB, int previous, ref Manifold manifold)
    {
        var pairIndex = AddPair(key, bodyA, bodyB, previous, false);
        if (pairIndex < 0)
        {
            return;
        }

        var friction = MathF.Sqrt(frictions[bodyA] * frictions[bodyB]);
        var restitution = MathF.Max(restitutions[bodyA], restitutions[bodyB]);
        for (var index = 0; index < manifold.Count && pointCount < points.Length; index++)
        {
            ref readonly var source = ref manifold.Points[index];
            ref var point = ref points[pointCount];
            point = default;
            point.BodyA = bodyA;
            point.BodyB = bodyB;
            point.Point = source.Point;
            point.Normal = source.Normal;
            point.Separation = source.Separation;
            point.Id = source.Id;
            point.Friction = friction;
            point.Restitution = restitution;
            if (previous >= 0)
            {
                InheritImpulses(previous, ref point);
            }

            pointCount++;
            pairs[pairIndex].PointCount++;
        }
    }

    private void InheritImpulses(int previous, ref ContactPoint point)
    {
        ref readonly var old = ref previousPairs[previous];
        var match = -1;
        var nearest = WarmStartReach * WarmStartReach;
        for (var index = old.FirstPoint; index < old.FirstPoint + old.PointCount; index++)
        {
            ref readonly var oldPoint = ref previousPoints[index];
            if (oldPoint.BodyA != point.BodyA)
            {
                continue;
            }

            if (oldPoint.Id == point.Id)
            {
                match = index;
                break;
            }

            var distance = Vector2.DistanceSquared(oldPoint.Point, point.Point);
            if (distance < nearest && Vector2.Dot(oldPoint.Normal, point.Normal) > WarmStartAlignment)
            {
                nearest = distance;
                match = index;
            }
        }

        if (match < 0)
        {
            return;
        }

        point.NormalImpulse = previousPoints[match].NormalImpulse;
        point.TangentImpulse = previousPoints[match].TangentImpulse;
    }

    private void CarryPair(int previous)
    {
        ref var old = ref previousPairs[previous];
        old.Matched = true;
        if (pairCount >= pairs.Length || pointCount + old.PointCount > points.Length)
        {
            return;
        }

        ref var pair = ref pairs[pairCount];
        pairCount++;
        pair = old;
        pair.FirstPoint = pointCount;
        pair.Carried = true;
        pair.WasTouching = old.Touching;
        pair.SubstepTouching = false;
        pair.SubstepImpulse = 0f;
        Array.Copy(previousPoints, old.FirstPoint, points, pointCount, old.PointCount);
        pointCount += old.PointCount;
    }

    private void ReleaseContacts(int body)
    {
        for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            ref var pair = ref pairs[pairIndex];
            if (pair.BodyA != body && pair.BodyB != body)
            {
                continue;
            }

            Wake(pair.BodyA == body ? pair.BodyB : pair.BodyA);
            pair.BodyA = -1;
            pair.PointCount = 0;
        }
    }

    private void WakeTouching(int body)
    {
        for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            ref readonly var pair = ref pairs[pairIndex];
            if (pair.BodyA == body)
            {
                Wake(pair.BodyB);
            }
            else if (pair.BodyB == body)
            {
                Wake(pair.BodyA);
            }
        }
    }

    private Vector2 RelativeVelocity(int bodyA, int bodyB, Vector2 anchorA, Vector2 anchorB) =>
        velocities[bodyB] + PhysicsMath.Cross(angularVelocities[bodyB], anchorB) -
        velocities[bodyA] - PhysicsMath.Cross(angularVelocities[bodyA], anchorA);

    private Vector2 PseudoRelativeVelocity(int bodyA, int bodyB, Vector2 anchorA, Vector2 anchorB) =>
        pseudoVelocities[bodyB] + PhysicsMath.Cross(pseudoAngularVelocities[bodyB], anchorB) -
        pseudoVelocities[bodyA] - PhysicsMath.Cross(pseudoAngularVelocities[bodyA], anchorA);

    private void Exchange(int bodyA, int bodyB, Vector2 anchorA, Vector2 anchorB, Vector2 impulse)
    {
        if (awake[bodyA])
        {
            velocities[bodyA] -= inverseMasses[bodyA] * impulse;
            angularVelocities[bodyA] -= inverseInertias[bodyA] * PhysicsMath.Cross(anchorA, impulse);
        }

        if (awake[bodyB])
        {
            velocities[bodyB] += inverseMasses[bodyB] * impulse;
            angularVelocities[bodyB] += inverseInertias[bodyB] * PhysicsMath.Cross(anchorB, impulse);
        }
    }

    private void ExchangePseudo(int bodyA, int bodyB, Vector2 anchorA, Vector2 anchorB, Vector2 impulse)
    {
        if (awake[bodyA])
        {
            pseudoVelocities[bodyA] -= inverseMasses[bodyA] * impulse;
            pseudoAngularVelocities[bodyA] -= inverseInertias[bodyA] * PhysicsMath.Cross(anchorA, impulse);
        }

        if (awake[bodyB])
        {
            pseudoVelocities[bodyB] += inverseMasses[bodyB] * impulse;
            pseudoAngularVelocities[bodyB] += inverseInertias[bodyB] * PhysicsMath.Cross(anchorB, impulse);
        }
    }

    private static float SpeculativeBias(in ContactPoint point) =>
        point.Separation > 0f ? point.Separation * InverseStep : 0f;

    private static float PseudoTarget(in ContactPoint point) =>
        -Baumgarte * InverseStep * MathF.Max(point.Separation + LinearSlop, -MaxCorrection);

    private static bool SolveBlock(in ContactPair pair, float normalMassFirst, float normalMassSecond,
        float biasFirst, float biasSecond, out float impulseFirst, out float impulseSecond)
    {
        impulseFirst = -(pair.InverseXX * biasFirst + pair.InverseXY * biasSecond);
        impulseSecond = -(pair.InverseXY * biasFirst + pair.InverseYY * biasSecond);
        if (impulseFirst >= 0f && impulseSecond >= 0f)
        {
            return true;
        }

        impulseFirst = -normalMassFirst * biasFirst;
        impulseSecond = 0f;
        if (impulseFirst >= 0f && pair.MassXY * impulseFirst + biasSecond >= 0f)
        {
            return true;
        }

        impulseFirst = 0f;
        impulseSecond = -normalMassSecond * biasSecond;
        if (impulseSecond >= 0f && pair.MassXY * impulseSecond + biasFirst >= 0f)
        {
            return true;
        }

        impulseFirst = 0f;
        impulseSecond = 0f;
        return biasFirst >= 0f && biasSecond >= 0f;
    }

    private void PrepareContacts()
    {
        activePairCount = 0;
        for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            ref var pair = ref pairs[pairIndex];
            pair.Block = false;
            if (pair.BodyA < 0 || pair.Sensor || pair.PointCount == 0)
            {
                continue;
            }

            if (!IsSolvable(pair.BodyA) && !IsSolvable(pair.BodyB))
            {
                continue;
            }

            for (var pointIndex = pair.FirstPoint; pointIndex < pair.FirstPoint + pair.PointCount; pointIndex++)
            {
                PreparePoint(ref points[pointIndex]);
            }

            PrepareBlock(ref pair);
            activePairs[activePairCount] = pairIndex;
            activePairCount++;
        }
    }

    private void PrepareBlock(ref ContactPair pair)
    {
        if (pair.PointCount != 2)
        {
            return;
        }

        ref readonly var first = ref points[pair.FirstPoint];
        ref readonly var second = ref points[pair.FirstPoint + 1];
        if (first.Normal != second.Normal)
        {
            return;
        }

        var inverseMassSum = SolverInverseMass(pair.BodyA) + SolverInverseMass(pair.BodyB);
        var inverseInertiaA = SolverInverseInertia(pair.BodyA);
        var inverseInertiaB = SolverInverseInertia(pair.BodyB);
        var firstCrossA = PhysicsMath.Cross(first.AnchorA, first.Normal);
        var firstCrossB = PhysicsMath.Cross(first.AnchorB, first.Normal);
        var secondCrossA = PhysicsMath.Cross(second.AnchorA, first.Normal);
        var secondCrossB = PhysicsMath.Cross(second.AnchorB, first.Normal);
        var massXX = inverseMassSum + inverseInertiaA * firstCrossA * firstCrossA +
            inverseInertiaB * firstCrossB * firstCrossB;
        var massYY = inverseMassSum + inverseInertiaA * secondCrossA * secondCrossA +
            inverseInertiaB * secondCrossB * secondCrossB;
        var massXY = inverseMassSum + inverseInertiaA * firstCrossA * secondCrossA +
            inverseInertiaB * firstCrossB * secondCrossB;
        var determinant = massXX * massYY - massXY * massXY;
        if (massXX * massXX >= MaxConditionNumber * determinant)
        {
            return;
        }

        var inverse = 1f / determinant;
        pair.Block = true;
        pair.MassXX = massXX;
        pair.MassXY = massXY;
        pair.MassYY = massYY;
        pair.InverseXX = massYY * inverse;
        pair.InverseXY = -massXY * inverse;
        pair.InverseYY = massXX * inverse;
    }

    private void PreparePoint(ref ContactPoint point)
    {
        var bodyA = point.BodyA;
        var bodyB = point.BodyB;
        var inverseMassSum = SolverInverseMass(bodyA) + SolverInverseMass(bodyB);
        var inverseInertiaA = SolverInverseInertia(bodyA);
        var inverseInertiaB = SolverInverseInertia(bodyB);
        point.AnchorA = point.Point - positions[bodyA];
        point.AnchorB = point.Point - positions[bodyB];
        var normal = point.Normal;
        var tangent = PhysicsMath.RightPerpendicular(normal);
        var normalCrossA = PhysicsMath.Cross(point.AnchorA, normal);
        var normalCrossB = PhysicsMath.Cross(point.AnchorB, normal);
        var normalMass = inverseMassSum + inverseInertiaA * normalCrossA * normalCrossA +
            inverseInertiaB * normalCrossB * normalCrossB;
        point.NormalMass = normalMass > 0f ? 1f / normalMass : 0f;
        var tangentCrossA = PhysicsMath.Cross(point.AnchorA, tangent);
        var tangentCrossB = PhysicsMath.Cross(point.AnchorB, tangent);
        var tangentMass = inverseMassSum + inverseInertiaA * tangentCrossA * tangentCrossA +
            inverseInertiaB * tangentCrossB * tangentCrossB;
        point.TangentMass = tangentMass > 0f ? 1f / tangentMass : 0f;
        point.RelativeVelocity = Vector2.Dot(RelativeVelocity(bodyA, bodyB, point.AnchorA, point.AnchorB), normal);
        point.MaxNormalImpulse = 0f;
        point.PseudoImpulse = 0f;
    }

    private void WarmStartContacts()
    {
        for (var activeIndex = 0; activeIndex < activePairCount; activeIndex++)
        {
            ref readonly var pair = ref pairs[activePairs[activeIndex]];
            for (var pointIndex = pair.FirstPoint; pointIndex < pair.FirstPoint + pair.PointCount; pointIndex++)
            {
                ref readonly var point = ref points[pointIndex];
                var impulse = point.NormalImpulse * point.Normal +
                    point.TangentImpulse * PhysicsMath.RightPerpendicular(point.Normal);
                Exchange(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB, impulse);
            }
        }
    }

    private void SolveContactVelocities()
    {
        for (var activeIndex = 0; activeIndex < activePairCount; activeIndex++)
        {
            ref readonly var pair = ref pairs[activePairs[activeIndex]];
            var end = pair.FirstPoint + pair.PointCount;
            for (var pointIndex = pair.FirstPoint; pointIndex < end; pointIndex++)
            {
                SolveFriction(ref points[pointIndex]);
            }

            if (pair.Block)
            {
                SolveNormalBlock(pair, false);
                continue;
            }

            for (var pointIndex = pair.FirstPoint; pointIndex < end; pointIndex++)
            {
                SolveNormal(ref points[pointIndex]);
            }
        }
    }

    private void SolveFriction(ref ContactPoint point)
    {
        var tangent = PhysicsMath.RightPerpendicular(point.Normal);
        var tangentSpeed = Vector2.Dot(RelativeVelocity(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB),
            tangent);
        var limit = point.Friction * point.NormalImpulse;
        var accumulated = MathF.Max(-limit, MathF.Min(point.TangentImpulse - point.TangentMass * tangentSpeed, limit));
        var impulse = accumulated - point.TangentImpulse;
        point.TangentImpulse = accumulated;
        Exchange(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB, tangent * impulse);
    }

    private void SolveNormal(ref ContactPoint point)
    {
        var normalSpeed = Vector2.Dot(RelativeVelocity(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB),
            point.Normal);
        var accumulated = MathF.Max(point.NormalImpulse - point.NormalMass * (normalSpeed + SpeculativeBias(point)), 0f);
        var impulse = accumulated - point.NormalImpulse;
        point.NormalImpulse = accumulated;
        point.MaxNormalImpulse = MathF.Max(point.MaxNormalImpulse, accumulated);
        Exchange(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB, point.Normal * impulse);
    }

    private void SolveNormalBlock(in ContactPair pair, bool pseudo)
    {
        ref var first = ref points[pair.FirstPoint];
        ref var second = ref points[pair.FirstPoint + 1];
        var bodyA = pair.BodyA;
        var bodyB = pair.BodyB;
        var normal = first.Normal;
        float speedFirst;
        float speedSecond;
        float oldFirst;
        float oldSecond;
        if (pseudo)
        {
            speedFirst = Vector2.Dot(PseudoRelativeVelocity(bodyA, bodyB, first.AnchorA, first.AnchorB), normal) -
                PseudoTarget(first);
            speedSecond = Vector2.Dot(PseudoRelativeVelocity(bodyA, bodyB, second.AnchorA, second.AnchorB), normal) -
                PseudoTarget(second);
            oldFirst = first.PseudoImpulse;
            oldSecond = second.PseudoImpulse;
        }
        else
        {
            speedFirst = Vector2.Dot(RelativeVelocity(bodyA, bodyB, first.AnchorA, first.AnchorB), normal) +
                SpeculativeBias(first);
            speedSecond = Vector2.Dot(RelativeVelocity(bodyA, bodyB, second.AnchorA, second.AnchorB), normal) +
                SpeculativeBias(second);
            oldFirst = first.NormalImpulse;
            oldSecond = second.NormalImpulse;
        }

        var biasFirst = speedFirst - (pair.MassXX * oldFirst + pair.MassXY * oldSecond);
        var biasSecond = speedSecond - (pair.MassXY * oldFirst + pair.MassYY * oldSecond);
        if (!SolveBlock(pair, first.NormalMass, second.NormalMass, biasFirst, biasSecond, out var newFirst,
                out var newSecond))
        {
            return;
        }

        var deltaFirst = normal * (newFirst - oldFirst);
        var deltaSecond = normal * (newSecond - oldSecond);
        if (pseudo)
        {
            first.PseudoImpulse = newFirst;
            second.PseudoImpulse = newSecond;
            ExchangePseudo(bodyA, bodyB, first.AnchorA, first.AnchorB, deltaFirst);
            ExchangePseudo(bodyA, bodyB, second.AnchorA, second.AnchorB, deltaSecond);
            return;
        }

        first.NormalImpulse = newFirst;
        second.NormalImpulse = newSecond;
        first.MaxNormalImpulse = MathF.Max(first.MaxNormalImpulse, newFirst);
        second.MaxNormalImpulse = MathF.Max(second.MaxNormalImpulse, newSecond);
        Exchange(bodyA, bodyB, first.AnchorA, first.AnchorB, deltaFirst);
        Exchange(bodyA, bodyB, second.AnchorA, second.AnchorB, deltaSecond);
    }

    private void SolveContactPositions()
    {
        for (var activeIndex = 0; activeIndex < activePairCount; activeIndex++)
        {
            ref readonly var pair = ref pairs[activePairs[activeIndex]];
            if (pair.Block)
            {
                SolveNormalBlock(pair, true);
                continue;
            }

            for (var pointIndex = pair.FirstPoint; pointIndex < pair.FirstPoint + pair.PointCount; pointIndex++)
            {
                SolvePseudoNormal(ref points[pointIndex]);
            }
        }
    }

    private void SolvePseudoNormal(ref ContactPoint point)
    {
        var target = PseudoTarget(point);
        if (target <= 0f)
        {
            return;
        }

        var normalSpeed = Vector2.Dot(PseudoRelativeVelocity(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB),
            point.Normal);
        var accumulated = MathF.Max(point.PseudoImpulse - point.NormalMass * (normalSpeed - target), 0f);
        var impulse = accumulated - point.PseudoImpulse;
        point.PseudoImpulse = accumulated;
        ExchangePseudo(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB, point.Normal * impulse);
    }

    private void ApplyRestitution()
    {
        for (var activeIndex = 0; activeIndex < activePairCount; activeIndex++)
        {
            ref readonly var pair = ref pairs[activePairs[activeIndex]];
            for (var pointIndex = pair.FirstPoint; pointIndex < pair.FirstPoint + pair.PointCount; pointIndex++)
            {
                ref var point = ref points[pointIndex];
                if (point.Restitution == 0f || point.RelativeVelocity > -RestitutionThreshold ||
                    point.MaxNormalImpulse == 0f)
                {
                    continue;
                }

                var normalSpeed = Vector2.Dot(
                    RelativeVelocity(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB), point.Normal);
                var accumulated = MathF.Max(
                    point.NormalImpulse - point.NormalMass * (normalSpeed + point.Restitution * point.RelativeVelocity),
                    0f);
                var impulse = accumulated - point.NormalImpulse;
                point.NormalImpulse = accumulated;
                point.MaxNormalImpulse = MathF.Max(point.MaxNormalImpulse, accumulated);
                Exchange(point.BodyA, point.BodyB, point.AnchorA, point.AnchorB, point.Normal * impulse);
            }
        }
    }

    private void SweepBullets()
    {
        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || !awake[body] || shapes[body] != ShapeKind.Circle ||
                (flags[body] & (BodyFlags.Bullet | BodyFlags.Sensor)) != BodyFlags.Bullet)
            {
                continue;
            }

            SweepBullet(body);
        }
    }

    private void SweepBullet(int bullet)
    {
        var start = startPositions[bullet];
        var end = positions[bullet];
        var radius = radii[bullet];
        var reach = new Vector2(radius + SpeculativeDistance);
        var sweptMin = Vector2.Min(start, end) - reach;
        var sweptMax = Vector2.Max(start, end) + reach;
        var candidateCount = 0;
        var candidateTravel = 0f;
        for (var other = 1; other < nextBody && candidateCount < bulletCandidates.Length; other++)
        {
            if (!IsBulletCandidate(bullet, other) || boundsMin[other].X > sweptMax.X ||
                boundsMax[other].X < sweptMin.X || boundsMin[other].Y > sweptMax.Y || boundsMax[other].Y < sweptMin.Y)
            {
                continue;
            }

            bulletCandidates[candidateCount] = other;
            candidateCount++;
            var travel = Vector2.Distance(positions[other], startPositions[other]) +
                MathF.Abs(angles[other] - startAngles[other]) * extents[other];
            candidateTravel = MathF.Max(candidateTravel, travel);
        }

        if (candidateCount == 0)
        {
            return;
        }

        var totalTravel = Vector2.Distance(start, end) + candidateTravel;
        var substeps = Math.Min(MaxBulletSubsteps, (int)MathF.Ceiling(totalTravel / (radius * BulletTravelPerRadius)));
        if (substeps <= 1)
        {
            return;
        }

        var position = start;
        var displacement = (end - start) / substeps;
        var velocity = velocities[bullet];
        var substepSeconds = StepSeconds / substeps;
        for (var substep = 1; substep <= substeps; substep++)
        {
            position += displacement;
            var fraction = (float)substep / substeps;
            for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                if (ResolveBulletContact(bullet, bulletCandidates[candidateIndex], fraction, ref position,
                        ref velocity))
                {
                    displacement = velocity * substepSeconds;
                }
            }
        }

        positions[bullet] = position;
        velocities[bullet] = velocity;
    }

    private bool IsBulletCandidate(int bullet, int other)
    {
        if (other == bullet || !alive[other] || shapes[other] == ShapeKind.None)
        {
            return false;
        }

        if (types[other] == BodyType.Dynamic && (flags[other] & BodyFlags.Bullet) != 0)
        {
            return false;
        }

        return CanCollide(bullet, other);
    }

    private bool ResolveBulletContact(int bullet, int candidate, float fraction, ref Vector2 position,
        ref Vector2 velocity)
    {
        var candidatePosition = Vector2.Lerp(startPositions[candidate], positions[candidate], fraction);
        var candidateRotation = types[candidate] == BodyType.Static
            ? rotations[candidate]
            : PhysicsMath.Rotation(startAngles[candidate] + (angles[candidate] - startAngles[candidate]) * fraction);
        var manifold = default(Manifold);
        CollideWithCircle(candidate, candidatePosition, candidateRotation, position, radii[bullet], 0f, ref manifold);
        if (manifold.Count == 0)
        {
            return false;
        }

        ref readonly var deepest = ref manifold.Points[manifold.DeepestSlot()];
        if ((flags[candidate] & BodyFlags.Sensor) != 0)
        {
            if (deepest.Separation < 0f)
            {
                MarkSubstepContact(candidate, bullet, deepest.Point, deepest.Normal, 0f, true);
            }

            return false;
        }

        if (deepest.Separation >= -LinearSlop)
        {
            return false;
        }

        var normal = deepest.Normal;
        position += normal * -deepest.Separation;
        var anchorBullet = -normal * radii[bullet];
        var anchorCandidate = deepest.Point - candidatePosition;
        var relative = BulletRelativeVelocity(bullet, candidate, velocity, anchorBullet, anchorCandidate);
        var normalSpeed = Vector2.Dot(relative, normal);
        if (normalSpeed >= 0f)
        {
            return false;
        }

        if (types[candidate] == BodyType.Dynamic)
        {
            Wake(candidate);
        }

        var inverseMassSum = inverseMasses[bullet] + inverseMasses[candidate];
        var inverseInertiaBullet = inverseInertias[bullet];
        var inverseInertiaCandidate = inverseInertias[candidate];
        var normalCross = PhysicsMath.Cross(anchorCandidate, normal);
        var normalMass = inverseMassSum + inverseInertiaCandidate * normalCross * normalCross;
        var restitution = -normalSpeed > RestitutionThreshold
            ? MathF.Max(restitutions[bullet], restitutions[candidate])
            : 0f;
        var normalImpulse = -(1f + restitution) * normalSpeed / normalMass;
        ApplyBulletImpulse(bullet, candidate, anchorBullet, anchorCandidate, normal * normalImpulse, ref velocity);

        var tangent = PhysicsMath.RightPerpendicular(normal);
        relative = BulletRelativeVelocity(bullet, candidate, velocity, anchorBullet, anchorCandidate);
        var tangentCrossBullet = PhysicsMath.Cross(anchorBullet, tangent);
        var tangentCrossCandidate = PhysicsMath.Cross(anchorCandidate, tangent);
        var tangentMass = inverseMassSum + inverseInertiaBullet * tangentCrossBullet * tangentCrossBullet +
            inverseInertiaCandidate * tangentCrossCandidate * tangentCrossCandidate;
        var limit = MathF.Sqrt(frictions[bullet] * frictions[candidate]) * normalImpulse;
        var tangentImpulse = MathF.Max(-limit, MathF.Min(-Vector2.Dot(relative, tangent) / tangentMass, limit));
        ApplyBulletImpulse(bullet, candidate, anchorBullet, anchorCandidate, tangent * tangentImpulse, ref velocity);
        MarkSubstepContact(candidate, bullet, deepest.Point, normal, normalImpulse, false);
        return true;
    }

    private Vector2 BulletRelativeVelocity(int bullet, int candidate, Vector2 velocity, Vector2 anchorBullet,
        Vector2 anchorCandidate) =>
        velocity + PhysicsMath.Cross(angularVelocities[bullet], anchorBullet) -
        velocities[candidate] - PhysicsMath.Cross(angularVelocities[candidate], anchorCandidate);

    private void ApplyBulletImpulse(int bullet, int candidate, Vector2 anchorBullet, Vector2 anchorCandidate,
        Vector2 impulse, ref Vector2 velocity)
    {
        velocity += inverseMasses[bullet] * impulse;
        angularVelocities[bullet] += inverseInertias[bullet] * PhysicsMath.Cross(anchorBullet, impulse);
        if (types[candidate] != BodyType.Dynamic)
        {
            return;
        }

        velocities[candidate] -= inverseMasses[candidate] * impulse;
        angularVelocities[candidate] -= inverseInertias[candidate] * PhysicsMath.Cross(anchorCandidate, impulse);
    }

    private void MarkSubstepContact(int candidate, int bullet, Vector2 point, Vector2 normal, float impulse,
        bool sensor)
    {
        var key = PairKey(candidate, bullet);
        var pairIndex = -1;
        for (var index = 0; index < pairCount; index++)
        {
            if (pairs[index].Key == key && pairs[index].BodyA >= 0)
            {
                pairIndex = index;
                break;
            }
        }

        if (pairIndex < 0)
        {
            pairIndex = AddPair(key, candidate, bullet, FindPreviousPair(key), sensor);
            if (pairIndex < 0)
            {
                return;
            }
        }

        ref var pair = ref pairs[pairIndex];
        if (!pair.SubstepTouching || impulse >= pair.SubstepImpulse)
        {
            pair.EventPoint = point;
            pair.EventNormal = pair.BodyA == candidate ? normal : -normal;
        }

        pair.SubstepTouching = true;
        pair.SubstepImpulse += impulse;
    }

    private void EmitEvents()
    {
        for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            ref var pair = ref pairs[pairIndex];
            if (pair.BodyA < 0 || pair.Carried)
            {
                continue;
            }

            if (pair.Sensor)
            {
                pair.Touching = true;
                if (!pair.WasTouching)
                {
                    PushEvent(new ContactEvent(pair.BodyA, pair.BodyB, pair.EventPoint, pair.EventNormal, 0f,
                        ContactEventKind.SensorEnter));
                }

                continue;
            }

            EmitSolidEvent(ref pair);
        }

        for (var pairIndex = 0; pairIndex < previousPairCount; pairIndex++)
        {
            ref readonly var old = ref previousPairs[pairIndex];
            if (old.Matched || !old.Sensor || !old.Touching || old.BodyA < 0)
            {
                continue;
            }

            PushEvent(new ContactEvent(old.BodyA, old.BodyB, old.EventPoint, old.EventNormal, 0f,
                ContactEventKind.SensorExit));
        }
    }

    private void EmitSolidEvent(ref ContactPair pair)
    {
        var touching = pair.SubstepTouching;
        var impulse = pair.SubstepImpulse;
        var strongest = pair.SubstepTouching ? pair.SubstepImpulse : -1f;
        var eventPoint = pair.EventPoint;
        var eventNormal = pair.EventNormal;
        for (var pointIndex = pair.FirstPoint; pointIndex < pair.FirstPoint + pair.PointCount; pointIndex++)
        {
            ref readonly var point = ref points[pointIndex];
            impulse += point.NormalImpulse;
            if (point.Separation < LinearSlop || point.NormalImpulse > 0f)
            {
                touching = true;
            }

            if (point.NormalImpulse > strongest)
            {
                strongest = point.NormalImpulse;
                eventPoint = point.Point;
                eventNormal = point.Normal;
            }
        }

        pair.Touching = touching;
        if (!touching || (pair.WasTouching && impulse < ImpactThreshold))
        {
            return;
        }

        PushEvent(new ContactEvent(pair.BodyA, pair.BodyB, eventPoint, eventNormal, impulse, ContactEventKind.Hit));
    }

    private void PushEvent(in ContactEvent contactEvent)
    {
        var slot = (eventStart + eventCount) % events.Length;
        events[slot] = contactEvent;
        if (eventCount < events.Length)
        {
            eventCount++;
            return;
        }

        eventStart = (eventStart + 1) % events.Length;
    }
}
