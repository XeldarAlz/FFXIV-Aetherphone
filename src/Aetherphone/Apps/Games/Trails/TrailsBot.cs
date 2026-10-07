using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Trails;

internal sealed class TrailsBot
{
    public const int EasyDepth = 12;
    public const float EasyWander = 0.04f;
    public const int EasyRoom = EasyDepth / 2;
    public const float HardJitter = 2f;
    private const float StraightBonus = 0.5f;
    private const float ReachWeight = 0.25f;
    private const float WallHugWeight = 0.6f;
    private const float HeadOnPenalty = 40f;
    private const sbyte Contested = -1;

    private readonly int[] queue = new int[TrailsBoard.CellCount];
    private readonly int[] distance = new int[TrailsBoard.CellCount];
    private readonly int[] visited = new int[TrailsBoard.CellCount];
    private readonly sbyte[] claimant = new sbyte[TrailsBoard.CellCount];
    private int stamp;

    public Heading Choose(TrailsBoard board, int index, BotSkill skill, ref GameRandom random)
    {
        ref readonly var rider = ref board.RiderAt(index);
        var current = rider.Heading;
        Span<Heading> options = stackalloc Heading[3]
        {
            current, TrailsBoard.TurnLeft(current), TrailsBoard.TurnRight(current),
        };
        Span<Heading> roomy = stackalloc Heading[3];
        var roomyCount = 0;
        var best = current;
        var bestScore = float.MinValue;
        for (var option = 0; option < options.Length; option++)
        {
            var heading = options[option];
            var column = rider.Column + TrailsBoard.DeltaX(heading);
            var row = rider.Row + TrailsBoard.DeltaY(heading);
            if (!board.IsFree(column, row))
            {
                continue;
            }

            var score = skill == BotSkill.Easy
                ? Flood(board, column, row, EasyDepth)
                : HardScore(board, index, column, row) + random.NextFloat() * HardJitter;
            if (score >= EasyRoom)
            {
                roomy[roomyCount++] = heading;
            }

            if (heading == current)
            {
                score += StraightBonus;
            }

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = heading;
        }

        if (skill == BotSkill.Easy && roomyCount > 1 && random.Chance(EasyWander))
        {
            return roomy[random.Next(roomyCount)];
        }

        return best;
    }

    public int Flood(TrailsBoard board, int startColumn, int startRow, int limit)
    {
        stamp++;
        var start = TrailsBoard.CellIndex(startColumn, startRow);
        var head = 0;
        var tail = 0;
        visited[start] = stamp;
        queue[tail++] = start;
        var count = 0;
        while (head < tail && count < limit)
        {
            var cell = queue[head++];
            count++;
            var column = cell % TrailsBoard.Columns;
            var row = cell / TrailsBoard.Columns;
            tail = Visit(board, column, row - 1, tail);
            tail = Visit(board, column + 1, row, tail);
            tail = Visit(board, column, row + 1, tail);
            tail = Visit(board, column - 1, row, tail);
        }

        return count;
    }

    private float HardScore(TrailsBoard board, int index, int column, int row)
    {
        var reach = Flood(board, column, row, TrailsBoard.CellCount);
        var territory = Territory(board, index, column, row);
        var score = territory + reach * ReachWeight + WallNeighbours(board, column, row) * WallHugWeight;
        if (NearOtherHead(board, index, column, row))
        {
            score -= HeadOnPenalty;
        }

        return score;
    }

    private int Territory(TrailsBoard board, int self, int startColumn, int startRow)
    {
        stamp++;
        var tail = 0;
        var start = TrailsBoard.CellIndex(startColumn, startRow);
        visited[start] = stamp;
        distance[start] = 0;
        claimant[start] = (sbyte)self;
        queue[tail++] = start;
        for (var other = 0; other < board.RiderCount; other++)
        {
            ref readonly var rider = ref board.RiderAt(other);
            if (other == self || !rider.Alive)
            {
                continue;
            }

            var cell = TrailsBoard.CellIndex(rider.Column, rider.Row);
            visited[cell] = stamp;
            distance[cell] = 0;
            claimant[cell] = (sbyte)other;
            queue[tail++] = cell;
        }

        var head = 0;
        while (head < tail)
        {
            var cell = queue[head++];
            var column = cell % TrailsBoard.Columns;
            var row = cell / TrailsBoard.Columns;
            tail = Claim(board, cell, column, row - 1, tail);
            tail = Claim(board, cell, column + 1, row, tail);
            tail = Claim(board, cell, column, row + 1, tail);
            tail = Claim(board, cell, column - 1, row, tail);
        }

        var owned = 0;
        for (var entry = 0; entry < tail; entry++)
        {
            if (claimant[queue[entry]] == self)
            {
                owned++;
            }
        }

        return owned;
    }

    private int Visit(TrailsBoard board, int column, int row, int tail)
    {
        if (!board.IsFree(column, row))
        {
            return tail;
        }

        var cell = TrailsBoard.CellIndex(column, row);
        if (visited[cell] == stamp)
        {
            return tail;
        }

        visited[cell] = stamp;
        queue[tail] = cell;
        return tail + 1;
    }

    private int Claim(TrailsBoard board, int from, int column, int row, int tail)
    {
        if (!board.IsFree(column, row))
        {
            return tail;
        }

        var cell = TrailsBoard.CellIndex(column, row);
        var reached = distance[from] + 1;
        if (visited[cell] == stamp)
        {
            if (distance[cell] == reached && claimant[cell] != claimant[from])
            {
                claimant[cell] = Contested;
            }

            return tail;
        }

        visited[cell] = stamp;
        distance[cell] = reached;
        claimant[cell] = claimant[from];
        queue[tail] = cell;
        return tail + 1;
    }

    private static int WallNeighbours(TrailsBoard board, int column, int row)
    {
        var walls = 0;
        walls += board.IsFree(column, row - 1) ? 0 : 1;
        walls += board.IsFree(column + 1, row) ? 0 : 1;
        walls += board.IsFree(column, row + 1) ? 0 : 1;
        walls += board.IsFree(column - 1, row) ? 0 : 1;
        return walls;
    }

    private static bool NearOtherHead(TrailsBoard board, int self, int column, int row)
    {
        for (var other = 0; other < board.RiderCount; other++)
        {
            ref readonly var rider = ref board.RiderAt(other);
            if (other == self || !rider.Alive)
            {
                continue;
            }

            if (Math.Abs(rider.Column - column) + Math.Abs(rider.Row - row) == 1)
            {
                return true;
            }
        }

        return false;
    }
}
