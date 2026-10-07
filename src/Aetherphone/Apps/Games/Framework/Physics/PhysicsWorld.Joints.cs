namespace Aetherphone.Apps.Games.Framework.Physics;

internal sealed partial class PhysicsWorld
{
    public const int MaxRopeSegments = 64;
    private const int RopeCapacity = 32;
    private const float RopeLinkMassFraction = 0.02f;
    private const float RopeLinkMass = 0.02f;
    private const float RopeLinkDamping = 0.5f;
    private const float RopeLinkRadiusFraction = 0.25f;
    private const float MaxAngularCorrection = 8f * MathF.PI / 180f;

    private enum JointKind : byte
    {
        None,
        Distance,
        Hinge,
    }

    private struct Joint
    {
        public JointKind Kind;
        public bool Active;
        public bool LimitEnabled;
        public bool MotorEnabled;
        public int BodyA;
        public int BodyB;
        public Vector2 LocalAnchorA;
        public Vector2 LocalAnchorB;
        public float Length;
        public float MinLength;
        public float MaxLength;
        public float Stiffness;
        public float DampingRatio;
        public float ReferenceAngle;
        public float LowerAngle;
        public float UpperAngle;
        public float MotorSpeed;
        public float MaxMotorTorque;
        public Vector2 AnchorA;
        public Vector2 AnchorB;
        public Vector2 Axis;
        public Vector2 PointImpulse;
        public Vector2 PointError;
        public float CurrentLength;
        public float CurrentAngle;
        public float AxialMass;
        public float SoftMass;
        public float Gamma;
        public float Bias;
        public float Impulse;
        public float LowerImpulse;
        public float UpperImpulse;
        public float MotorImpulse;
        public float PositionError;
        public float PseudoImpulse;
    }

    private struct RopeRecord
    {
        public bool Alive;
        public int AnchorBody;
        public int Body;
        public int Segments;
        public int LengthJoint;
        public Vector2 LocalAnchor;
    }

    private readonly Joint[] joints;
    private readonly int[] freeJoints;
    private readonly RopeRecord[] ropes;
    private readonly int[] ropeLinks;
    private readonly int[] ropeJoints;
    private int nextJoint;
    private int freeJointCount;

    public int CreateDistanceJoint(int bodyA, int bodyB, Vector2 anchorA, Vector2 anchorB, float stiffnessHertz = 0f,
        float dampingRatio = 0f)
    {
        var length = Vector2.Distance(anchorA, anchorB);
        if (stiffnessHertz > 0f)
        {
            return AddDistanceJoint(bodyA, bodyB, anchorA, anchorB, length, 0f, float.MaxValue, stiffnessHertz,
                dampingRatio);
        }

        return AddDistanceJoint(bodyA, bodyB, anchorA, anchorB, length, length, length, 0f, 0f);
    }

    public int CreateHinge(int bodyA, int bodyB, Vector2 pivot)
    {
        var joint = AllocateJoint(JointKind.Hinge, bodyA, bodyB);
        ref var record = ref joints[joint];
        record.LocalAnchorA = LocalPoint(bodyA, pivot);
        record.LocalAnchorB = LocalPoint(bodyB, pivot);
        record.ReferenceAngle = angles[bodyB] - angles[bodyA];
        return joint;
    }

    public void SetHingeLimits(int joint, float lowerAngle, float upperAngle)
    {
        ref var record = ref joints[joint];
        record.LowerAngle = MathF.Min(lowerAngle, upperAngle);
        record.UpperAngle = MathF.Max(lowerAngle, upperAngle);
        record.LimitEnabled = true;
        Wake(record.BodyA);
        Wake(record.BodyB);
    }

    public void SetHingeMotor(int joint, float speed, float maxTorque)
    {
        ref var record = ref joints[joint];
        record.MotorSpeed = speed;
        record.MaxMotorTorque = MathF.Max(0f, maxTorque);
        record.MotorEnabled = maxTorque > 0f;
        Wake(record.BodyA);
        Wake(record.BodyB);
    }

    public float HingeAngle(int joint)
    {
        ref readonly var record = ref joints[joint];
        return angles[record.BodyB] - angles[record.BodyA] - record.ReferenceAngle;
    }

