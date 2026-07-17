using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

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

    private readonly DropdownMenu menu = new();
    private readonly DropdownMenu.Item[] items = new DropdownMenu.Item[7];
    private readonly byte[] actions = new byte[7];
    private string? messageId;
    private bool mine;
    private int kind;
    private Vector2 anchor;
    private bool openPending;

    public bool Active => menu.Open || openPending;

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
    }

    public void Draw(Rect area, in ChatMenuModel model)
    {
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
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.ReplyAction), FontAwesomeIcon.Reply.ToIconString());
            actions[count++] = ActReply;
        }

        if (model.CanForward)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.ForwardAction), FontAwesomeIcon.Share.ToIconString());
            actions[count++] = ActForward;
        }

        if (model.CanCopy)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Encryption.CopyTextAction), FontAwesomeIcon.Copy.ToIconString());
            actions[count++] = ActCopy;
        }

        if (model.CanStar)
        {
            items[count] = new DropdownMenu.Item(
                Loc.T(model.IsStarred(id) ? L.Message.UnstarAction : L.Message.StarAction),
                FontAwesomeIcon.Star.ToIconString());
            actions[count++] = ActStar;
        }

        if (model.CanEdit && mine && kind == TextKind)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.EditAction), FontAwesomeIcon.Pen.ToIconString());
            actions[count++] = ActEdit;
        }

        if (model.CanInfo && mine)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.InfoAction), FontAwesomeIcon.InfoCircle.ToIconString());
            actions[count++] = ActInfo;
        }

        if (model.CanDelete && mine)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Message.DeleteAction), FontAwesomeIcon.TrashAlt.ToIconString(),
                Danger: true);
            actions[count++] = ActDelete;
        }

        if (model.CanReport && !mine)
        {
            items[count] = new DropdownMenu.Item(Loc.T(L.Encryption.ReportMessageAction),
                FontAwesomeIcon.Flag.ToIconString(), Danger: true);
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
        }
    }

    private Rect ReactionStripRect(Rect area, bool showReactions)
    {
        if (!showReactions)
        {
            return new Rect(anchor, anchor + new Vector2(1f, 1f));
        }

        var scale = ImGuiHelpers.GlobalScale;
        var slot = 34f * scale;
        var padding = 7f * scale;
        var width = ReactionArt.Tokens.Length * slot + padding * 2f;
        var height = 38f * scale;
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
        var scale = ImGuiHelpers.GlobalScale;
        var theme = model.Ui.Theme;
        var drawList = ImGui.GetForegroundDrawList();
        var slot = 34f * scale;
        var padding = 7f * scale;
        var height = strip.Height;
        var min = strip.Min;
        var max = strip.Max;
        Elevation.Floating(drawList, min, max, height * 0.5f, scale);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, MathF.Min(0.98f, theme.GroupedCard.W + 0.4f))));
        Material.EdgeSquircle(drawList, min, max, height * 0.5f, scale);
        var myReaction = model.MyReactionTo(targetId);
        for (var index = 0; index < ReactionArt.Tokens.Length; index++)
        {
            var token = ReactionArt.Tokens[index];
            var center = new Vector2(min.X + padding + slot * (index + 0.5f), (min.Y + max.Y) * 0.5f);
            var hitMin = new Vector2(center.X - slot * 0.5f, min.Y);
            var hitMax = new Vector2(center.X + slot * 0.5f, max.Y);
            var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
            if (token == myReaction)
            {
                drawList.AddCircleFilled(center, 14f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(model.Ui.Accent, 0.25f)), 24);
            }
            else if (hovered)
            {
                drawList.AddCircleFilled(center, 14f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)), 24);
            }

            var color = ReactionArt.Color(token);
            AppSkin.Icon(drawList, center, ReactionArt.Glyph(token), color, hovered ? 1.08f : 0.95f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnReact(targetId, token == myReaction ? string.Empty : token);
                    menu.Close();
                }
            }
        }
    }
}
