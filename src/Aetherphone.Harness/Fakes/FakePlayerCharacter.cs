using Aetherphone.Core.Game;
using Aetherphone.Harness.Scenarios;
using Dalamud.Game.ClientState.Customize;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakePlayerCharacter : IPlayerCharacter
{
    private readonly CharacterProfile profile;
    private readonly ExcelModule excel;
    private readonly byte[] customize;

    public FakePlayerCharacter(CharacterProfile profile, ExcelModule excel)
    {
        this.profile = profile;
        this.excel = excel;
        customize = (byte[])profile.Customize.Clone();
        Name = new SeStringBuilder().AddText(profile.Name).Build();
        CompanyTag = new SeStringBuilder().AddText(profile.CompanyTag).Build();
    }

    public SeString Name { get; }

    public SeString CompanyTag { get; }

    public RowRef<World> HomeWorld => new(excel, profile.HomeWorldId);

    public RowRef<World> CurrentWorld => new(excel, profile.CurrentWorldId);

    public RowRef<ClassJob> ClassJob => new(excel, GameMemory.Simulated?.CurrentClassJobId ?? 0);

    public byte Level
    {
        get
        {
            if (GameMemory.Simulated is not { } simulated ||
                !excel.GetSheet<ClassJob>().TryGetRow(simulated.CurrentClassJobId, out var job))
            {
                return 0;
            }

            var index = job.ExpArrayIndex;
            return index >= 0 && index < simulated.ClassJobLevels.Length ? (byte)simulated.ClassJobLevels[index] : (byte)0;
        }
    }

    public Span<byte> Customize => customize;

    public ICustomizeData CustomizeData => null!;

    public StatusList StatusList => null!;

    public RowRef<OnlineStatus> OnlineStatus => new(excel, 0);

    public StatusFlags StatusFlags => StatusFlags.None;

    public RowRef<Mount>? CurrentMount => null;

    public RowRef<Companion>? CurrentMinion => null;

    public uint CurrentHp => 1;

    public uint MaxHp => 1;

    public uint CurrentMp => 10000;

    public uint MaxMp => 10000;

    public uint CurrentGp => 0;

    public uint MaxGp => 0;

    public uint CurrentCp => 0;

    public uint MaxCp => 0;

    public byte ShieldPercentage => 0;

    public uint NameId => 0;

    public bool IsCasting => false;

    public bool IsCastInterruptible => false;

    public byte CastActionType => 0;

    public uint CastActionId => 0;

    public ulong CastTargetObjectId => 0;

    public float CurrentCastTime => 0f;

    public float BaseCastTime => 0f;

    public float TotalCastTime => 0f;

    public ulong GameObjectId => profile.ContentId;

    public uint EntityId => (uint)profile.ContentId;

    public uint DataId => 0;

    public uint BaseId => 0;

    public uint OwnerId => 0;

    public ushort ObjectIndex => 0;

    public ObjectKind ObjectKind => ObjectKind.Pc;

    public byte SubKind => 0;

    public byte YalmDistanceX => 0;

    public byte YalmDistanceZ => 0;

    public byte CurrentDistance => 0;

    public byte NextDistance => 0;

    public bool IsDead => false;

    public bool IsTargetable => true;

    public Vector3 Position => Vector3.Zero;

    public float Rotation => 0f;

    public float HitboxRadius => 0.5f;

    public ulong TargetObjectId => 0;

    public IGameObject? TargetObject => null;

    public nint Address => 0;

    public bool IsValid() => true;

    public bool Equals(IGameObject? other) => ReferenceEquals(this, other);
}
