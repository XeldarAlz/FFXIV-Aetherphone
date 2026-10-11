using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class CasinoSfx
{
    public const long BigWinThrottleMilliseconds = 10_000;
    public const float StepPitch = 0.045f;
    public const float MaxPitch = 1.9f;

    private static int armedFrame = -1;
    private static bool armedFocused;
    private static long lastBigWinTick = long.MinValue / 2;

    public static void Arm(bool focused)
    {
        armedFrame = ImGui.GetFrameCount();
        armedFocused = focused;
    }

    public static bool Audible
    {
        get
        {
            var frame = ImGui.GetFrameCount();
            return armedFocused && (armedFrame == frame || armedFrame == frame - 1);
        }
    }

    public static void Play(UiSound sound)
    {
        if (!Audible)
        {
            return;
        }

        if (IsBigWin(sound) && !TryTakeBigWinSlot(Environment.TickCount64))
        {
            return;
        }

        UiFeedback.Play(sound);
    }

    public static void Pitched(UiSound sound, int step)
    {
        if (!Audible)
        {
            return;
        }

        UiFeedback.PlayPitched(sound, PitchFor(step));
    }

    public static void Win(in WinTierSpec spec)
    {
        if (!Audible || spec.Tier == WinTier.None)
        {
            return;
        }

        if (IsBigWin(spec.Sound) && !TryTakeBigWinSlot(Environment.TickCount64))
        {
            UiFeedback.Play(UiSound.WinSmall);
            return;
        }

        UiFeedback.Play(spec.Sound);
        if (spec.Fanfare)
        {
            UiFeedback.Play(UiSound.Fanfare);
        }
    }

    public static float PitchFor(int step) => MathF.Min(MaxPitch, 1f + Math.Max(0, step) * StepPitch);

    public static bool IsBigWin(UiSound sound) => sound is UiSound.WinBig or UiSound.WinEpic or UiSound.Fanfare;

    internal static bool TryTakeBigWinSlot(long nowTick)
    {
        if (nowTick - lastBigWinTick < BigWinThrottleMilliseconds)
        {
            return false;
        }

        lastBigWinTick = nowTick;
        return true;
    }

    internal static void ResetThrottle()
    {
        lastBigWinTick = long.MinValue / 2;
    }
}
