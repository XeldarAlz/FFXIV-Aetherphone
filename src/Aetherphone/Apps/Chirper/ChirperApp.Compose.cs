using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

// The compose flow: the floating compose button, the compose card, and the text wrapping/length
// bookkeeping that keeps the chirp within limits. Split from the main feed for readability.
internal sealed partial class ChirperApp
{
    private void DrawComposeFab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 26f * scale;
        var center = new Vector2(area.Max.X - radius - 16f * scale, area.Max.Y - radius - 18f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent), 32);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Feather.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                composeFocus = true;
                router.Push(ChirperRoute.Compose);
            }
        }
    }

    private void DrawCompose(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            composeStatus = string.Empty;
            sinceForYou = FeedRefreshSeconds;
            sinceFollowing = FeedRefreshSeconds;
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.NewChirp), back);
        var canPost = !string.IsNullOrWhiteSpace(draft) && !store.Posting;
        if (DrawHeaderAction(area, store.Posting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Post), canPost))
        {
            Submit();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var footerHeight = 40f * scale;
            var cardMin = origin;
            var cardMax = new Vector2(origin.X + width, area.Max.Y - footerHeight);
            ui.Card(drawList, cardMin, cardMax, 18f * scale);
            var pad = 14f * scale;
            var radius = 20f * scale;
            var me = store.Me;
            var displayName =
                me is null ? string.Empty : (string.IsNullOrEmpty(me.DisplayName) ? me.Name : me.DisplayName);
            if (me is not null)
            {
                DrawAvatar(drawList, new Vector2(cardMin.X + pad + radius, cardMin.Y + pad + radius), radius, me.Name,
                    me.World, me.AvatarUrl, 0.95f, 48);
            }

            var inputLeft = pad + radius * 2f + 12f * scale;
            var inputX = cardMin.X + inputLeft;
            var nameSize = displayName.Length > 0
                ? Typography.Measure(displayName, 1.05f, FontWeight.SemiBold)
                : Vector2.Zero;
            if (displayName.Length > 0)
            {
                Typography.Draw(new Vector2(inputX, cardMin.Y + pad), displayName, theme.TextStrong, 1.05f,
                    FontWeight.SemiBold);
            }

            var inputTop = cardMin.Y + pad + nameSize.Y + 6f * scale;
            var inputWidth = width - inputLeft - pad;
            var inputHeight = cardMax.Y - inputTop - pad;
            ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
            ImGui.SetNextItemWidth(inputWidth);
            if (composeFocus)
            {
                ImGui.SetKeyboardFocusHere();
                composeFocus = false;
            }

            var framePadding = ImGui.GetStyle().FramePadding.X;
            composeWrapWidth = inputWidth - framePadding * 2f - 4f * scale;
            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.TitleInk))
            using (Plugin.Fonts.Push(1.15f))
            {
                ImGui.InputTextMultiline("##chirpBody", ref draft, ComposeBufferBytes,
                    new Vector2(inputWidth, inputHeight),
                    ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.CallbackCharFilter, composeCallback);
            }

            if (draft.Length == 0)
            {
                Typography.Draw(new Vector2(inputX + 4f * scale, inputTop + 2f * scale), Loc.T(L.Chirper.Compose),
                    AppPalettes.Chirper.MutedInk, 1.15f);
            }

            var footerY = area.Max.Y - footerHeight * 0.5f;
            if (composeStatus.Length > 0)
            {
                Typography.Draw(
                    new Vector2(origin.X + 2f * scale, footerY - Typography.Measure(composeStatus, 0.85f).Y * 0.5f),
                    composeStatus, theme.Danger, 0.85f);
            }

            var remaining = MaxPostLength - ComposeLogicalLength(draft);
            var counterColor = remaining < 40
                ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
                : AppPalettes.Chirper.MutedInk;
            var counter = remaining.ToString(Loc.Culture);
            var counterSize = Typography.Measure(counter, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(area.Max.X - 4f * scale - counterSize.X, footerY - counterSize.Y * 0.5f),
                counter, counterColor, 0.9f, FontWeight.Medium);
        }
    }

    private int ComposeTextCallback(ImGuiInputTextCallbackDataPtr data)
    {
        if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
        {
            if (data.EventChar is '\n' or '\r')
            {
                data.EventChar = 0;
            }

            return 0;
        }

        var current = System.Text.Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
        var charCursor = ByteIndexToCharIndex(current, data.CursorPos);
        var logicalCursor = charCursor - CountNewlines(current, charCursor);

        var logical = StripNewlines(current);
        if (logical.Length > MaxPostLength)
        {
            logical = logical[..MaxPostLength];
            if (logicalCursor > MaxPostLength)
            {
                logicalCursor = MaxPostLength;
            }
        }

        var wrapped = WrapText(logical, composeWrapWidth);
        if (string.Equals(wrapped, current, StringComparison.Ordinal))
        {
            return 0;
        }

        var wrappedCursor = LogicalToWrappedIndex(wrapped, logicalCursor);
        var byteCursor = System.Text.Encoding.UTF8.GetByteCount(wrapped.AsSpan(0, wrappedCursor));

        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, wrapped);
        data.CursorPos = byteCursor;
        data.SelectionStart = byteCursor;
        data.SelectionEnd = byteCursor;
        return 0;
    }

    private static string WrapText(string text, float wrapWidth)
    {
        if (text.Length == 0 || wrapWidth <= 0f)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length + 16);
        var lineWidth = 0f;
        var lineStart = 0;
        var wordStart = 0;
        var index = 0;
        while (index < text.Length)
        {
            var runeLength = char.IsHighSurrogate(text[index]) && index + 1 < text.Length &&
                             char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;
            var isSpace = runeLength == 1 && text[index] is ' ' or '\t';
            var characterWidth = ImGui.CalcTextSize(text.Substring(index, runeLength)).X;

            if (!isSpace && lineWidth > 0f && lineWidth + characterWidth > wrapWidth)
            {
                if (wordStart > lineStart)
                {
                    builder.Insert(wordStart, '\n');
                    lineStart = wordStart + 1;
                    lineWidth = MeasureRange(builder, lineStart);
                    wordStart = lineStart;
                }
                else
                {
                    builder.Append('\n');
                    lineStart = builder.Length;
                    lineWidth = 0f;
                    wordStart = builder.Length;
                }
            }

            builder.Append(text, index, runeLength);
            lineWidth += characterWidth;
            if (isSpace)
            {
                wordStart = builder.Length;
            }

            index += runeLength;
        }

        return builder.ToString();
    }

    private static float MeasureRange(System.Text.StringBuilder builder, int start)
    {
        if (start >= builder.Length)
        {
            return 0f;
        }

        return ImGui.CalcTextSize(builder.ToString(start, builder.Length - start)).X;
    }

    private static string StripNewlines(string text)
    {
        return text.IndexOf('\n') < 0 ? text : text.Replace("\n", string.Empty);
    }

    private static int ComposeLogicalLength(string text)
    {
        var length = text.Length;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                length--;
            }
        }

        return length;
    }

    private static int CountNewlines(string text, int limit)
    {
        var count = 0;
        for (var index = 0; index < limit; index++)
        {
            if (text[index] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static int ByteIndexToCharIndex(string text, int byteIndex)
    {
        if (byteIndex <= 0)
        {
            return 0;
        }

        var bytes = 0;
        var index = 0;
        while (index < text.Length && bytes < byteIndex)
        {
            var runeLength = char.IsHighSurrogate(text[index]) && index + 1 < text.Length &&
                             char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;
            bytes += System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(index, runeLength));
            index += runeLength;
        }

        return index;
    }

    private static int LogicalToWrappedIndex(string wrapped, int logicalCursor)
    {
        var seen = 0;
        var index = 0;
        while (index < wrapped.Length && seen < logicalCursor)
        {
            if (wrapped[index] != '\n')
            {
                seen++;
            }

            index++;
        }

        return index;
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(draft) || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.Compose(StripNewlines(draft), ok => composeOutcome = ok ? 1 : 2);
    }
}
