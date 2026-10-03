using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const ImGuiWindowFlags SheetHostFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                    ImGuiWindowFlags.NoBackground;

    private const int ResumeSettleFrames = 3;
    private const int ContentsScrollFrames = 2;
    private const float ReadingLineGap = 12f;
    private const float GlideRestPosition = 0.5f;
    private const float GlideRestVelocity = 2f;
    private const float ContentsHeaderHeight = 54f;
    private const float ContentsPhaseHeight = 38f;
    private const float ContentsRowHeight = 44f;
    private const float ContentsPadX = 20f;
    private const float ContentsIndent = 14f;
    private const float ContentsMarkerWidth = 3f;
    private const float ContentsMarkerHeight = 18f;
    private const float ContentsSpotSize = 15f;
    private const float ContentsSpotGap = 10f;
    private const float ContentsHoverAlpha = 0.07f;
    private const float ContentsHairlineAlpha = 0.10f;
    private const float ContentsMutedAlpha = 0.62f;
    private const float ContentsLeadShare = 0.3f;

    private readonly Sheet contentsSheet = new();
    private StratsContentsEntry[] contents = Array.Empty<StratsContentsEntry>();
    private int contentsForYou;
    private float[] entryTops = Array.Empty<float>();
    private int entryCursor;
    private int currentEntry = -1;
    private int pendingResume = -1;
    private int resumeFrames;
    private int contentsScrollPending;
    private bool gliding;
    private float glideTarget;
    private float lastScrollY;
    private Spring glide;

    private static float ReadingLineOffset(float scale) => (NavBarMetrics.InlineHeight + ReadingLineGap) * scale;

    private void PrepareReading(int resumeEntry)
    {
        pendingResume = resumeEntry;
        resumeFrames = 0;
        currentEntry = -1;
        gliding = false;
        lastScrollY = 0f;
        contents = Array.Empty<StratsContentsEntry>();
        entryTops = Array.Empty<float>();
        entryCursor = 0;
    }

    private void RebuildContents(ResolvedFight current, ResolvedFight? previous)
    {
        contents = StratsContents.Build(current);
        contentsForYou = StratsContents.CountForYou(contents);
        entryTops = new float[contents.Length];
        entryCursor = 0;
        var moved = previous is not null &&
                    (previous.StratIndex != current.StratIndex || previous.TabIndex != current.TabIndex);
        if (moved)
        {
            selection.ClearReading();
            pendingResume = -1;
            currentEntry = -1;
            gliding = false;
            return;
        }

        if (pendingResume >= contents.Length)
        {
            pendingResume = -1;
        }
    }

    private void MarkEntry()
    {
        if (entryCursor < entryTops.Length)
        {
            entryTops[entryCursor] = ImGui.GetCursorPosY();
        }

        entryCursor++;
    }

    private void TrackReading(in AppSurface.SurfaceScope surface, float scale)
    {
        var recorded = entryCursor;
        entryCursor = 0;
        if (recorded != entryTops.Length || entryTops.Length == 0)
        {
            return;
        }

        var offset = ReadingLineOffset(scale);
        if (pendingResume >= 0)
        {
            resumeFrames++;
            if (resumeFrames < ResumeSettleFrames)
            {
                return;
            }

            surface.JumpTo(MathF.Max(0f, entryTops[pendingResume] - offset));
            pendingResume = -1;
            return;
        }

        StepGlide(in surface);
        var scrollY = ImGui.GetScrollY();
        lastScrollY = scrollY;
        var line = scrollY + offset + scale;
        var entry = line < entryTops[0] ? -1 : StratsContents.IndexAt(entryTops, line);
        currentEntry = entry;
        var label = entry >= 0 ? contents[entry].Label : string.Empty;
        if (selection.MarkReading(entry, label, StratsContents.Progress(scrollY, ImGui.GetScrollMaxY())))
        {
            selectionDirty = true;
        }
    }

    private void StepGlide(in AppSurface.SurfaceScope surface)
    {
        if (!gliding)
        {
            return;
        }

        if (surface.Dragging || ImGui.GetIO().MouseWheel != 0f || PressedOnSurface())
        {
            gliding = false;
            return;
        }

        var target = Math.Clamp(glideTarget, 0f, ImGui.GetScrollMaxY());
        var value = glide.Step(target, Motion.Sheet, FrameDelta());
        surface.JumpTo(value);
        if (glide.IsResting(target, GlideRestPosition, GlideRestVelocity))
        {
            surface.JumpTo(target);
            gliding = false;
        }
    }

    private static bool PressedOnSurface()
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        var min = ImGui.GetWindowPos();
        return UiInteract.Hover(min, min + ImGui.GetWindowSize());
    }

    private void GlideTo(int entry)
    {
        if (entry < 0 || entry >= entryTops.Length)
        {
            return;
        }

        glide.SnapTo(lastScrollY);
        glideTarget = MathF.Max(0f, entryTops[entry] - ReadingLineOffset(UiScale.Current));
        gliding = true;
    }

    private void OpenContents()
    {
        if (contents.Length == 0)
        {
            return;
        }

        contentsScrollPending = ContentsScrollFrames;
        contentsSheet.Open();
    }

    private void DrawContentsSheet(Rect screen)
    {
        if (!contentsSheet.CapturesPointer)
        {
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##stratsContents", screen.Size, false, SheetHostFlags))
        {
            var frame = contentsSheet.Begin(ImGui.GetWindowDrawList(), screen, theme,
                SheetDetents.Standard(screen.Height), SheetMetrics.AppVeil);
            if (!frame.Visible)
            {
                return;
            }

            DrawContentsBody(in frame);
            contentsSheet.End(in frame);
        }
    }

    private void DrawContentsBody(in SheetFrame frame)
    {
        var scale = UiScale.Current;
        var content = frame.Content;
        var drawList = frame.DrawList;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var muted = Palette.WithAlpha(ink, ink.W * ContentsMutedAlpha);
        var accent = Palette.WithAlpha(ui.Accent, ui.Accent.W * frame.Opacity);
        var headerHeight = ContentsHeaderHeight * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitle = SheetSubtitle();
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = content.Min.Y + (headerHeight - titleHeight - subtitleHeight) * 0.5f;
        var textWidth = content.Width - ContentsPadX * 2f * scale;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, top + titleHeight * 0.5f),
            Loc.T(L.Strats.Contents), ink, TextStyles.Headline);
        if (subtitle.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(content.Center.X, top + titleHeight + subtitleHeight * 0.5f),
                Typography.FitText(subtitle, textWidth, TextStyles.Footnote), muted, TextStyles.Footnote);
        }

        var list = new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        if (list.Height <= 0f)
        {
            return;
        }

        drawList.AddLine(new Vector2(list.Min.X + ContentsPadX * scale, list.Min.Y),
            new Vector2(list.Max.X - ContentsPadX * scale, list.Min.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ink, ContentsHairlineAlpha * frame.Opacity)), Metrics.Stroke.Hairline);
        var picked = -1;
        using (ImRaii.PushId("strats.contents"))
        using (var surface = AppSurface.BeginEdgeToEdge(list))
        {
            if (contentsScrollPending > 0)
            {
                contentsScrollPending--;
                if (contentsScrollPending == 0 && currentEntry > 0)
                {
                    surface.JumpTo(MathF.Max(0f, RowTop(currentEntry, scale) - list.Height * ContentsLeadShare));
                }
            }

            var rowsDrawList = ImGui.GetWindowDrawList();
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                for (var index = 0; index < contents.Length; index++)
                {
                    if (DrawContentsRow(rowsDrawList, index, ink, muted, accent, frame.Interactive, scale))
                    {
                        picked = index;
                    }
                }

                DrawContentsLegend(rowsDrawList, muted, accent, scale);
            }
        }

        if (picked < 0)
        {
            return;
        }

        contentsSheet.Close();
        GlideTo(picked);
    }

    private void DrawContentsLegend(ImDrawListPtr drawList, Vector4 muted, Vector4 accent, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (contentsForYou == 0)
        {
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
            return;
        }

        var padX = ContentsPadX * scale;
        var top = origin.Y + Metrics.Space.Lg * scale;
        var glyphSize = ContentsSpotSize * scale;
        var textLeft = origin.X + padX + glyphSize + Metrics.Space.Sm * scale;
        var textWidth = MathF.Max(1f, origin.X + width - padX - textLeft);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        PhoneIcon.Draw(drawList, new Vector2(origin.X + padX + glyphSize * 0.5f, top + lineHeight * 0.5f),
            PhoneIcons.User, accent, glyphSize);
        var textHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, top), Loc.T(L.Strats.ContentsLegend), muted,
            TextStyles.Footnote, textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale * 2f + textHeight));
    }

    private string SheetSubtitle()
    {
        var current = resolved;
        if (current is null || current.Doc.Tabs.Length == 0)
        {
            return string.Empty;
        }

        return tabLabels.Length > current.TabIndex ? tabLabels[current.TabIndex] : string.Empty;
    }

    private float RowTop(int entry, float scale)
    {
        var top = 0f;
        for (var index = 0; index < entry && index < contents.Length; index++)
        {
            top += (contents[index].IsPhase ? ContentsPhaseHeight : ContentsRowHeight) * scale;
        }

        return top;
    }

    private bool DrawContentsRow(ImDrawListPtr drawList, int index, Vector4 ink, Vector4 muted, Vector4 accent,
        bool interactive, float scale)
    {
        var entry = contents[index];
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = (entry.IsPhase ? ContentsPhaseHeight : ContentsRowHeight) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ImGui.Dummy(new Vector2(width, height));
        if (!ImGui.IsRectVisible(origin, max))
        {
            return false;
        }

        var hovered = interactive && UiInteract.HoverWindowOnly(origin, max);
        var padX = ContentsPadX * scale;
        var inset = new Vector2(Metrics.Space.Sm * scale, 0f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            Squircle.Fill(drawList, origin + inset, max - inset, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ink, ContentsHoverAlpha)));
        }

        var isCurrent = index == currentEntry;
        var centerY = origin.Y + height * 0.5f;
        var left = origin.X + padX + (entry.IsPhase ? 0f : ContentsIndent * scale);
        var right = max.X - padX;
        if (isCurrent)
        {
            var markerHalf = ContentsMarkerHeight * scale * 0.5f;
            var markerLeft = origin.X + padX - ContentsMarkerWidth * scale - Metrics.Space.Xs * scale;
            Squircle.Fill(drawList, new Vector2(markerLeft, centerY - markerHalf),
                new Vector2(markerLeft + ContentsMarkerWidth * scale, centerY + markerHalf),
                ContentsMarkerWidth * scale * 0.5f, ImGui.GetColorU32(accent));
        }

        if (entry.ForYou)
        {
            var spotCenter = new Vector2(right - ContentsSpotSize * scale * 0.5f, centerY);
            PhoneIcon.Draw(drawList, spotCenter, PhoneIcons.User, accent, ContentsSpotSize * scale);
            right -= (ContentsSpotSize + ContentsSpotGap) * scale;
        }

        var style = entry.IsPhase
            ? TextStyles.FootnoteEmphasized
            : isCurrent
                ? TextStyles.BodyEmphasized
                : TextStyles.Body;
        var labelInk = isCurrent ? accent : entry.IsPhase ? muted : ink;
        var label = Typography.FitText(entry.Label, MathF.Max(1f, right - left), style);
        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(style) * 0.5f), label, labelInk,
            style);
        return interactive && UiInteract.Click(origin, max, hovered);
    }
}