    public bool IsJointAlive(int joint) => joint >= 0 && joint < nextJoint && joints[joint].Kind != JointKind.None;

    public void DestroyJoint(int joint)
    {
        if (!IsJointAlive(joint))
        {
            return;
        }

        ref var record = ref joints[joint];
        Wake(record.BodyA);
        Wake(record.BodyB);
        jointCounts[record.BodyA]--;
        jointCounts[record.BodyB]--;
        record.Kind = JointKind.None;
        record.Active = false;
        freeJoints[freeJointCount] = joint;
        freeJointCount++;
    }

    public int CreateRope(Vector2 anchor, int body, int segments, float length, int anchorBody = Ground)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(segments, MaxRopeSegments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (!IsAlive(body) || !IsAlive(anchorBody) || body == anchorBody)
        {
            throw new ArgumentException("A rope needs two different live bodies.", nameof(body));
        }

        var rope = FreeRopeSlot();
        var spareBodies = types.Length - nextBody + freeBodyCount;
        var spareJoints = joints.Length - nextJoint + freeJointCount;
        if (rope < 0 || spareBodies < segments - 1 || spareJoints < segments + 1)
        {
            throw new InvalidOperationException("PhysicsWorld rope, body or joint capacity reached.");
        }

        var slot = rope * MaxRopeSegments;
        var end = positions[body];
        var segmentLength = length / segments;
        var linkRadius = segmentLength * RopeLinkRadiusFraction;
        var linkMass = types[body] == BodyType.Dynamic ? masses[body] * RopeLinkMassFraction : RopeLinkMass;
        var linkMaterial = new PhysicsMaterial(linkMass / (MathF.PI * linkRadius * linkRadius), 0f, 0f);
        for (var link = 0; link < segments - 1; link++)
        {
            var linkBody = CreateCircle(BodyType.Dynamic, Vector2.Lerp(anchor, end, (link + 1f) / segments),
                linkRadius, linkMaterial, BodyFlags.FixedRotation);
            categories[linkBody] = 0;
            masks[linkBody] = 0;
            linearDampings[linkBody] = RopeLinkDamping;
            ropeLinks[slot + link] = linkBody;
        }

        for (var segment = 0; segment < segments; segment++)
        {
            var from = segment == 0 ? anchorBody : ropeLinks[slot + segment - 1];
            var to = segment == segments - 1 ? body : ropeLinks[slot + segment];
            var fromPoint = segment == 0 ? anchor : positions[from];
            ropeJoints[slot + segment] = AddDistanceJoint(from, to, fromPoint, positions[to], segmentLength, 0f,
                segmentLength, 0f, 0f);
        }

        ref var record = ref ropes[rope];
        record.Alive = true;
        record.AnchorBody = anchorBody;
        record.Body = body;
        record.Segments = segments;
        record.LocalAnchor = LocalPoint(anchorBody, anchor);
        record.LengthJoint = AddDistanceJoint(anchorBody, body, anchor, end, length, 0f, length, 0f, 0f);
        return rope;
    }

    public bool IsRopeAlive(int rope) => rope >= 0 && rope < ropes.Length && ropes[rope].Alive;

    public bool RopeHolds(int rope) => IsRopeAlive(rope) && ropes[rope].LengthJoint >= 0;

    public int RopeSegments(int rope) => ropes[rope].Segments;

    public bool IsRopeSegmentCut(int rope, int segment) => ropeJoints[rope * MaxRopeSegments + segment] < 0;

    public Vector2 RopePoint(int rope, int index)
    {
        ref readonly var record = ref ropes[rope];
        if (index <= 0)
        {
            return WorldPoint(record.AnchorBody, record.LocalAnchor);
        }

        if (index >= record.Segments)
        {
            return positions[record.Body];
        }

        return positions[ropeLinks[rope * MaxRopeSegments + index - 1]];
    }

    public bool CutRope(int rope, int segment)
    {
        if (!IsRopeAlive(rope) || segment < 0 || segment >= ropes[rope].Segments)
        {
            return false;
        }

        var slot = rope * MaxRopeSegments + segment;
        if (ropeJoints[slot] < 0)
        {
            return false;
        }

        DestroyJoint(ropeJoints[slot]);
        ropeJoints[slot] = -1;
        ref var record = ref ropes[rope];
        if (record.LengthJoint >= 0)
        {
            DestroyJoint(record.LengthJoint);
            record.LengthJoint = -1;
        }

        return true;
    }

