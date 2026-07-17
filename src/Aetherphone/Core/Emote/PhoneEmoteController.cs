using Aetherphone.Core.Linkpearl;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using CharacterStruct = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Aetherphone.Core.Emote;

internal sealed class PhoneEmoteController : IDisposable
{
    private const ushort TomescrollEmoteId = 295;
    private const ushort TomestoneEmoteId = 191;
    private const long StillnessDelayMilliseconds = 400;
    private const long RecastCooldownMilliseconds = 1000;
    private const float MovementThreshold = 0.0025f;
    private const float RotationThreshold = 0.02f;

    private static readonly ConditionFlag[] BlockingConditions =
    {
        ConditionFlag.InCombat, ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51,
        ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78,
        ConditionFlag.OccupiedInQuestEvent, ConditionFlag.Casting,
    };

    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IDataManager dataManager;
    private readonly Func<bool> isPhoneVisible;
    private bool commandsResolved;
    private string loopCommand = string.Empty;
    private string onceCommand = string.Empty;
    private Vector3 lastPosition;
    private float lastRotation;
    private bool hasSample;
    private long stillSinceMilliseconds;
    private long lastCastMilliseconds;

    public PhoneEmoteController(Configuration configuration, IFramework framework, IObjectTable objectTable,
        ICondition condition, IDataManager dataManager, Func<bool> isPhoneVisible)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.objectTable = objectTable;
        this.condition = condition;
        this.dataManager = dataManager;
        this.isPhoneVisible = isPhoneVisible;
        this.framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework owner)
    {
        if (!configuration.ScrollWhileIdle || !isPhoneVisible())
        {
            hasSample = false;
            return;
        }

        var player = objectTable.LocalPlayer;
        if (player is null || IsBlocked())
        {
            hasSample = false;
            return;
        }

        var now = Environment.TickCount64;
        if (HasMoved(player.Position, player.Rotation, now))
        {
            return;
        }

        if (now - stillSinceMilliseconds < StillnessDelayMilliseconds)
        {
            return;
        }

        if (now - lastCastMilliseconds < RecastCooldownMilliseconds)
        {
            return;
        }

        if (IsBusy(player.Address))
        {
            return;
        }

        if (!TrySelectCommand(out var command))
        {
            return;
        }

        if (ChatSender.TrySend(command))
        {
            lastCastMilliseconds = now;
        }
    }

    private bool IsBlocked()
    {
        for (var index = 0; index < BlockingConditions.Length; index++)
        {
            if (condition[BlockingConditions[index]])
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMoved(Vector3 position, float rotation, long now)
    {
        if (!hasSample)
        {
            lastPosition = position;
            lastRotation = rotation;
            hasSample = true;
            stillSinceMilliseconds = now;
            return true;
        }

        var distanceSquared = Vector3.DistanceSquared(position, lastPosition);
        var rotationDelta = MathF.Abs(WrapAngle(rotation - lastRotation));
        lastPosition = position;
        lastRotation = rotation;
        if (distanceSquared > MovementThreshold || rotationDelta > RotationThreshold)
        {
            stillSinceMilliseconds = now;
            return true;
        }

        return false;
    }

    private bool TrySelectCommand(out string command)
    {
        EnsureCommandsResolved();
        if (loopCommand.Length > 0 && IsUnlocked(TomescrollEmoteId))
        {
            command = loopCommand;
            return true;
        }

        if (onceCommand.Length > 0 && IsUnlocked(TomestoneEmoteId))
        {
            command = onceCommand;
            return true;
        }

        command = string.Empty;
        return false;
    }

    private void EnsureCommandsResolved()
    {
        if (commandsResolved)
        {
            return;
        }

        commandsResolved = true;
        loopCommand = BuildCommand(TomescrollEmoteId);
        onceCommand = BuildCommand(TomestoneEmoteId);
    }

    private string BuildCommand(ushort emoteId)
    {
        if (!dataManager.GetExcelSheet<Lumina.Excel.Sheets.Emote>().TryGetRow(emoteId, out var emote) ||
            emote.TextCommand.RowId == 0)
        {
            return string.Empty;
        }

        var text = emote.TextCommand.Value.Command.ExtractText();
        return string.IsNullOrEmpty(text) ? string.Empty : string.Concat(text, " motion");
    }

    private static unsafe bool IsBusy(nint address)
    {
        if (address == nint.Zero)
        {
            return true;
        }

        return ((CharacterStruct*)address)->Mode != CharacterModes.Normal;
    }

    private static unsafe bool IsUnlocked(ushort emoteId)
    {
        var state = UIState.Instance();
        return state != null && state->IsEmoteUnlocked(emoteId);
    }

    private static float WrapAngle(float radians)
    {
        const float fullTurn = MathF.PI * 2f;
        radians %= fullTurn;
        if (radians > MathF.PI)
        {
            radians -= fullTurn;
        }
        else if (radians < -MathF.PI)
        {
            radians += fullTurn;
        }

        return radians;
    }
}
