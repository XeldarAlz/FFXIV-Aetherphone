using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal struct ChatMenuModel
{
    public AppSkin Ui;
    public bool ShowReactions;
    public bool CanReply;
    public bool CanForward;
    public bool CanCopy;
    public bool CanStar;
    public bool CanEdit;
    public bool CanInfo;
    public bool CanDelete;
    public bool CanReport;
    public bool CanTranslate;
    public Func<string, bool> IsStarred;
    public Func<string, string> MyReactionTo;
    public Action<string> OnReply;
    public Action<string> OnForward;
    public Action<string> OnCopy;
    public Action<string> OnStar;
    public Action<string> OnEdit;
    public Action<string> OnInfo;
    public Action<string> OnDelete;
    public Action<string> OnReport;
    public Action<string> OnTranslate;
    public Action<string, string> OnReact;
}

internal sealed class ChatMenuController
{
    private const int TextKind = 0;
    private const byte ActReply = 0;
    private const byte ActForward = 1;
    private const byte ActCopy = 2;
    private const byte ActStar = 3;
    private const byte ActEdit = 4;
    private const byte ActInfo = 5;
    private const byte ActDelete = 6;
    private const byte ActReport = 7;
    private const byte ActTranslate = 8;

    private const float StripSlot = 42f;
    private const float StripPad = 8f;
    private const float StripHeight = 50f;
    private const float StripEmoji = 30f;
    private const float StripEmojiHoverGrow = 1.12f;
    private const float StripHalo = 18f;
    private const float StripPlusGlyph = 22f;
    private const float StripFallbackScale = 1f;
    private const float StripFallbackHoverScale = 1.12f;
    private const float SheetHeightRatio = 0.62f;
    private const float SheetCapHeight = 22f;
    private const float SheetRounding = 18f;
    private const float SheetHandleWidth = 40f;
    private const float SheetHandleHeight = 4f;
    private static readonly Vector4 SheetScrim = new(0f, 0f, 0f, 0.5f);
    private static readonly Vector4 SheetHandle = new(1f, 1f, 1f, 0.3f);

    private readonly DropdownMenu menu = new();
    private readonly DropdownMenu.Item[] items = new DropdownMenu.Item[8];
    private readonly byte[] actions = new byte[8];
    private readonly EmojiPicker reactionPicker = new();
    private string? messageId;
    private bool mine;
    private int kind;
    private Vector2 anchor;
    private bool openPending;
    private string? sheetTargetId;
    private int sheetOpenedFrame = -1;

    public bool Active => menu.Open || openPending || sheetTargetId is not null;

    public void Open(string messageId, bool mine, int kind)
    {
        this.messageId = messageId;
        this.mine = mine;
        this.kind = kind;
        anchor = ImGui.GetMousePos();
        openPending = true;
    }

    public void Gate()
    {
        menu.Gate();
    }

    public void Close()
    {
        menu.Close();
        sheetTargetId = null;
    }