    public int CutRopes(Vector2 from, Vector2 to)
    {
        var cut = 0;
        for (var rope = 0; rope < ropes.Length; rope++)
        {
            if (!ropes[rope].Alive)
            {
                continue;
            }

            for (var segment = 0; segment < ropes[rope].Segments; segment++)
            {
                if (IsRopeSegmentCut(rope, segment) ||
                    !Geometry2D.SegmentSegment(RopePoint(rope, segment), RopePoint(rope, segment + 1), from, to))
                {
                    continue;
                }

                CutRope(rope, segment);
                cut++;
                break;
            }
        }

        return cut;
    }

    public void DestroyRope(int rope)
    {
        if (!IsRopeAlive(rope))
        {
            return;
        }

        ref var record = ref ropes[rope];
        record.Alive = false;
        var slot = rope * MaxRopeSegments;
        for (var segment = 0; segment < record.Segments; segment++)
        {
            DestroyJoint(ropeJoints[slot + segment]);
            ropeJoints[slot + segment] = -1;
        }

        DestroyJoint(record.LengthJoint);
        record.LengthJoint = -1;
        for (var link = 0; link < record.Segments - 1; link++)
        {
            DestroyBody(ropeLinks[slot + link]);
        }
    }

    private int FreeRopeSlot()
    {
        for (var rope = 0; rope < ropes.Length; rope++)
        {
            if (!ropes[rope].Alive)
            {
                return rope;
            }
        }

        return -1;
    }

    private void DestroyRopesTouching(int body)
    {
        for (var rope = 0; rope < ropes.Length; rope++)
        {
            if (ropes[rope].Alive && (ropes[rope].AnchorBody == body || ropes[rope].Body == body))
            {
                DestroyRope(rope);
            }
        }
    }

    private void ClearJoints()
    {
        nextJoint = 0;
        freeJointCount = 0;
        Array.Clear(joints);
        Array.Clear(ropes);
    }

    private int AddDistanceJoint(int bodyA, int bodyB, Vector2 anchorA, Vector2 anchorB, float length,
        float minLength, float maxLength, float stiffnessHertz, float dampingRatio)
    {
        var joint = AllocateJoint(JointKind.Distance, bodyA, bodyB);
        ref var record = ref joints[joint];
        record.LocalAnchorA = LocalPoint(bodyA, anchorA);
        record.LocalAnchorB = LocalPoint(bodyB, anchorB);
        record.Length = MathF.Max(length, LinearSlop);
        record.MinLength = minLength;
        record.MaxLength = maxLength;
        record.Stiffness = stiffnessHertz;
        record.DampingRatio = dampingRatio;
        return joint;
    }

    private int AllocateJoint(JointKind kind, int bodyA, int bodyB)
    {
        if (!IsAlive(bodyA) || !IsAlive(bodyB) || bodyA == bodyB)
        {
            throw new ArgumentException("A joint needs two different live bodies.", nameof(bodyB));
        }

        int joint;
        if (freeJointCount > 0)
        {
            freeJointCount--;
            joint = freeJoints[freeJointCount];
        }
        else
        {
            if (nextJoint >= joints.Length)
            {
                throw new InvalidOperationException("PhysicsWorld joint capacity reached.");
            }

            joint = nextJoint;
            nextJoint++;
        }

        ref var record = ref joints[joint];
        record = default;
        record.Kind = kind;
        record.BodyA = bodyA;
        record.BodyB = bodyB;
        jointCounts[bodyA]++;
        jointCounts[bodyB]++;
        Wake(bodyA);
        Wake(bodyB);
        return joint;
    }

