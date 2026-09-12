using Aetherphone.Core;
using Aetherphone.Core.Contacts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal static class FriendLabels
{
    private sealed class Entry
    {
        public string First = string.Empty;
        public string Second = string.Empty;
        public string Text = string.Empty;
    }

    private const string Separator = " · ";

    private static readonly Dictionary<ulong, Entry> Subtitles = new();
    private static readonly Dictionary<ulong, Entry> Metas = new();
    private static readonly Dictionary<ulong, Entry> Presences = new();

    public static string RowSubtitle(FriendEntry friend)
    {
        if (!friend.Online)
        {
            return Loc.T(L.Contacts.Offline);
        }

        return Joined(Subtitles, friend.ContentId, friend.JobName, friend.Location);
    }

    public static string HeroMeta(FriendEntry friend) =>
        Joined(Metas, friend.ContentId, friend.WorldName, friend.Online ? friend.JobName : string.Empty);

    public static string Presence(FriendEntry friend)
    {
        if (!friend.Online)
        {
            return Loc.T(L.Contacts.Offline);
        }

        return Joined(Presences, friend.ContentId, Loc.T(L.Contacts.Online), friend.Location);
    }

    private static string Joined(Dictionary<ulong, Entry> cache, ulong id, string first, string second)
    {
        if (!cache.TryGetValue(id, out var entry))
        {
            entry = new Entry();
            cache[id] = entry;
        }

        if (string.Equals(entry.First, first, StringComparison.Ordinal)
            && string.Equals(entry.Second, second, StringComparison.Ordinal))
        {
            return entry.Text;
        }

        entry.First = first;
        entry.Second = second;
        entry.Text = first.Length == 0 ? second : second.Length == 0 ? first : string.Concat(first, Separator, second);
        return entry.Text;
    }
}

internal static class FriendRow
{
    public const float Height = 64f;

    private const float AvatarRadius = 23f;
    private const float OfflineAlpha = 0.55f;
    private const float ChipGap = 8f;
    private const float ChipPadX = 9f;
    private const int AvatarSegments = 32;
    private const float MonogramScale = 0.95f;

    private static readonly TextStyle WorldChipStyle = TextStyles.Caption1;

    public static bool Draw(ChatListChrome chrome, FriendEntry friend, LodestoneService lodestone)
    {
        var ink = chrome.Ink;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var chipLabelWidth = friend.WorldName.Length > 0 ? Typography.Measure(friend.WorldName, WorldChipStyle).X : 0f;
        var chipWidth = chipLabelWidth > 0f ? chipLabelWidth + ChipPadX * 2f * scale : 0f;
        var reserve = chipWidth > 0f ? chipWidth + ChipGap * scale : 0f;
        var person = chrome.BeginPersonRow(drawList, Height, AvatarRadius, reserve, true, out var avatarCenter);
        var alpha = friend.Online ? 1f : OfflineAlpha;
        var radius = AvatarRadius * scale;
        AvatarView.Draw(drawList, avatarCenter, radius, friend.Online ? ink.Accent : ink.FaintInk,
            Initials.Of(friend.Name), MonogramScale, lodestone.Avatar(friend.Name, friend.WorldName, radius * 2f),
            AvatarSegments, alpha);
        if (friend.Online)
        {
            chrome.DrawPresenceDot(drawList, avatarCenter, radius);
        }

        if (chipWidth > 0f)
        {
            DrawWorldChip(drawList, ink, friend.WorldName, person.Bounds.Max.X - ChatListChrome.CellPadX * scale,
                person.Bounds.Center.Y, chipWidth, alpha, scale);
        }

        chrome.DrawRowTitleAndSub(drawList, new MarqueeId("linkpearl.friend.", friend.Name), friend.Name,
            FriendLabels.RowSubtitle(friend), person.TextLeft, person.TextRight, person.Bounds.Center.Y,
            Palette.WithAlpha(ink.TitleInk, alpha), Palette.WithAlpha(ink.MutedInk, alpha));
        chrome.EndPersonRow(drawList, person);
        return person.Tapped;
    }

    private static void DrawWorldChip(ImDrawListPtr drawList, SocialInk ink, string world, float right,
        float centerY, float width, float alpha, float scale)
    {
        var height = SocialChrome.MetaChipHeight * scale;
        var min = new Vector2(right - width, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(ink.ChipFill, ink.ChipFill.W * alpha)));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ink.ChipStroke, ink.ChipStroke.W * alpha)), 1f);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, world, Palette.WithAlpha(ink.MutedInk, alpha),
            WorldChipStyle);
    }
}
