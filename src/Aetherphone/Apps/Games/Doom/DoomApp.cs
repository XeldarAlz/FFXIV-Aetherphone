using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomApp : ILegacyMiniGame
{
    private const string GameId = "doom";
    private const float ScreenAspect = 4f / 3f;
    private const float TipToastSeconds = 6f;
    private const float TipCaptionInset = 14f;
    private const float CardHeight = 66f;
    private const float GameButtonHeight = 40f;
    private const float InstallButtonWidth = 118f;
    private static readonly Vector4 TheaterBackdrop = new(0f, 0f, 0f, 1f);
    private readonly DoomAssets assets = new();
    private DoomRuntime? runtime;
    private string? failure;
    private bool dragging;
    private float lastDragX;
    private float tipProgress = 1f;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Doom);
    public GameGenre Genre => GameGenre.Action;
    public bool RunsOnAClock => true;
    public bool WantsLandscape => true;

    public void Open()
    {
        assets.RefreshStates();
        failure = null;
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

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        if (runtime is not null && runtime.Finished)
        {
            DisposeRuntime();
            assets.RefreshStates();
        }

        if (runtime is null)
        {
            GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
            DrawLobby(body, theme, scale);
            return;
        }

        DrawGame(context, body, theme, scale);
    }

    private void TryStart(string iwad)
    {
        try
        {
            runtime = new DoomRuntime(iwad, assets.SoundfontPath(), assets.Folder);
            failure = null;
            tipProgress = 0f;
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine could not start.");
            failure = exception.Message;
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

    private void DrawGame(in GameContext context, Rect body, PhoneTheme theme, float scale)
    {
        var active = runtime!;
        var screen = FitScreen(body);
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
            failure = exception.Message;
            DisposeRuntime();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(TheaterBackdrop), theme.ScreenRounding * scale);
        active.Present(drawList, screen);
        tipProgress = GameBanner.Advance(tipProgress, context.DeltaSeconds, TipToastSeconds);
        if (tipProgress < 1f)
        {
            GameBanner.Draw(drawList, new Vector2(screen.Center.X, screen.Max.Y - TipCaptionInset * scale * 3f),
                Loc.T(L.Games.DoomControls), Accent, theme, tipProgress, TextStyles.Subheadline);
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

    private void DrawLobby(Rect body, PhoneTheme theme, float scale)
    {
        assets.RefreshStates();
        var drawList = ImGui.GetWindowDrawList();
        var landscape = body.IsLandscape();
        var margin = 18f * scale;
        var content = new Rect(body.Min + new Vector2(margin, margin + (landscape ? 22f * scale : 0f)),
            body.Max - new Vector2(margin, margin));
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, content.Min.Y + titleHeight * 0.5f),
            assets.AvailableIwadCount > 0 ? Loc.T(L.Games.DoomChooseGame) : Loc.T(L.Games.DoomSetupTitle), theme.TextStrong,
            TextStyles.Title2);
        var cursorY = content.Min.Y + titleHeight + 6f * scale;
        if (failure is not null)
        {
            cursorY += Typography.DrawWrappedCentered(new Vector2(content.Center.X, cursorY),
                $"{Loc.T(L.Games.DoomFailed)}: {failure}", theme.Danger, TextStyles.Caption1, content.Width) + 6f * scale;
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
            if (GameHud.Button(center, new Vector2(column.Width, buttonHeight), label, Accent, theme))
            {
                TryStart(assets.PathFor(in iwad));
            }

            y += buttonHeight + gap;
        }

        if (assets.AvailableIwadCount == 0)
        {
            Typography.DrawWrappedCentered(new Vector2(column.Center.X, column.Min.Y), Loc.T(L.Games.DoomSetupBody),
                theme.TextMuted, TextStyles.Subheadline, column.Width);
        }
    }

    private void DrawInstallCards(Rect column, PhoneTheme theme, float scale)
    {
        var cardHeight = CardHeight * scale;
        var gap = 8f * scale;
        var y = column.Min.Y;
        var sharewareReady = assets.SharewareReady;
        var freedoomReady = assets.FreedoomReady;
        var soundfontReady = assets.SoundfontPath() is not null;
        if (!sharewareReady)
        {
            DrawCard(new Rect(new Vector2(column.Min.X, y), new Vector2(column.Max.X, y + cardHeight)), Loc.T(L.Games.DoomGameData),
                Loc.T(L.Games.DoomGameDataDetail), assets.Shareware, DoomAssets.SharewareDownloadBytes, theme, scale,
                () => assets.Install(true, false, !soundfontReady));
            y += cardHeight + gap;
        }

        if (!freedoomReady)
        {
            DrawCard(new Rect(new Vector2(column.Min.X, y), new Vector2(column.Max.X, y + cardHeight)), Loc.T(L.Games.DoomFreedoom),
                Loc.T(L.Games.DoomFreedoomDetail), assets.Freedoom, DoomAssets.FreedoomDownloadBytes, theme, scale,
                () => assets.Install(false, true, !soundfontReady));
            y += cardHeight + gap;
        }

        if (!soundfontReady)
        {
            DrawCard(new Rect(new Vector2(column.Min.X, y), new Vector2(column.Max.X, y + cardHeight)), Loc.T(L.Games.DoomMusic),
                Loc.T(L.Games.DoomMusicDetail), assets.Soundfont, DoomAssets.SoundfontDownloadBytes, theme, scale,
                () => assets.Install(false, false, true));
        }
    }

    private void DrawCard(Rect card, string title, string detail, MediaDependency dependency, long downloadBytes, PhoneTheme theme,
        float scale, Action install)
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
            Typography.FitText(title, right - left, TextStyles.BodyEmphasized), theme.TextStrong, TextStyles.BodyEmphasized);
        var detailY = card.Min.Y + pad * 0.7f + titleHeight;
        Typography.Draw(drawList, new Vector2(left, detailY), Typography.FitText(detail, right - left, TextStyles.Caption1),
            theme.TextMuted, TextStyles.Caption1);
        var statusY = detailY + Typography.LineHeight(TextStyles.Caption1) + 2f * scale;
        var statusColor = snapshot.State == DependencyState.Failed ? theme.Danger : theme.TextMuted;
        Typography.Draw(drawList, new Vector2(left, statusY),
            Typography.FitText(DependencySetup.StatusText(snapshot), right - left, TextStyles.Caption1), statusColor,
            TextStyles.Caption1);
        var busy = DependencySetup.IsBusy(snapshot);
        var label = busy
            ? Loc.T(L.AetherStream.SetupInstalling)
            : snapshot.State == DependencyState.Failed
                ? Loc.T(L.AetherStream.SetupRetry)
                : string.Format(Loc.T(L.AetherStream.SetupInstallSized), DependencySetup.FormatMegabytes(downloadBytes));
        var buttonCenter = new Vector2(card.Max.X - pad - buttonWidth * 0.5f, card.Center.Y);
        if (GameHud.Button(buttonCenter, new Vector2(buttonWidth, 34f * scale), label, Accent, theme) && !busy && !assets.Installing)
        {
            install();
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
            ImGui.GetColorU32(Accent), 1.5f * scale);
    }
}
