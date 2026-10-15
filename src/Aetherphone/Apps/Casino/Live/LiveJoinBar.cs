using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Live;

internal sealed class LiveJoinBar
{
    public const float Height = 42f;

    private const string FieldId = "##casinoLiveCode";
    private const string JoinId = "casino.live.join";
    private const int BufferLength = 80;
    private const float GlyphInset = 14f;
    private const float GlyphSize = 16f;
    private const float GlyphGap = 8f;
    private const float FieldTextInset = 16f;
    private const float PillInset = 4f;
    private const float PillMinWidth = 64f;
    private const float RimAlpha = 0.12f;
    private const float ReasonGap = 8f;

    private static readonly string HashGlyph = IconGlyph.Of(FontAwesomeIcon.Hashtag);

    private string buffer = string.Empty;
    private string checkedBuffer = string.Empty;
    private LiveJoinInput checkedInput;
    private string reason = string.Empty;

    public bool DirectoryFailed { get; private set; }

    public void Enter()
    {
        reason = string.Empty;
        DirectoryFailed = false;
    }

    public void Consume(CasinoTablesStore tables)
    {
        var directoryFailed = tables.TakeTablesFailure();
        var intentFailed = tables.TakeIntentFailure();
        if (directoryFailed)
        {
            DirectoryFailed = true;
        }

        if (directoryFailed || intentFailed)
        {
            reason = CasinoReasons.Unreachable;
        }

        var notice = tables.TakeNoticeOutcome();
        if (notice is not null)
        {
            reason = notice.Granted ? string.Empty : notice.Reason;
        }
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, CasinoTablesStore tables, Vector2 origin, float width,
        bool accepting, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + Height * scale));
        var radius = field.Height * 0.5f;
        Squircle.Fill(drawList, field.Min, field.Max, radius,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        Squircle.Stroke(drawList, field.Min, field.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, RimAlpha)), MathF.Max(1f, scale));
        var glyphCenter = new Vector2(field.Min.X + (GlyphInset + GlyphSize * 0.5f) * scale, field.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, HashGlyph, ui.MutedInk, GlyphSize * scale);
        var label = Loc.T(L.LivePage.Join);
        var pillHeight = Button.RegularHeight * scale;
        var pillWidth = MathF.Max(PillMinWidth * scale, Button.WidthFor(label, ButtonSize.Regular));
        var pillRight = field.Max.X - PillInset * scale;
        var pill = new Rect(new Vector2(pillRight - pillWidth, field.Center.Y - pillHeight * 0.5f),
            new Vector2(pillRight, field.Center.Y + pillHeight * 0.5f));
        var textLeft = glyphCenter.X + (GlyphSize * 0.5f + GlyphGap) * scale;
        var textRight = pill.Min.X - GlyphGap * scale;
        var inset = FieldTextInset * scale;
        var input = new Rect(new Vector2(textLeft - inset, field.Min.Y), new Vector2(textRight + inset, field.Max.Y));
        bool submitted;
        using (Plugin.Fonts.Push(TextStyles.Headline.Scale, TextStyles.Headline.Weight))
        {
            submitted = GlassField.Text(input, FieldId, Loc.T(L.LivePage.CodeHint), ref buffer, ui.Theme, scale,
                BufferLength, false, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var kind = Classify(buffer);
        var ready = kind != LiveJoinInput.None && !tables.IntentInFlight;
        var tapped = Button.Draw(drawList, pill, label, ui.Ink, ready ? ButtonStyle.Prominent : ButtonStyle.Gray,
            enabled: ready, id: JoinId);
        if ((tapped || (submitted && accepting)) && ready)
        {
            Join(tables, kind);
        }

        if (reason.Length == 0)
        {
            return field.Max.Y;
        }

        return CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty,
            Loc.T(CasinoReasons.MessageFor(reason)), origin.X, field.Max.Y + ReasonGap * scale, width, scale);
    }

    private LiveJoinInput Classify(string text)
    {
        if (string.Equals(text, checkedBuffer, StringComparison.Ordinal))
        {
            return checkedInput;
        }

        checkedBuffer = text;
        checkedInput = LiveBoard.Classify(text);
        if (text.Length > 0)
        {
            reason = string.Empty;
        }

        return checkedInput;
    }

    private void Join(CasinoTablesStore tables, LiveJoinInput kind)
    {
        reason = string.Empty;
        if (kind == LiveJoinInput.Code)
        {
            tables.JoinByCode(buffer);
            buffer = string.Empty;
            return;
        }

        if (!CasinoShare.TryParse(buffer, out var tableId))
        {
            return;
        }

        tables.ResolveToken(tableId);
        buffer = string.Empty;
    }
}