    public void Draw(Rect area, in ChatMenuModel model)
    {
        if (sheetTargetId is { } sheetId)
        {
            DrawReactionSheet(area, sheetId, model);
            return;
        }

        if (openPending && messageId is { } pendingId)
        {
            openPending = false;
            menu.Toggle(pendingId, ReactionStripRect(area, model.ShowReactions));
        }

        if (messageId is not { } id || !menu.IsOpenFor(id))
        {
            return;
        }

        if (model.ShowReactions)
        {
            DrawReactionStrip(ReactionStripRect(area, true), id, model);
        }

        var count = 0;
        if (model.CanReply)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.ReplyAction), IconGlyph.Of(FontAwesomeIcon.Reply));
            actions[count++] = ActReply;
        }

        if (model.CanForward)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.ForwardAction), IconGlyph.Of(FontAwesomeIcon.Share));
            actions[count++] = ActForward;
        }

        if (model.CanCopy)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Encryption.CopyTextAction), IconGlyph.Of(FontAwesomeIcon.Copy));
            actions[count++] = ActCopy;
        }

        if (model.CanTranslate && !mine && kind == TextKind)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Translate.Action), IconGlyph.Of(FontAwesomeIcon.Language));
            actions[count++] = ActTranslate;
        }

        if (model.CanStar)
        {
            items[count] = new DropdownMenu.Item(
                Loc.T(model.IsStarred(id) ? L.Message.UnstarAction : L.Message.StarAction),
                IconGlyph.Of(FontAwesomeIcon.Star));
            actions[count++] = ActStar;
        }

        if (model.CanEdit && mine && kind == TextKind)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.EditAction), IconGlyph.Of(FontAwesomeIcon.Pen));
            actions[count++] = ActEdit;
        }

        if (model.CanInfo && mine)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.InfoAction), IconGlyph.Of(FontAwesomeIcon.InfoCircle));
            actions[count++] = ActInfo;
        }

        if (model.CanDelete && mine)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.DeleteAction), IconGlyph.Of(FontAwesomeIcon.TrashAlt),
                Danger: true);
            actions[count++] = ActDelete;
        }

        if (model.CanReport && !mine)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Encryption.ReportMessageAction),
                IconGlyph.Of(FontAwesomeIcon.Flag), Danger: true);
            actions[count++] = ActReport;
        }

        var clicked = menu.Draw(area, model.Ui.Theme, items.AsSpan(0, count));
        if (clicked < 0)
        {
            return;
        }

        switch (actions[clicked])
        {
            case ActReply:
                model.OnReply(id);
                break;
            case ActForward:
                model.OnForward(id);
                break;
            case ActCopy:
                model.OnCopy(id);
                break;
            case ActStar:
                model.OnStar(id);
                break;
            case ActEdit:
                model.OnEdit(id);
                break;
            case ActInfo:
                model.OnInfo(id);
                break;
            case ActDelete:
                model.OnDelete(id);
                break;
            case ActReport:
                model.OnReport(id);
                break;
            case ActTranslate:
                model.OnTranslate(id);
                break;
        }
    }

    private Rect ReactionStripRect(Rect area, bool showReactions)
    {
        if (!showReactions)
        {
            return new Rect(anchor, anchor + new Vector2(1f, 1f));
        }

        var scale = UiScale.Current;
        var slot = StripSlot * scale;
        var padding = StripPad * scale;
        var width = (ReactionArt.Tokens.Length + 1) * slot + padding * 2f;
        var height = StripHeight * scale;
        var left = Math.Clamp(anchor.X - width * 0.5f, area.Min.X + 8f * scale,
            MathF.Max(area.Min.X + 8f * scale, area.Max.X - 8f * scale - width));
        var top = anchor.Y - height - 10f * scale;
        if (top < area.Min.Y + 8f * scale)
        {
            top = anchor.Y + 10f * scale;
        }

        var min = new Vector2(left, top);
        return new Rect(min, min + new Vector2(width, height));
    }

    private void DrawReactionStrip(Rect strip, string targetId, in ChatMenuModel model)
    {
        var scale = UiScale.Current;
        var theme = model.Ui.Theme;
        var drawList = ImGui.GetForegroundDrawList();
        var slot = StripSlot * scale;
        var padding = StripPad * scale;
        var height = strip.Height;
        var min = strip.Min;
        var max = strip.Max;
        Elevation.Floating(drawList, min, max, height * 0.5f, scale);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, MathF.Min(0.98f, theme.GroupedCard.W + 0.4f))));
        Material.EdgeSquircle(drawList, min, max, height * 0.5f, scale);
        var myReaction = model.MyReactionTo(targetId);
        var centerY = (min.Y + max.Y) * 0.5f;
        var halo = StripHalo * scale;
        var tokens = ReactionArt.Tokens;
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            var center = new Vector2(min.X + padding + slot * (index + 0.5f), centerY);
            var hitMin = new Vector2(center.X - slot * 0.5f, min.Y);
            var hitMax = new Vector2(center.X + slot * 0.5f, max.Y);
            var hovered = UiInteract.HoverWindowOnly(hitMin, hitMax);
            var selected = ReactionArt.Same(token, myReaction);
            if (selected)
            {
                drawList.AddCircleFilled(center, halo,
                    ImGui.GetColorU32(Palette.WithAlpha(model.Ui.Accent, 0.25f)), 24);
            }
            else if (hovered)
            {
                drawList.AddCircleFilled(center, halo,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)), 24);
            }

            var emojiSize = StripEmoji * scale * (hovered ? StripEmojiHoverGrow : 1f);
            ReactionArt.Draw(drawList, token, center, emojiSize, 1f,
                hovered ? StripFallbackHoverScale : StripFallbackScale);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                model.OnReact(targetId, selected ? string.Empty : ReactionArt.Normalize(token));
                menu.Close();
            }
        }

        var plusCenter = new Vector2(min.X + padding + slot * (tokens.Length + 0.5f), centerY);
        var plusMin = new Vector2(plusCenter.X - slot * 0.5f, min.Y);
        var plusMax = new Vector2(plusCenter.X + slot * 0.5f, max.Y);
        var plusHovered = UiInteract.HoverWindowOnly(plusMin, plusMax);
        if (plusHovered)
        {
            drawList.AddCircleFilled(plusCenter, halo,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, plusCenter, PhoneIcons.Plus, theme.TextStrong, StripPlusGlyph * scale);
        HoverTooltip.Show(new Rect(plusMin, plusMax), Loc.T(L.Message.ReactionMore), HoverLabelSide.Above);
        if (plusHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            sheetTargetId = targetId;
            sheetOpenedFrame = ImGui.GetFrameCount();
            menu.Close();
        }
    }

    private void DrawReactionSheet(Rect area, string targetId, in ChatMenuModel model)
    {
        var scale = UiScale.Current;
        var theme = model.Ui.Theme;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(SheetScrim));
        var sheetTop = area.Max.Y - area.Height * SheetHeightRatio;
        var capHeight = SheetCapHeight * scale;
        var background = theme.AppBackground;
        Squircle.Fill(drawList, new Vector2(area.Min.X, sheetTop),
            new Vector2(area.Max.X, sheetTop + capHeight * 2f), SheetRounding * scale,
            ImGui.GetColorU32(new Vector4(background.X, background.Y, background.Z, 1f)));
        var handleHalf = new Vector2(SheetHandleWidth * 0.5f * scale, SheetHandleHeight * 0.5f * scale);
        var handleCenter = new Vector2(area.Center.X, sheetTop + capHeight * 0.5f);
        Squircle.Fill(drawList, handleCenter - handleHalf, handleCenter + handleHalf, handleHalf.Y,
            ImGui.GetColorU32(SheetHandle));
        var panel = new Rect(new Vector2(area.Min.X, sheetTop + capHeight), area.Max);
        string? picked;
        using (ImRaii.PushId("reactionSheet"))
        {
            picked = reactionPicker.Draw(panel, model.Ui);
        }

        UiInteract.HoverOverlay(area);
        if (picked is not null)
        {
            model.OnReact(targetId, ReactionArt.Normalize(picked));
            sheetTargetId = null;
            return;
        }

        var frame = ImGui.GetFrameCount();
        if (frame == sheetOpenedFrame)
        {
            return;
        }

        var sheetMin = new Vector2(area.Min.X, sheetTop);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || UiInteract.ClickedOutside(sheetMin, area.Max))
        {
            sheetTargetId = null;
        }
    }
}
