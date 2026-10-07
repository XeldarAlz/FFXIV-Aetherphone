using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Doom;

internal enum DoomInstallTarget : byte
{
    Shareware,
    Freedoom,
    Soundfont,
}

internal sealed class DoomApp : IMiniGame
{
    private const string GameId = "doom";
    private const float ScreenAspect = 4f / 3f;
    private const float TipToastSeconds = 6f;
    private const float TipCaptionInset = 14f;
    private const float CardHeight = 66f;
    private const float CompactCardHeight = 44f;
    private const float GameButtonHeight = 40f;
    private const float InstallButtonWidth = 118f;
    private const float LobbyMargin = 18f;
    private const float LandscapeChipClearance = 22f;
    private const long StatusBucketBytes = 102_400;
    private const int InstallTargets = 3;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Doom, GameGenre.Action, L.Doom.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, landscape: true, keyboard: true);
    private static readonly Vector4 TheaterBackdrop = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 ScreenRim = new(1f, 1f, 1f, 0.12f);
    private readonly DoomAssets assets = new();
    private readonly string[] installLabels = new string[InstallTargets];
    private readonly string?[] statusLabels = new string?[InstallTargets];
    private readonly DependencyState[] statusStates = new DependencyState[InstallTargets];
    private readonly long[] statusBuckets = new long[InstallTargets];
    private readonly Vector4 accent = AppAccents.For(GameId);
    private DoomRuntime? runtime;
    private string? failure;
    private string failureLine = string.Empty;
    private LanguageInfo? labelLanguage;
    private bool wasInstalling;
    private bool dragging;
    private float lastDragX;
    private float tipProgress = 1f;

    public GameSpec Spec => StageSpec;

    public void Start(in GameStart start)
    {
        DisposeRuntime();
        SetFailure(null);
        assets.RefreshStates();
    }

    public void Close()
    {
        DisposeRuntime();
    }

    public void Dispose()
    {
        DisposeRuntime();
        assets.Dispose();
    }

    private void DisposeRuntime()
    {
        runtime?.Dispose();
        runtime = null;
        dragging = false;
    }

    private void SetFailure(string? message)
    {
        failure = message;
        failureLine = message is null ? string.Empty : string.Concat(Loc.T(L.Games.DoomFailed), ": ", message);
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var screen = FitScreen(context.Safe);
        var rounding = Metrics.Radius.Lg * scale;
        ProgressRing.Glow(screen.Center, screen.Width * 0.5f, accent, 0.45f);
        Elevation.Floating(drawList, screen.Min, screen.Max, rounding, scale);
        Squircle.Fill(drawList, screen.Min, screen.Max, rounding, ImGui.GetColorU32(TheaterBackdrop));
        Squircle.Stroke(drawList, screen.Min, screen.Max, rounding, ImGui.GetColorU32(ScreenRim), 1f * scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var installing = assets.Installing;
        if (wasInstalling && !installing)
        {
            assets.RefreshStates();
        }

        wasInstalling = installing;
        if (runtime is not null && runtime.Finished)
        {
            DisposeRuntime();
            assets.RefreshStates();
        }

        if (runtime is null)
        {
            DrawLobby(context.Full, theme, scale);
            return;
        }

        DrawGame(context, theme, scale);
    }

    private void TryStart(string iwad)
    {
        try
        {
            runtime = new DoomRuntime(iwad, assets.SoundfontPath(), assets.Folder);
            SetFailure(null);
            tipProgress = 0f;
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine could not start.");
            SetFailure(exception.Message);
            runtime = null;
        }
    }

    private static Rect FitScreen(Rect body)
    {
        var height = body.Height;
        var width = height * ScreenAspect;
        if (width > body.Width)
        {
            width = body.Width;
            height = width / ScreenAspect;
        }

        var min = new Vector2(body.Center.X - width * 0.5f, body.Center.Y - height * 0.5f);
        return new Rect(min, min + new Vector2(width, height));
    }

    private void DrawGame(in GameContext context, PhoneTheme theme, float scale)
    {
        var active = runtime!;
        var full = context.Full;
        var screen = FitScreen(full);
        var running = context.DeltaSeconds > 0f;
        active.Muted = !running;
        var keyboard = running && GameInput.Claim();
        ReadKeyboard(active.Input, keyboard);
        ReadDrag(active.Input, screen);
        try
        {
            active.Tick(context.DeltaSeconds, keyboard);
            active.Render();
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine stopped.");
            SetFailure(exception.Message);
            DisposeRuntime();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(full.Min, full.Max, ImGui.GetColorU32(TheaterBackdrop));
        active.Present(drawList, screen);
        tipProgress = GameBanner.Advance(tipProgress, context.DeltaSeconds, TipToastSeconds);
        if (tipProgress < 1f)
        {
            GameBanner.Draw(drawList, new Vector2(screen.Center.X, screen.Max.Y - TipCaptionInset * scale * 3f),
                Loc.T(L.Games.DoomControls), accent, theme, tipProgress, TextStyles.Subheadline);
        }
    }

    private static void ReadKeyboard(DoomInput input, bool keyboard)
    {
        input.SetHeld(DoomAction.Forward, keyboard && (ImGui.IsKeyDown(ImGuiKey.W) || ImGui.IsKeyDown(ImGuiKey.UpArrow)));
        input.SetHeld(DoomAction.Backward, keyboard && (ImGui.IsKeyDown(ImGuiKey.S) || ImGui.IsKeyDown(ImGuiKey.DownArrow)));
        input.SetHeld(DoomAction.StrafeLeft, keyboard && ImGui.IsKeyDown(ImGuiKey.A));
        input.SetHeld(DoomAction.StrafeRight, keyboard && ImGui.IsKeyDown(ImGuiKey.D));
        input.SetHeld(DoomAction.TurnLeft, keyboard && ImGui.IsKeyDown(ImGuiKey.LeftArrow));
        input.SetHeld(DoomAction.TurnRight, keyboard && ImGui.IsKeyDown(ImGuiKey.RightArrow));
        input.SetHeld(DoomAction.Fire, keyboard && (ImGui.IsKeyDown(ImGuiKey.Space) || ImGui.IsKeyDown(ImGuiKey.LeftCtrl)));
        input.SetHeld(DoomAction.Use, keyboard && (ImGui.IsKeyDown(ImGuiKey.E) || ImGui.IsKeyDown(ImGuiKey.LeftShift)));
        for (var weapon = 0; weapon < 7; weapon++)
        {
            input.SetWeapon(weapon, keyboard && ImGui.IsKeyDown(ImGuiKey.Key1 + weapon));
        }
    }

    private void ReadDrag(DoomInput input, Rect screen)
    {
        var mouse = ImGui.GetMousePos();
        if (!dragging)
        {
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(screen.Min, screen.Max))
            {
                return;
            }

            dragging = true;
            lastDragX = mouse.X;
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            return;
        }

        input.AddTurn(mouse.X - lastDragX);
        lastDragX = mouse.X;
    }

    private void SyncLabels()
    {
        if (ReferenceEquals(labelLanguage, Loc.Current))
        {
            return;
        }

        labelLanguage = Loc.Current;
        installLabels[(int)DoomInstallTarget.Shareware] = Loc.T(L.AetherStream.SetupInstallSized,
            DependencySetup.FormatMegabytes(DoomAssets.SharewareDownloadBytes));
        installLabels[(int)DoomInstallTarget.Freedoom] = Loc.T(L.AetherStream.SetupInstallSized,
            DependencySetup.FormatMegabytes(DoomAssets.FreedoomDownloadBytes));
        installLabels[(int)DoomInstallTarget.Soundfont] = Loc.T(L.AetherStream.SetupInstallSized,
            DependencySetup.FormatMegabytes(DoomAssets.SoundfontDownloadBytes));
        Array.Clear(statusLabels);
        SetFailure(failure);
    }

    private void DrawLobby(Rect body, PhoneTheme theme, float scale)
    {
        SyncLabels();
        var drawList = ImGui.GetWindowDrawList();
        var landscape = body.IsLandscape();
        var margin = LobbyMargin * scale;
        var topClearance = landscape ? LandscapeChipClearance * scale : StageLayout.ChromeBand * scale;
        var content = new Rect(body.Min + new Vector2(margin, margin + topClearance), body.Max - new Vector2(margin, margin));
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, content.Min.Y + titleHeight * 0.5f),
            assets.AvailableIwadCount > 0 ? Loc.T(L.Games.DoomChooseGame) : Loc.T(L.Games.DoomSetupTitle), StageInks.Strong,
            TextStyles.Title2);
        var cursorY = content.Min.Y + titleHeight + 6f * scale;
        if (failureLine.Length > 0)
        {
            cursorY += Typography.DrawWrappedCentered(new Vector2(content.Center.X, cursorY), failureLine, theme.Danger,
                TextStyles.Caption1, content.Width) + 6f * scale;
        }

        Rect gamesColumn;
        Rect cardsColumn;
        if (landscape)
        {
            var gap = 16f * scale;
            var columnWidth = (content.Width - gap) * 0.5f;
            gamesColumn = new Rect(new Vector2(content.Min.X, cursorY), new Vector2(content.Min.X + columnWidth, content.Max.Y));
            cardsColumn = new Rect(new Vector2(content.Max.X - columnWidth, cursorY), new Vector2(content.Max.X, content.Max.Y));
        }
        else
        {
            var gamesHeight = assets.AvailableIwadCount * (GameButtonHeight + 8f) * scale;
            gamesColumn = new Rect(new Vector2(content.Min.X, cursorY), new Vector2(content.Max.X, cursorY + gamesHeight));
            cardsColumn = new Rect(new Vector2(content.Min.X, gamesColumn.Max.Y + 10f * scale), new Vector2(content.Max.X, content.Max.Y));
        }

        DrawGameButtons(gamesColumn, theme, scale);
        DrawInstallCards(cardsColumn, theme, scale);
    }

    private void DrawGameButtons(Rect column, PhoneTheme theme, float scale)
    {
        var buttonHeight = GameButtonHeight * scale;
        var gap = 8f * scale;
        var y = column.Min.Y;
        for (var index = 0; index < assets.AvailableIwadCount; index++)
        {
            var iwad = assets.AvailableIwad(index);
            var label = iwad.Title ?? Loc.T(L.Games.DoomShareware);
            var center = new Vector2(column.Center.X, y + buttonHeight * 0.5f);
            if (GameHud.Button(center, new Vector2(column.Width, buttonHeight), label, accent, theme))
            {
                TryStart(assets.PathFor(in iwad));
            }

            y += buttonHeight + gap;
        }

        if (assets.AvailableIwadCount == 0)
        {
            Typography.DrawWrappedCentered(new Vector2(column.Center.X, column.Min.Y), Loc.T(L.Games.DoomSetupBody),
                StageInks.Muted, TextStyles.Subheadline, column.Width);
        }
    }

    private void DrawInstallCards(Rect column, PhoneTheme theme, float scale)
    {
        var gap = 8f * scale;
        var cards = (assets.HasShareware ? 0 : 1) + (assets.HasFreedoom ? 0 : 1) + (assets.HasSoundfont ? 0 : 1);
        var fitted = cards == 0 ? 0f : (column.Height - gap * (cards - 1)) / cards;
        var cardHeight = Math.Clamp(fitted, CompactCardHeight * scale, CardHeight * scale);
        var y = column.Min.Y;
        if (!assets.HasShareware)
        {
            DrawCard(new Rect(new Vector2(column.Min.X, y), new Vector2(column.Max.X, y + cardHeight)), Loc.T(L.Games.DoomGameData),
                Loc.T(L.Games.DoomGameDataDetail), assets.Shareware, DoomInstallTarget.Shareware, theme, scale);
            y += cardHeight + gap;
        }

        if (!assets.HasFreedoom)
        {
            DrawCard(new Rect(new Vector2(column.Min.X, y), new Vector2(column.Max.X, y + cardHeight)), Loc.T(L.Games.DoomFreedoom),
                Loc.T(L.Games.DoomFreedoomDetail), assets.Freedoom, DoomInstallTarget.Freedoom, theme, scale);
            y += cardHeight + gap;
        }

        if (!assets.HasSoundfont)
        {
            DrawCard(new Rect(new Vector2(column.Min.X, y), new Vector2(column.Max.X, y + cardHeight)), Loc.T(L.Games.DoomMusic),
                Loc.T(L.Games.DoomMusicDetail), assets.Soundfont, DoomInstallTarget.Soundfont, theme, scale);
        }
    }

    private void DrawCard(Rect card, string title, string detail, MediaDependency dependency, DoomInstallTarget target,
        PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var snapshot = dependency.Snapshot();
        var radius = 14f * scale;
        Material.Frosted(drawList, card.Min, card.Max, radius, scale);
        var pad = 12f * scale;
        var buttonWidth = InstallButtonWidth * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad - buttonWidth - pad;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, card.Min.Y + pad * 0.7f),
            Typography.FitText(title, right - left, TextStyles.BodyEmphasized), StageInks.Strong, TextStyles.BodyEmphasized);
        var detailY = card.Min.Y + pad * 0.7f + titleHeight;
        var lineHeight = Typography.LineHeight(TextStyles.Caption1);
        var statusY = detailY + lineHeight + 2f * scale;
        var roomy = statusY + lineHeight <= card.Max.Y - pad * 0.3f;
        var urgent = snapshot.State is DependencyState.Failed or DependencyState.Downloading;
        if (roomy || !urgent)
        {
            Typography.Draw(drawList, new Vector2(left, detailY),
                Typography.FitText(detail, right - left, TextStyles.Caption1), StageInks.Muted, TextStyles.Caption1);
        }

        if (roomy || urgent)
        {
            var statusColor = snapshot.State == DependencyState.Failed ? theme.Danger : StageInks.Muted;
            Typography.Draw(drawList, new Vector2(left, roomy ? statusY : detailY),
                Typography.FitText(StatusLabel(target, in snapshot), right - left, TextStyles.Caption1), statusColor,
                TextStyles.Caption1);
        }

        var busy = DependencySetup.IsBusy(snapshot);
        var label = busy
            ? Loc.T(L.AetherStream.SetupInstalling)
            : snapshot.State == DependencyState.Failed
                ? Loc.T(L.AetherStream.SetupRetry)
                : installLabels[(int)target];
        var buttonCenter = new Vector2(card.Max.X - pad - buttonWidth * 0.5f, card.Center.Y);
        if (GameHud.Button(buttonCenter, new Vector2(buttonWidth, 34f * scale), label, accent, theme) && !busy && !assets.Installing)
        {
            Install(target);
        }

        if (snapshot.State != DependencyState.Downloading)
        {
            return;
        }

        var barTop = card.Max.Y - 5f * scale;
        var barMin = new Vector2(left, barTop);
        var barMax = new Vector2(right, barTop + 3f * scale);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), 1.5f * scale);
        drawList.AddRectFilled(barMin, new Vector2(left + (right - left) * snapshot.Fraction, barMax.Y),
            ImGui.GetColorU32(accent), 1.5f * scale);
    }

    private string StatusLabel(DoomInstallTarget target, in DependencyProgress snapshot)
    {
        var slot = (int)target;
        var bucket = snapshot.ReceivedBytes / StatusBucketBytes;
        var cached = statusLabels[slot];
        if (cached is not null && statusStates[slot] == snapshot.State && statusBuckets[slot] == bucket)
        {
            return cached;
        }

        statusStates[slot] = snapshot.State;
        statusBuckets[slot] = bucket;
        var label = DependencySetup.StatusText(snapshot);
        statusLabels[slot] = label;
        return label;
    }

    private void Install(DoomInstallTarget target)
    {
        var soundfont = !assets.HasSoundfont;
        switch (target)
        {
            case DoomInstallTarget.Shareware:
                assets.Install(true, false, soundfont);
                return;
            case DoomInstallTarget.Freedoom:
                assets.Install(false, true, soundfont);
                return;
            default:
                assets.Install(false, false, true);
                return;
        }
    }
}
