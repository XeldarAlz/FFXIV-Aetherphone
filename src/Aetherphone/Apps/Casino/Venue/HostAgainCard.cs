using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class HostAgainCard
{
    private const float Radius = 20f;
    private const float PadX = 14f;
    private const float PadY = 12f;
    private const float Tile = 44f;
    private const float TextGap = 12f;
    private const float LineGap = 2f;
    private const float PillGap = 10f;
    private const float WashAlpha = 0.16f;
    private const float WashFloorAlpha = 0.02f;
    private const float RimAlpha = 0.4f;
    private const float EyebrowLift = 0.35f;
    private const int SummaryLines = 2;

    private readonly string[] wrapped = new string[SummaryLines];

    private CasinoHostDraftMemory? summaryOf;
    private bool summaryGilOpen;
    private LanguageInfo? summaryLanguage;
    private string summary = string.Empty;
    private string gameId = CasinoGames.Blackjack;
    private string wrappedOf = string.Empty;
    private float wrappedWidth;
    private int wrappedCount;

    public bool Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, CasinoHostDraftMemory memory,
        bool gilOpen, bool enabled, float scale, out float bottom)
    {
        Refresh(memory, gilOpen);
        var pillLabel = Loc.T(L.HostDesk.OpenDraft);
        var pillHeight = Button.RegularHeight * scale;
        var pillWidth = DoorPill.WidthFor(pillLabel, pillHeight, scale);
        var textLeft = origin.X + (PadX + Tile + TextGap) * scale;
        var textWidth = MathF.Max(1f, origin.X + width - PadX * scale - pillWidth - PillGap * scale - textLeft);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var lineCount = Wrap(textWidth);
        var lineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var textBlock = eyebrowHeight + LineGap * scale + lineHeight * lineCount;
        var height = MathF.Max(Tile * scale, textBlock) + PadY * 2f * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Radius * scale;
        var pill = new Rect(new Vector2(card.Max.X - PadX * scale - pillWidth, card.Center.Y - pillHeight * 0.5f),
            new Vector2(card.Max.X - PadX * scale, card.Center.Y + pillHeight * 0.5f));
        var overPill = UiInteract.Hover(pill.Min, pill.Max);
        var hovered = enabled && !overPill && UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(ui.Accent with { W = WashAlpha }), ImGui.GetColorU32(ui.Accent with { W = WashFloorAlpha }));
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = RimAlpha }),
            1f * scale);
        CasinoArt.GameTile(drawList, gameId, new Vector2(origin.X + (PadX + Tile * 0.5f) * scale, card.Center.Y),
            Tile * scale);
        var top = card.Center.Y - textBlock * 0.5f;
        var eyebrowInk = Palette.Mix(ui.Accent, CasinoArt.White, EyebrowLift);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.HostDesk.HostAgainEyebrow), textWidth, TextStyles.FootnoteEmphasized),
            eyebrowInk, TextStyles.FootnoteEmphasized);
        top += eyebrowHeight + LineGap * scale;
        for (var index = 0; index < lineCount; index++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top), wrapped[index], ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            top += lineHeight;
        }

        var pressed = DoorPill.Draw(drawList, pill, pillLabel, ui.Ink, ButtonStyle.Prominent, null, enabled,
            "casino.host.again", scale);
        bottom = card.Max.Y;
        return pressed || (hovered && UiInteract.Click(card.Min, card.Max, hovered));
    }

    private int Wrap(float width)
    {
        if (ReferenceEquals(summary, wrappedOf) && MathF.Abs(width - wrappedWidth) < 0.5f)
        {
            return wrappedCount;
        }

        wrappedOf = summary;
        wrappedWidth = width;
        var lines = Typography.WrapText(summary, TextStyles.SubheadlineEmphasized, width);
        wrappedCount = Math.Min(lines.Length, SummaryLines);
        for (var index = 0; index < wrappedCount; index++)
        {
            wrapped[index] = lines[index];
        }

        if (lines.Length > SummaryLines)
        {
            var last = string.Concat(lines[SummaryLines - 1], " ", lines[SummaryLines]);
            wrapped[SummaryLines - 1] = Typography.FitText(last, width, TextStyles.SubheadlineEmphasized);
        }

        return wrappedCount;
    }

    private void Refresh(CasinoHostDraftMemory memory, bool gilOpen)
    {
        if (ReferenceEquals(memory, summaryOf) && gilOpen == summaryGilOpen
            && ReferenceEquals(summaryLanguage, Loc.Current))
        {
            return;
        }

        summaryOf = memory;
        summaryGilOpen = gilOpen;
        summaryLanguage = Loc.Current;
        summary = HostDraftSummary.Of(memory, gilOpen);
        gameId = HostGames.Of(HostDraft.GameOf(memory)).GameId;
    }
}
