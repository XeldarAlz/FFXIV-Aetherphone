using System.Globalization;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const float SummaryHeight = 72f;
    private const float SummaryDiscRadius = 21f;
    private const float SummaryDiscFillAlpha = 0.18f;
    private const float SummaryDiscStroke = 1.6f;
    private const float SummaryTextGap = 12f;
    private const float SummaryLineGap = 2f;
    private const float SummaryGlowCoverage = 0.6f;
    private const float SummaryGlowStrength = 0.10f;
    private const float SummaryGlowStrengthHover = 0.16f;
    private const float RevealEpsilon = 0.002f;
    private const float RevealSettled = 0.998f;
    private const float StratRowHeight = 46f;
    private const float CheckScale = 0.7f;
    private const float CheckWidth = 22f;
    private const float BadgeGap = 4f;
    private const float BadgeLabelGap = 10f;
    private const float RoleChipHeight = 34f;
    private const float RoleChipGap = 6f;
    private const float RoleCaptionGap = 4f;
    private const float AlignmentHeight = 38f;
    private const float AlignmentPillHeight = 32f;
    private const float AlignmentLabelScale = 0.85f;
    private const float EditorSectionGap = 14f;
    private const string StratRowIdPrefix = "strats.strat:";
    private const string SummarySeparator = " · ";

    private static readonly string CheckGlyph = IconGlyph.Of(FontAwesomeIcon.Check);
    private static readonly TextStyle RoleCaptionStyle = TextStyles.FootnoteEmphasized;

    private readonly StringBuilder summaryBuilder = new();
    private bool setupOpen;
    private Spring setupReveal;
    private float setupHeight;
    private string[] stratLabels = Array.Empty<string>();
    private string[] stratRowIds = Array.Empty<string>();
    private string[][] stratBadgeTexts = Array.Empty<string[]>();
    private Vector4[][] stratBadgeInks = Array.Empty<Vector4[]>();
    private string[][] toggleLabels = Array.Empty<string[]>();
    private bool[][] toggleActive = Array.Empty<bool[]>();
    private string[] alignmentLabels = Array.Empty<string>();
    private string[] roleLabels = Array.Empty<string>();
    private bool roleLabelsJapanese;
    private string[] roleColumnRoles = Array.Empty<string>();
    private int[][] roleColumnSlots = Array.Empty<int[]>();
    private Vector4[] roleColumnInks = Array.Empty<Vector4>();
    private int roleRows;
    private int summaryRevision = -1;
    private CultureInfo? summaryCulture;
    private string summaryLine = string.Empty;
    private string roleSpotLabel = string.Empty;
    private string slotLabel = string.Empty;
    private Vector4 slotInk;

    private void BuildSetupLabels(FightDoc doc)
    {
        var count = doc.Strats.Length;
        stratLabels = new string[count];
        stratRowIds = new string[count];
        stratBadgeTexts = new string[count][];
        stratBadgeInks = new Vector4[count][];
        for (var index = 0; index < count; index++)
        {
            var strat = doc.Strats[index];
            stratLabels[index] = strat.Label;
            stratRowIds[index] = string.Concat(StratRowIdPrefix, index.ToString());
            var badges = strat.Badges;
            var texts = new string[badges.Length];
            var inks = new Vector4[badges.Length];
            for (var badgeIndex = 0; badgeIndex < badges.Length; badgeIndex++)
            {
                texts[badgeIndex] = badges[badgeIndex].Text;
                inks[badgeIndex] = BadgeInk(badges[badgeIndex].Kind);
            }

            stratBadgeTexts[index] = texts;
            stratBadgeInks[index] = inks;
        }

        toggleLabels = new string[doc.Toggles.Length][];
        toggleActive = new bool[doc.Toggles.Length][];
        for (var toggleIndex = 0; toggleIndex < doc.Toggles.Length; toggleIndex++)
        {
            var options = doc.Toggles[toggleIndex].Options;
            var labels = new string[options.Length];
            for (var optionIndex = 0; optionIndex < options.Length; optionIndex++)
            {
                labels[optionIndex] = options[optionIndex].Label;
            }

            toggleLabels[toggleIndex] = labels;
            toggleActive[toggleIndex] = new bool[options.Length];
        }

        alignmentLabels = new string[doc.Alignments.Length];
        for (var index = 0; index < doc.Alignments.Length; index++)
        {
            alignmentLabels[index] = doc.Alignments[index].Label;
        }

        roleLabels = Array.Empty<string>();
        summaryRevision = -1;
    }

    private Vector4 BadgeInk(string kind)
    {
        if (kind == "na" || kind.Contains("blue", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("blue", ui.BodyInk, ui.MutedInk);
        }

        if (kind == "eu" || kind.Contains("yellow", StringComparison.Ordinal) ||
            kind.Contains("amber", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("yellow", ui.BodyInk, ui.MutedInk);
        }

        if (kind == "oce" || kind.Contains("green", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("green", ui.BodyInk, ui.MutedInk);
        }

        if (kind == "jp" || kind.Contains("red", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk);
        }

        return ui.MutedInk;
    }

    private void EnsureRoleColumns(FightDoc doc, bool japanese)
    {
        if (roleLabels.Length > 0 && roleLabelsJapanese == japanese)
        {
            return;
        }

        roleLabelsJapanese = japanese;
        summaryRevision = -1;
        if (doc.RoleOptions.Length > 0)
        {
            roleLabels = new string[doc.RoleOptions.Length];
            var roles = new List<string>();
            var members = new List<List<int>>();
            for (var index = 0; index < doc.RoleOptions.Length; index++)
            {
                var option = doc.RoleOptions[index];
                roleLabels[index] = option.Label;
                var column = roles.IndexOf(option.Role);
                if (column < 0)
                {
                    roles.Add(option.Role);
                    members.Add(new List<int>());
                    column = roles.Count - 1;
                }

                members[column].Add(index);
            }

            roleColumnRoles = roles.ToArray();
            roleColumnSlots = new int[roles.Count][];
            for (var column = 0; column < roles.Count; column++)
            {
                roleColumnSlots[column] = members[column].ToArray();
            }
        }
        else
        {
            roleLabels = new string[StratsRoles.SlotCount];
            for (var slot = 0; slot < StratsRoles.SlotCount; slot++)
            {
                roleLabels[slot] = StratsRoles.Label(slot, japanese);
            }

            var columns = StratsRoles.SlotCount / 2;
            roleColumnRoles = new string[columns];
            roleColumnSlots = new int[columns][];
            for (var column = 0; column < columns; column++)
            {
                roleColumnRoles[column] = StratsRoles.RoleName(column * 2);
                roleColumnSlots[column] = new[] { column * 2, column * 2 + 1 };
            }
        }

        roleColumnInks = new Vector4[roleColumnRoles.Length];
        roleRows = 0;
        for (var column = 0; column < roleColumnRoles.Length; column++)
        {
            roleColumnInks[column] = RoleInk(roleColumnRoles[column]);
            roleRows = Math.Max(roleRows, roleColumnSlots[column].Length);
        }
    }

    private Vector4 RoleInk(string role) =>
        role switch
        {
            "Tank" => StratsInk.Resolve("blue", ui.BodyInk, ui.MutedInk),
            "Healer" => StratsInk.Resolve("green", ui.BodyInk, ui.MutedInk),
            "Melee" => StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk),
            "Ranged" => StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk),
            _ => ui.MutedInk,
        };

    private static string RoleCaption(string role) =>
        role switch
        {
            "Tank" => Loc.T(L.Strats.RoleTank),
            "Healer" => Loc.T(L.Strats.RoleHealer),
            "Melee" => Loc.T(L.Strats.RoleMelee),
            "Ranged" => Loc.T(L.Strats.RoleRanged),
            _ => role,
        };

    private int ActiveSlot => Math.Clamp(selection.Slot, 0, Math.Max(0, roleLabels.Length - 1));

    private string SlotRole(FightDoc doc, int slot) =>
        doc.RoleOptions.Length > 0
            ? doc.RoleOptions[Math.Clamp(slot, 0, doc.RoleOptions.Length - 1)].Role
            : StratsRoles.RoleName(slot);

    private void EnsureSummary(FightDoc doc, ResolvedFight current)
    {
        if (summaryRevision == current.Revision && ReferenceEquals(summaryCulture, Loc.Culture))
        {
            return;
        }

        summaryRevision = current.Revision;
        summaryCulture = Loc.Culture;
        var slot = ActiveSlot;
        var role = SlotRole(doc, slot);
        slotLabel = roleLabels.Length > 0 ? roleLabels[slot] : string.Empty;
        slotInk = RoleInk(role);
        roleSpotLabel = slotLabel.Length > 0 ? Loc.T(L.Strats.ForYouSlot, slotLabel) : Loc.T(L.Strats.ForYou);
        summaryBuilder.Clear();
        summaryBuilder.Append(RoleCaption(role));
        for (var toggleIndex = 0; toggleIndex < doc.Toggles.Length; toggleIndex++)
        {
            var optionIndex = current.ToggleOptionIndices[toggleIndex];
            if (!current.ToggleVisible[toggleIndex] || optionIndex < 0)
            {
                continue;
            }

            summaryBuilder.Append(SummarySeparator).Append(toggleLabels[toggleIndex][optionIndex]);
        }

        if (alignmentLabels.Length > 0)
        {
            summaryBuilder.Append(SummarySeparator).Append(alignmentLabels[current.AlignmentIndex]);
        }

        summaryLine = summaryBuilder.ToString();
    }

    private void DrawSetup(FightDoc doc, ResolvedFight current, float scale)
    {
        EnsureSummary(doc, current);
        DrawSetupSummary(current, scale);
        var reveal = setupReveal.Step(setupOpen ? 1f : 0f, Motion.Sheet, FrameDelta());
        if (!setupOpen && reveal < RevealEpsilon)
        {
            setupReveal.SnapTo(0f);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var clipped = reveal < RevealSettled;
        if (clipped)
        {
            var visible = MathF.Max(0f, setupHeight * reveal);
            ImGui.PushClipRect(new Vector2(origin.X - Metrics.Space.Sm * scale, origin.Y),
                new Vector2(origin.X + width + Metrics.Space.Sm * scale, origin.Y + visible), true);
        }

        DrawStratPicker(doc, current, scale);
        DrawRolePicker(scale);
        DrawToggles(doc, current, scale);
        DrawAlignment(doc, current, scale);
        var bottom = ImGui.GetCursorScreenPos().Y;
        setupHeight = MathF.Max(0f, bottom - origin.Y);
        if (!clipped)
        {
            return;
        }

        ImGui.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, setupHeight * reveal - ImGui.GetStyle().ItemSpacing.Y)));
    }

    private void DrawSetupSummary(ResolvedFight current, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + SummaryHeight * scale));
        UiAnchors.Report("strats.setup", rect);
        if (!setupOpen)
        {
            UiAnchors.Report("strats.strategy", rect);
            UiAnchors.Report("strats.role", rect);
        }

        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("strats.setup", pressed, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, radius, true);
        Material.TopGlow(drawList, min, max, radius, ui.Accent, SummaryGlowCoverage,
            hovered ? SummaryGlowStrengthHover : SummaryGlowStrength);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = CardPad * scale;
        var discRadius = SummaryDiscRadius * scale;
        var discCenter = new Vector2(min.X + pad + discRadius, (min.Y + max.Y) * 0.5f);
        drawList.AddCircleFilled(discCenter, discRadius,
            ImGui.GetColorU32(Palette.WithAlpha(slotInk, SummaryDiscFillAlpha)), 32);
        drawList.AddCircle(discCenter, discRadius - SummaryDiscStroke * scale * 0.5f, ImGui.GetColorU32(slotInk), 32,
            SummaryDiscStroke * scale);
        Typography.DrawCentered(drawList, discCenter,
            Typography.FitText(slotLabel, discRadius * 1.7f, TextStyles.SubheadlineEmphasized), slotInk,
            TextStyles.SubheadlineEmphasized);

        var action = setupOpen ? Loc.T(L.Strats.Done) : Loc.T(L.Strats.Edit);
        var actionWidth = Typography.Measure(action, TextStyles.Subheadline).X;
        var actionLeft = max.X - pad - actionWidth;
        Typography.Draw(drawList,
            new Vector2(actionLeft, discCenter.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f), action,
            ui.Accent, TextStyles.Subheadline);

        var textLeft = discCenter.X + discRadius + SummaryTextGap * scale;
        var textRight = actionLeft - SummaryTextGap * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var stratIndex = current.StratIndex;
        var badgesWidth = StratBadgesWidth(stratIndex, scale);
        var titleBudget = badgesWidth > 0f ? textWidth - badgesWidth - BadgeLabelGap * scale : textWidth;
        var showBadges = badgesWidth > 0f && titleBudget > textWidth * 0.4f;
        var title = Typography.FitText(stratLabels[stratIndex], showBadges ? titleBudget : textWidth,
            TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = discCenter.Y - (titleHeight + SummaryLineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), title, ui.TitleInk, TextStyles.Headline);
        if (showBadges)
        {
            var badgeLeft = textLeft + Typography.Measure(title, TextStyles.Headline).X + BadgeLabelGap * scale;
            DrawStratBadges(drawList, badgeLeft, top + titleHeight * 0.5f, stratIndex, scale);
        }

        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + SummaryLineGap * scale),
            Typography.FitText(summaryLine, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            setupOpen = !setupOpen;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width,
            (SummaryHeight + EditorSectionGap) * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawStratPicker(FightDoc doc, ResolvedFight current, float scale)
    {
        ui.SectionLabel(Loc.T(L.Strats.Strategy));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowCount = doc.Strats.Length;
        UiAnchors.Report("strats.strategy",
            new Rect(origin, new Vector2(origin.X + width, origin.Y + rowCount * StratRowHeight * scale)));
        var interactive = rowCount > 1;
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(ui, rowCount, StratRowHeight);
        for (var index = 0; index < rowCount; index++)
        {
            var row = card.NextRow();
            if (DrawStratRow(drawList, row, card.Bounds, index, index == current.StratIndex, interactive, scale) &&
                index != current.StratIndex)
            {
                selection.StratId = doc.Strats[index].Id;
                selection.Toggles.Clear();
                TouchSelection();
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, EditorSectionGap * scale - ImGui.GetStyle().ItemSpacing.Y * 2f));
    }

    private bool DrawStratRow(ImDrawListPtr drawList, Rect row, Rect bounds, int index, bool selected,
        bool interactive, float scale)
    {
        var hovered = interactive && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            drawList.AddRectFilled(new Vector2(bounds.Min.X, row.Min.Y), new Vector2(bounds.Max.X, row.Max.Y),
                ImGui.GetColorU32(ui.HoverWash));
        }

        var checkReserve = CheckWidth * scale;
        if (selected)
        {
            AppSkin.Icon(drawList, new Vector2(row.Max.X - checkReserve * 0.5f, row.Center.Y), CheckGlyph, ui.Accent,
                CheckScale);
        }

        var badgesWidth = StratBadgesWidth(index, scale);
        var badgesLeft = row.Max.X - checkReserve - badgesWidth;
        if (badgesWidth > 0f)
        {
            DrawStratBadges(drawList, badgesLeft, row.Center.Y, index, scale);
        }

        var labelRight = badgesWidth > 0f ? badgesLeft - BadgeLabelGap * scale : row.Max.X - checkReserve;
        var style = selected ? TextStyles.BodyEmphasized : TextStyles.Body;
        Marquee.DrawLeft(drawList, stratRowIds[index], stratLabels[index], row.Min.X,
            row.Center.Y - Typography.LineHeight(style) * 0.5f, MathF.Max(1f, labelRight - row.Min.X), style,
            selected ? ui.TitleInk : ui.BodyInk, hovered);
        return interactive && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float StratBadgesWidth(int stratIndex, float scale)
    {
        var texts = stratBadgeTexts[stratIndex];
        var total = 0f;
        for (var index = 0; index < texts.Length; index++)
        {
            total += InlineBadge.Width(texts[index], scale) + (index > 0 ? BadgeGap * scale : 0f);
        }

        return total;
    }

    private void DrawStratBadges(ImDrawListPtr drawList, float left, float centerY, int stratIndex, float scale)
    {
        var texts = stratBadgeTexts[stratIndex];
        var inks = stratBadgeInks[stratIndex];
        for (var index = 0; index < texts.Length; index++)
        {
            left += InlineBadge.Draw(drawList, left, centerY, texts[index], inks[index], scale) + BadgeGap * scale;
        }
    }

    private void DrawRolePicker(float scale)
    {
        ui.SectionLabel(Loc.T(L.Strats.Role));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var columnGap = Metrics.Space.Sm * scale;
        var chipGap = RoleChipGap * scale;
        var columns = roleColumnSlots.Length;
        if (columns == 0)
        {
            return;
        }

        var columnWidth = (width - columnGap * (columns - 1)) / columns;
        var chipHeight = RoleChipHeight * scale;
        var captionLineHeight = Typography.LineHeight(RoleCaptionStyle);
        var captionHeight = captionLineHeight + RoleCaptionGap * scale;
        var gridHeight = captionHeight + roleRows * chipHeight + (roleRows - 1) * chipGap;
        UiAnchors.Report("strats.role", new Rect(origin, new Vector2(origin.X + width, origin.Y + gridHeight)));
        var activeSlot = ActiveSlot;
        for (var column = 0; column < columns; column++)
        {
            var left = origin.X + column * (columnWidth + columnGap);
            var caption = Typography.FitText(RoleCaption(roleColumnRoles[column]), columnWidth, RoleCaptionStyle);
            Typography.DrawCentered(drawList,
                new Vector2(left + columnWidth * 0.5f, origin.Y + captionLineHeight * 0.5f), caption,
                roleColumnInks[column], RoleCaptionStyle.Scale, RoleCaptionStyle.Weight);
            var slots = roleColumnSlots[column];
            for (var row = 0; row < slots.Length; row++)
            {
                var top = origin.Y + captionHeight + row * (chipHeight + chipGap);
                var rect = new Rect(new Vector2(left, top), new Vector2(left + columnWidth, top + chipHeight));
                var slot = slots[row];
                if (ui.Chip(rect, roleLabels[slot], slot == activeSlot) && slot != activeSlot)
                {
                    selection.Slot = slot;
                    TouchSelection();
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, gridHeight + EditorSectionGap * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawToggles(FightDoc doc, ResolvedFight current, float scale)
    {
        for (var toggleIndex = 0; toggleIndex < doc.Toggles.Length; toggleIndex++)
        {
            if (!current.ToggleVisible[toggleIndex])
            {
                continue;
            }

            var toggle = doc.Toggles[toggleIndex];
            var active = toggleActive[toggleIndex];
            var selected = current.ToggleOptionIndices[toggleIndex];
            for (var index = 0; index < active.Length; index++)
            {
                active[index] = index == selected;
            }

            ui.SectionLabel(toggle.Label);
            if (!toggleRails.TryGetValue(toggle.Key, out var rail))
            {
                rail = new ChipRail();
                toggleRails[toggle.Key] = rail;
            }

            var tapped = rail.Draw(ui, toggleLabels[toggleIndex], active);
            if (tapped >= 0 && tapped != selected)
            {
                selection.Toggles[toggle.Key] = toggle.Options[tapped].Value;
                TouchSelection();
            }

            ImGui.Dummy(new Vector2(0f, EditorSectionGap * scale - ImGui.GetStyle().ItemSpacing.Y * 2f));
        }
    }

    private void DrawAlignment(FightDoc doc, ResolvedFight current, float scale)
    {
        if (doc.Alignments.Length == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Strats.Orientation));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + AlignmentHeight * scale));
        var picked = SegmentStrip.Draw("strats.alignment", row, alignmentLabels, current.AlignmentIndex,
            AppPalettes.Strats, AlignmentPillHeight, AlignmentLabelScale);
        if (picked != current.AlignmentIndex)
        {
            selection.Alignment = doc.Alignments[picked].Id;
            TouchSelection();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width,
            (AlignmentHeight + EditorSectionGap) * scale - ImGui.GetStyle().ItemSpacing.Y));
    }
}