    private bool JointConnects(int first, int second)
    {
        for (var joint = 0; joint < nextJoint; joint++)
        {
            ref readonly var record = ref joints[joint];
            if (record.Kind == JointKind.None)
            {
                continue;
            }

            if ((record.BodyA == first && record.BodyB == second) || (record.BodyA == second && record.BodyB == first))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRigid(in Joint joint) => joint.MinLength >= joint.MaxLength;

    private void PrepareJoints()
    {
        for (var joint = 0; joint < nextJoint; joint++)
        {
            ref var record = ref joints[joint];
            record.Active = record.Kind != JointKind.None && (IsSolvable(record.BodyA) || IsSolvable(record.BodyB));
            if (!record.Active)
            {
                continue;
            }

            record.AnchorA = PhysicsMath.Rotate(rotations[record.BodyA], record.LocalAnchorA);
            record.AnchorB = PhysicsMath.Rotate(rotations[record.BodyB], record.LocalAnchorB);
            record.PseudoImpulse = 0f;
            if (record.Kind == JointKind.Distance)
            {
                PrepareDistance(ref record);
                continue;
            }

            PrepareHinge(ref record);
        }
    }

    private void SolveJointVelocities()
    {
        for (var joint = 0; joint < nextJoint; joint++)
        {
            ref var record = ref joints[joint];
            if (!record.Active)
            {
                continue;
            }

            if (record.Kind == JointKind.Distance)
            {
                SolveDistanceVelocity(ref record);
                continue;
            }

            SolveHingeVelocity(ref record);
        }
    }

    private void SolveJointPositions()
    {
        for (var joint = 0; joint < nextJoint; joint++)
        {
            ref var record = ref joints[joint];
            if (!record.Active)
            {
                continue;
            }

            if (record.Kind == JointKind.Distance)
            {
                SolveDistancePosition(ref record);
                continue;
            }

            SolveHingePosition(ref record);
        }
    }

    private void PrepareDistance(ref Joint joint)
    {
        var bodyA = joint.BodyA;
        var bodyB = joint.BodyB;
        var delta = positions[bodyB] + joint.AnchorB - positions[bodyA] - joint.AnchorA;
        var length = delta.Length();
        joint.CurrentLength = length;
        joint.Axis = length > LinearSlop ? delta / length : Vector2.Zero;
        var crossA = PhysicsMath.Cross(joint.AnchorA, joint.Axis);
        var crossB = PhysicsMath.Cross(joint.AnchorB, joint.Axis);
        var inverseMass = SolverInverseMass(bodyA) + SolverInverseInertia(bodyA) * crossA * crossA +
            SolverInverseMass(bodyB) + SolverInverseInertia(bodyB) * crossB * crossB;
        joint.AxialMass = inverseMass > 0f ? 1f / inverseMass : 0f;
        joint.Gamma = 0f;
        joint.Bias = 0f;
        joint.SoftMass = joint.AxialMass;
        var rigid = IsRigid(joint);
        if (joint.Stiffness > 0f && !rigid)
        {
            var omega = 2f * MathF.PI * joint.Stiffness;
            var stiffness = joint.AxialMass * omega * omega;
            var damping = 2f * joint.AxialMass * joint.DampingRatio * omega;
            var gamma = StepSeconds * (damping + StepSeconds * stiffness);
            joint.Gamma = gamma > 0f ? 1f / gamma : 0f;
            joint.Bias = (length - joint.Length) * StepSeconds * stiffness * joint.Gamma;
            var softMass = inverseMass + joint.Gamma;
            joint.SoftMass = softMass > 0f ? 1f / softMass : 0f;
        }

        joint.PositionError = rigid ? length - joint.Length
            : length < joint.MinLength ? length - joint.MinLength
            : length > joint.MaxLength ? length - joint.MaxLength
            : 0f;
        Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB,
            (joint.Impulse + joint.LowerImpulse - joint.UpperImpulse) * joint.Axis);
    }

    private void SolveDistanceVelocity(ref Joint joint)
    {
        var bodyA = joint.BodyA;
        var bodyB = joint.BodyB;
        if (IsRigid(joint))
        {
            var speed = Vector2.Dot(joint.Axis, RelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB));
            var impulse = -joint.AxialMass * speed;
            joint.Impulse += impulse;
            Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB, impulse * joint.Axis);
            return;
        }

