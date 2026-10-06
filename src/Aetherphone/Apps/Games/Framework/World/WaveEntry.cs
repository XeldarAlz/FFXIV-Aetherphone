namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct WaveEntry
{
    public readonly float Time;
    public readonly byte Lane;
    public readonly byte EnemyKind;

    public WaveEntry(float time, byte lane, byte enemyKind)
    {
        Time = time;
        Lane = lane;
        EnemyKind = enemyKind;
    }
}
