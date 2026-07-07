namespace Aetherphone.Core.Animation;

internal sealed class BootSequence
{
    private const float EmblemStartScale = 0.7f;

    private static readonly string[] Greetings = { "Hello!", "Bonjour!", "Hola!", "Ciao!", "Olá!", };

    private bool full;
    private float elapsed;
    private float powerOnSeconds;
    private float emblemInSeconds;
    private float emblemHoldSeconds;
    private float emblemExitSeconds;
    private float greetingInSeconds;
    private float greetingHoldSeconds;
    private float greetingOutSeconds;
    private int greetingCount;
    private float revealSeconds;
    private float gateWait;

    public bool IsActive { get; private set; }
    public float BackdropAlpha { get; private set; }
    public float EmblemAlpha { get; private set; }
    public float EmblemScale { get; private set; } = 1f;
    public float EmblemRingProgress { get; private set; }
    public float EmblemRingAlpha { get; private set; }
    public string? Greeting { get; private set; }
    public float GreetingReveal { get; private set; }
    public float GreetingAlpha { get; private set; }
    public float GreetingDrift { get; private set; }

    public void Begin(bool fullSequence)
    {
        full = fullSequence;
        elapsed = 0f;
        IsActive = true;
        powerOnSeconds = fullSequence ? BootTiming.PowerOnSeconds : BootTiming.ShortPowerOnSeconds;
        emblemInSeconds = fullSequence ? BootTiming.EmblemInSeconds : BootTiming.ShortEmblemInSeconds;
        emblemHoldSeconds = fullSequence ? BootTiming.EmblemHoldSeconds : BootTiming.ShortEmblemHoldSeconds;
        emblemExitSeconds = fullSequence ? BootTiming.EmblemExitSeconds : BootTiming.ShortEmblemExitSeconds;
        greetingInSeconds = BootTiming.GreetingInSeconds;
        greetingHoldSeconds = BootTiming.GreetingHoldSeconds;
        greetingOutSeconds = BootTiming.GreetingOutSeconds;
        greetingCount = fullSequence ? Greetings.Length : 0;
        revealSeconds = fullSequence ? BootTiming.RevealSeconds : BootTiming.ShortRevealSeconds;
        gateWait = 0f;
        Recompute();
    }

    public void Cancel()
    {
        IsActive = false;
        elapsed = 0f;
        ClearChannels();
        BackdropAlpha = 0f;
    }

    public void Advance(float deltaSeconds)
    {
        if (!IsActive)
        {
            return;
        }

        elapsed += deltaSeconds;

        if (IsWaitingForFonts())
        {
            gateWait += deltaSeconds;
        }

        if (elapsed >= TotalSeconds)
        {
            Complete();
            return;
        }

        Recompute();
    }

    private bool IsWaitingForFonts()
    {
        if (gateWait >= BootTiming.FontWaitCapSeconds)
        {
            return false;
        }

        var holdEnd = powerOnSeconds + emblemInSeconds + emblemHoldSeconds + gateWait;
        return elapsed >= holdEnd && !Plugin.Fonts.Ready;
    }

    private float TotalSeconds => powerOnSeconds + EmblemDuration + GreetingsDuration + revealSeconds;
    private float EmblemDuration => emblemInSeconds + emblemHoldSeconds + gateWait + emblemExitSeconds;
    private float NonLastGreetingLife => greetingInSeconds + greetingHoldSeconds + greetingOutSeconds;
    private float LastGreetingLife => greetingInSeconds + greetingHoldSeconds;
    private float GreetingsDuration => greetingCount == 0 ? 0f : (greetingCount - 1) * NonLastGreetingLife + LastGreetingLife;

    private void Complete()
    {
        IsActive = false;
        ClearChannels();
        BackdropAlpha = 0f;

        if (!full || Plugin.Cfg.WelcomeShown)
        {
            return;
        }

        Plugin.Cfg.WelcomeShown = true;
        Plugin.Cfg.Save();
    }

    private void Recompute()
    {
        ClearChannels();
        BackdropAlpha = 1f;
        var emblemStart = powerOnSeconds;
        var greetingsStart = emblemStart + EmblemDuration;
        var revealStart = greetingsStart + GreetingsDuration;

        if (elapsed < emblemStart)
        {
            return;
        }

        if (elapsed < greetingsStart)
        {
            ComputeEmblem(elapsed - emblemStart);
            return;
        }

        if (elapsed < revealStart)
        {
            ComputeGreeting(elapsed - greetingsStart);
            return;
        }

        ComputeReveal((elapsed - revealStart) / revealSeconds);
    }

    private void ComputeEmblem(float phase)
    {
        var holdEnd = emblemInSeconds + emblemHoldSeconds + gateWait;

        if (phase < emblemInSeconds)
        {
            var progress = Easing.EaseOutCubic(phase / emblemInSeconds);
            EmblemAlpha = progress;
            EmblemScale = Easing.Lerp(EmblemStartScale, 1f, progress);
        }
        else if (phase < holdEnd)
        {
            EmblemAlpha = 1f;
            EmblemScale = 1f + BootTiming.EmblemBreatheAmplitude *
                MathF.Sin((phase - emblemInSeconds) * BootTiming.EmblemBreatheFrequency);
        }
        else
        {
            var exit = (phase - holdEnd) / emblemExitSeconds;
            EmblemAlpha = 1f - Easing.SmoothStep(exit);
            EmblemScale = Easing.Lerp(1f, BootTiming.EmblemExitGrowth, Easing.EaseInCubic(exit));
        }

        if (!(phase < holdEnd))
        {
            return;
        }

        EmblemRingProgress = phase % BootTiming.EmblemRingPeriod / BootTiming.EmblemRingPeriod;
        EmblemRingAlpha = (1f - EmblemRingProgress) * EmblemAlpha;
    }

    private void ComputeGreeting(float phase)
    {
        var index = 0;
        var local = phase;

        while (index < greetingCount - 1 && local >= NonLastGreetingLife)
        {
            local -= NonLastGreetingLife;
            index++;
        }

        var isLast = index == greetingCount - 1;
        Greeting = Greetings[index];
        GreetingReveal = Easing.Clamp01(local / greetingInSeconds);

        if (isLast)
        {
            GreetingAlpha = 1f;
            GreetingDrift = 0f;
            return;
        }

        var outStart = greetingInSeconds + greetingHoldSeconds;

        if (local <= outStart)
        {
            GreetingAlpha = 1f;
            GreetingDrift = 0f;
            return;
        }

        var outProgress = Easing.SmoothStep((local - outStart) / greetingOutSeconds);
        GreetingAlpha = 1f - outProgress;
        GreetingDrift = outProgress;
    }

    private void ComputeReveal(float progress)
    {
        var fade = 1f - BootTiming.RevealCurve(progress);
        BackdropAlpha = fade;

        if (greetingCount == 0)
        {
            return;
        }

        Greeting = Greetings[greetingCount - 1];
        GreetingReveal = 1f;
        GreetingAlpha = fade;
        GreetingDrift = 1f - fade;
    }

    private void ClearChannels()
    {
        EmblemAlpha = 0f;
        EmblemScale = 1f;
        EmblemRingProgress = 0f;
        EmblemRingAlpha = 0f;
        Greeting = null;
        GreetingReveal = 0f;
        GreetingAlpha = 0f;
        GreetingDrift = 0f;
    }
}