        if (joint.Stiffness > 0f)
        {
            var speed = Vector2.Dot(joint.Axis, RelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB));
            var impulse = -joint.SoftMass * (speed + joint.Bias + joint.Gamma * joint.Impulse);
            joint.Impulse += impulse;
            Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB, impulse * joint.Axis);
        }

        if (joint.MinLength > 0f)
        {
            var speed = Vector2.Dot(joint.Axis, RelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB));
            var reach = MathF.Max(0f, joint.CurrentLength - joint.MinLength) * InverseStep;
            var accumulated = MathF.Max(0f, joint.LowerImpulse - joint.AxialMass * (speed + reach));
            var impulse = accumulated - joint.LowerImpulse;
            joint.LowerImpulse = accumulated;
            Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB, impulse * joint.Axis);
        }

        if (joint.MaxLength < float.MaxValue)
        {
            var speed = Vector2.Dot(joint.Axis, RelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB));
            var reach = MathF.Max(0f, joint.MaxLength - joint.CurrentLength) * InverseStep;
            var accumulated = MathF.Max(0f, joint.UpperImpulse - joint.AxialMass * (reach - speed));
            var impulse = accumulated - joint.UpperImpulse;
            joint.UpperImpulse = accumulated;
            Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB, -impulse * joint.Axis);
        }
    }

    private void SolveDistancePosition(ref Joint joint)
    {
        if (joint.PositionError == 0f)
        {
            return;
        }

        var bodyA = joint.BodyA;
        var bodyB = joint.BodyB;
        var speed = Vector2.Dot(joint.Axis, PseudoRelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB));
        var error = Math.Clamp(joint.PositionError, -MaxCorrection, MaxCorrection);
        var accumulated = joint.PseudoImpulse - joint.AxialMass * (speed + Baumgarte * InverseStep * error);
        if (!IsRigid(joint))
        {
            accumulated = joint.PositionError < 0f ? MathF.Max(0f, accumulated) : MathF.Min(0f, accumulated);
        }

        var impulse = accumulated - joint.PseudoImpulse;
        joint.PseudoImpulse = accumulated;
        ExchangePseudo(bodyA, bodyB, joint.AnchorA, joint.AnchorB, impulse * joint.Axis);
    }

    private void PrepareHinge(ref Joint joint)
    {
        var bodyA = joint.BodyA;
        var bodyB = joint.BodyB;
        var inverseInertia = SolverInverseInertia(bodyA) + SolverInverseInertia(bodyB);
        joint.AxialMass = inverseInertia > 0f ? 1f / inverseInertia : 0f;
        joint.CurrentAngle = angles[bodyB] - angles[bodyA] - joint.ReferenceAngle;
        if (!joint.MotorEnabled)
        {
            joint.MotorImpulse = 0f;
        }

        if (!joint.LimitEnabled)
        {
            joint.LowerImpulse = 0f;
            joint.UpperImpulse = 0f;
        }

        joint.PointError = positions[bodyB] + joint.AnchorB - positions[bodyA] - joint.AnchorA;
        var pointErrorLength = joint.PointError.Length();
        if (pointErrorLength > MaxCorrection)
        {
            joint.PointError *= MaxCorrection / pointErrorLength;
        }

        joint.PositionError = !joint.LimitEnabled ? 0f
            : joint.CurrentAngle < joint.LowerAngle ? joint.CurrentAngle - joint.LowerAngle
            : joint.CurrentAngle > joint.UpperAngle ? joint.CurrentAngle - joint.UpperAngle
            : 0f;
        Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB, joint.PointImpulse);
        Twist(bodyA, bodyB, joint.MotorImpulse + joint.LowerImpulse - joint.UpperImpulse);
    }

    private void SolveHingeVelocity(ref Joint joint)
    {
        var bodyA = joint.BodyA;
        var bodyB = joint.BodyB;
        if (joint.MotorEnabled && joint.AxialMass > 0f)
        {
            var speed = angularVelocities[bodyB] - angularVelocities[bodyA] - joint.MotorSpeed;
            var limit = StepSeconds * joint.MaxMotorTorque;
            var accumulated = Math.Clamp(joint.MotorImpulse - joint.AxialMass * speed, -limit, limit);
            var impulse = accumulated - joint.MotorImpulse;
            joint.MotorImpulse = accumulated;
            Twist(bodyA, bodyB, impulse);
        }

        if (joint.LimitEnabled && joint.AxialMass > 0f)
        {
            var lowerSpeed = angularVelocities[bodyB] - angularVelocities[bodyA];
            var lowerReach = MathF.Max(0f, joint.CurrentAngle - joint.LowerAngle) * InverseStep;
            var lowerAccumulated = MathF.Max(0f, joint.LowerImpulse - joint.AxialMass * (lowerSpeed + lowerReach));
            var lowerImpulse = lowerAccumulated - joint.LowerImpulse;
            joint.LowerImpulse = lowerAccumulated;
            Twist(bodyA, bodyB, lowerImpulse);

            var upperSpeed = angularVelocities[bodyA] - angularVelocities[bodyB];
            var upperReach = MathF.Max(0f, joint.UpperAngle - joint.CurrentAngle) * InverseStep;
            var upperAccumulated = MathF.Max(0f, joint.UpperImpulse - joint.AxialMass * (upperSpeed + upperReach));
            var upperImpulse = upperAccumulated - joint.UpperImpulse;
            joint.UpperImpulse = upperAccumulated;
            Twist(bodyA, bodyB, -upperImpulse);
        }

        var velocityError = RelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB);
        var pointImpulse = SolvePoint(joint, -velocityError);
        joint.PointImpulse += pointImpulse;
        Exchange(bodyA, bodyB, joint.AnchorA, joint.AnchorB, pointImpulse);
    }

    private void SolveHingePosition(ref Joint joint)
    {
        var bodyA = joint.BodyA;
        var bodyB = joint.BodyB;
        if (joint.PositionError != 0f && joint.AxialMass > 0f)
        {
            var speed = pseudoAngularVelocities[bodyB] - pseudoAngularVelocities[bodyA];
            var error = Math.Clamp(joint.PositionError, -MaxAngularCorrection, MaxAngularCorrection);
            var accumulated = joint.PseudoImpulse - joint.AxialMass * (speed + Baumgarte * InverseStep * error);
            accumulated = joint.PositionError < 0f ? MathF.Max(0f, accumulated) : MathF.Min(0f, accumulated);
            var impulse = accumulated - joint.PseudoImpulse;
            joint.PseudoImpulse = accumulated;
            TwistPseudo(bodyA, bodyB, impulse);
        }

        var velocityError = PseudoRelativeVelocity(bodyA, bodyB, joint.AnchorA, joint.AnchorB);
        var pointImpulse = SolvePoint(joint, -(velocityError + Baumgarte * InverseStep * joint.PointError));
        ExchangePseudo(bodyA, bodyB, joint.AnchorA, joint.AnchorB, pointImpulse);
    }

    private Vector2 SolvePoint(in Joint joint, Vector2 target)
    {
        var inverseMassSum = SolverInverseMass(joint.BodyA) + SolverInverseMass(joint.BodyB);
        var inverseInertiaA = SolverInverseInertia(joint.BodyA);
        var inverseInertiaB = SolverInverseInertia(joint.BodyB);
        var anchorA = joint.AnchorA;
        var anchorB = joint.AnchorB;
        var massXX = inverseMassSum + anchorA.Y * anchorA.Y * inverseInertiaA + anchorB.Y * anchorB.Y * inverseInertiaB;
        var massXY = -anchorA.Y * anchorA.X * inverseInertiaA - anchorB.Y * anchorB.X * inverseInertiaB;
        var massYY = inverseMassSum + anchorA.X * anchorA.X * inverseInertiaA + anchorB.X * anchorB.X * inverseInertiaB;
        return PhysicsMath.SolveSymmetric(massXX, massXY, massYY, target);
    }

    private void Twist(int bodyA, int bodyB, float impulse)
    {
        if (awake[bodyA])
        {
            angularVelocities[bodyA] -= inverseInertias[bodyA] * impulse;
        }

        if (awake[bodyB])
        {
            angularVelocities[bodyB] += inverseInertias[bodyB] * impulse;
        }
    }

    private void TwistPseudo(int bodyA, int bodyB, float impulse)
    {
        if (awake[bodyA])
        {
            pseudoAngularVelocities[bodyA] -= inverseInertias[bodyA] * impulse;
        }

        if (awake[bodyB])
        {
            pseudoAngularVelocities[bodyB] += inverseInertias[bodyB] * impulse;
        }
    }
}
