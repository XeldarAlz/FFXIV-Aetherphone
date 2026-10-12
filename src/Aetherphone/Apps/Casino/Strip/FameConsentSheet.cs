using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class FameConsentSheet
{
    private const float PanelHeightShare = 0.86f;
    private const float HeroSize = 64f;
    private const float ButtonGap = 8f;
    private const float BodyMinHeight = 24f;
    private const float PreviewStrokeAlpha = 0.6f;
    private const int SampleRank = 3;
    private const long SampleValue = 12_480;
    private const string SampleGameKind = CasinoWire.PlinkoKind;
    private const string ShowId = "casino.fameConsent.show";
    private const string NotNowId = "casino.fameConsent.notNow";

    private readonly SheetSurface sheet = new("casino.fameConsent");
    private readonly CasinoFameChoiceStore choice;
    private readonly FailureSlot failure = new();
    private readonly Action<Rect> drawSheetBody;

    private AppSkin skin = null!;
    private UserDto? sampleUser;
    private CasinoFameEntryDto sampleEntry = new();
    private bool answered;
    private bool awaitingSave;

    public FameConsentSheet(CasinoFameChoiceStore choice)
    {
        this.choice = choice;
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open()
    {
        if (sheet.IsOpen)
        {
            return;
        }

        answered = false;
        awaitingSave = false;
        choice.ClearFailure();
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        Settle();
        var wasOpen = sheet.IsOpen;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Club.FameTitle), PanelHeightShare, drawSheetBody);
        if (wasOpen && !sheet.IsOpen && !answered)
        {
            choice.Decline();
        }
    }

    private void Settle()
    {
        if (!awaitingSave || choice.Saving)
        {
            return;
        }

        awaitingSave = false;
        if (choice.Failure.Failed)
        {
            answered = false;
            return;
        }

        sheet.Close();
    }

    private void DrawSheetBody(Rect content)
    {
        var scale = UiScale.Current;
        var failureText = FailureText();
        var gap = ButtonGap * scale;
        var buttonHeight = Button.LargeHeight * scale;
        var failureHeight = failureText.Length > 0
            ? Typography.MeasureWrappedBlock(failureText, TextStyles.Footnote, content.Width).Y + gap
            : 0f;
        var stackTop = content.Max.Y - buttonHeight * 2f - gap - failureHeight;
        var bodyHeight = MathF.Max(BodyMinHeight * scale, stackTop - Metrics.Space.Md * scale - content.Min.Y);
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##casinoFameConsentBody", new Vector2(content.Width, bodyHeight), false,
                   ImGuiWindowFlags.NoBackground))
        {
            DrawBody(scale);
        }

        DrawButtons(new Vector2(content.Min.X, stackTop), content.Width, failureText, scale);
    }

    private void DrawBody(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var left = origin.X;
        var center = left + width * 0.5f;
        var hero = HeroSize * scale;
        var y = origin.Y + Metrics.Space.Sm * scale;
        CasinoArt.IconTileAt(drawList, new Vector2(center, y + hero * 0.5f), hero, CasinoColors.Money,
            FontAwesomeIcon.Crown);
        y += hero + Metrics.Space.Lg * scale;
        y += Typography.DrawWrappedCentered(new Vector2(center, y), Loc.T(L.Club.FameConsentTitle), skin.TitleInk,
            TextStyles.Title2, width);
        y += Metrics.Space.Sm * scale;
        y += Typography.DrawWrappedCentered(new Vector2(center, y), Loc.T(L.Club.FameConsentBody), skin.BodyInk,
            TextStyles.Subheadline, width);
        y = DrawPreview(drawList, left, y + Metrics.Space.Xl * scale, width, scale) + Metrics.Space.Xl * scale;
        y = InfoLine.Draw(drawList, PhoneIcons.EyeOff, Loc.T(L.Club.FameConsentOff), skin.Accent, skin.BodyInk,
            left, y, width, scale) + Metrics.Space.Md * scale;
        y = InfoLine.Draw(drawList, PhoneIcons.Settings, Loc.T(L.Leaderboard.ConsentSettings), skin.Accent,
            skin.BodyInk, left, y, width, scale);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Metrics.Space.Md * scale));
    }

    private float DrawPreview(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.Leaderboard.ConsentPreview), width, TextStyles.FootnoteEmphasized),
            skin.MutedInk, TextStyles.FootnoteEmphasized);
        var min = new Vector2(left, top + Typography.LineHeight(TextStyles.FootnoteEmphasized)
                                     + Metrics.Space.Xs * scale);
        var max = new Vector2(left + width, min.Y + FameView.RowHeight * scale);
        var radius = Metrics.Radius.Grouped * scale;
        skin.Card(drawList, min, max, radius);
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = PreviewStrokeAlpha }), MathF.Max(1f, scale));
        var user = choice.CurrentUser;
        if (user is not null)
        {
            FameView.DrawEntry(drawList, skin, CasinoFameBoards.Win, SampleFor(user), new Rect(min, max), false,
                scale);
        }

        return max.Y;
    }

    private void DrawButtons(Vector2 topLeft, float width, string failureText, float scale)
    {
        var y = topLeft.Y;
        if (failureText.Length > 0)
        {
            y += Typography.DrawWrappedLeft(new Vector2(topLeft.X, y), failureText, skin.Theme.Danger,
                TextStyles.Footnote, width) + ButtonGap * scale;
        }

        var enabled = sheet.IsOpen && !choice.Saving;
        var height = Button.LargeHeight * scale;
        var show = new Rect(new Vector2(topLeft.X, y), new Vector2(topLeft.X + width, y + height));
        y += height + ButtonGap * scale;
        var notNow = new Rect(new Vector2(topLeft.X, y), new Vector2(topLeft.X + width, y + height));
        if (Button.Draw(show, Loc.T(L.Club.FameShowMe), skin.Ink, ButtonStyle.Prominent, enabled: enabled,
                overlay: true, id: ShowId))
        {
            answered = true;
            awaitingSave = true;
            choice.SetChoice(true);
        }

        if (!Button.Draw(notNow, Loc.T(L.Leaderboard.NotNow), skin.Ink, ButtonStyle.Gray, enabled: enabled,
                overlay: true, id: NotNowId))
        {
            return;
        }

        answered = true;
        choice.Decline();
        sheet.Close();
    }

    private CasinoFameEntryDto SampleFor(UserDto user)
    {
        if (ReferenceEquals(sampleUser, user))
        {
            return sampleEntry;
        }

        sampleUser = user;
        sampleEntry = new CasinoFameEntryDto(SampleRank,
            new CasinoPlayerRefDto(user.Id, user.DisplayName, user.Handle, user.AvatarUrl, user.Badges), SampleValue,
            SampleGameKind);
        return sampleEntry;
    }

    private string FailureText()
    {
        failure.Set(choice.Failure);
        return failure.Failed ? failure.Text() : string.Empty;
    }
}
