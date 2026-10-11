using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class GetChipsSheet
{
    private const string FieldId = "##getChipsCoins";
    private const int FieldDigits = 12;
    private const float MinShare = 0.3f;
    private const float MaxShare = 0.92f;
    private const float NoteFillAlpha = 0.10f;
    private const float NoteStrokeAlpha = 0.35f;

    private readonly SheetSurface sheet = new("casino.getchips");
    private readonly Action<Rect> drawBody;
    private readonly ChipsDesk desk;
    private readonly CasinoTextCache texts = new();
    private readonly ChipValueText stackValue = new();
    private readonly ChipsOption[] options = new ChipsOption[ChipsAmounts.OptionCount];
    private readonly string[] tileChips = new string[ChipsAmounts.OptionCount];
    private readonly string[] tileCoins = new string[ChipsAmounts.OptionCount];
    private readonly string[] tileCaptions = new string[ChipsAmounts.OptionCount];

    private AppSkin skin = null!;
    private long need;
    private ChipsNeedKind kind;
    private int optionCount;
    private long optionsNeed = -1;
    private long optionsStack = -1;
    private long optionsWallet = -1;
    private long optionsRate = -1;
    private ChipsNeedKind optionsKind;
    private LanguageInfo? optionsLanguage;
    private string buffer = string.Empty;
    private string note = string.Empty;

    public GetChipsSheet(ChipsDesk desk)
    {
        this.desk = desk;
        drawBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open(long needChips, ChipsNeedKind needKind)
    {
        need = Math.Max(0, needChips);
        kind = needKind;
        note = string.Empty;
        buffer = string.Empty;
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void ShowNote(string text)
    {
        note = text;
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
        if (!sheet.CapturesPointer)
        {
            return;
        }

        skin = ui;
        var scale = UiScale.Current;
        var width = screen.Width - Metrics.Space.Lg * 2f * scale;
        var content = Compute(0f, 0f, width, scale).Bottom;
        var share = Math.Clamp((SheetSurface.ChromeHeight() + content + Metrics.Space.Lg * 2f * scale) / screen.Height,
            MinShare, MaxShare);
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Strip.GetChips), share, drawBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##getChipsBody", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.NativeScrollContentWidth();
            var layout = Compute(origin.X, origin.Y, width, scale);
            var interactive = sheet.IsOpen && !desk.Buying;
            DrawBalances(drawList, layout);
            if (layout.HasNeed)
            {
                Typography.DrawWrappedLeft(layout.Need.Min, NeedText(), skin.BodyInk, TextStyles.Footnote,
                    layout.Need.Width);
            }

            if (layout.HasNote)
            {
                DrawNote(drawList, layout.Note, scale);
            }

            for (var index = 0; index < layout.TileCount; index++)
            {
                DrawTile(drawList, layout.Tile(index), index, interactive);
            }

            DrawCustom(drawList, layout, interactive);
            DrawAuto(drawList, layout);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(1f, layout.Bottom - origin.Y + Metrics.Space.Sm * scale)));
        }
    }

    private GetChipsLayout Compute(float left, float top, float width, float scale)
    {
        RefreshOptions();
        var needText = NeedText();
        var hint = Loc.T(L.Chips.AutoTopUpHint);
        var blocks = new GetChipsBlocks(Typography.LineHeight(TextStyles.Subheadline),
            needText.Length > 0 ? Typography.MeasureWrappedBlock(needText, TextStyles.Footnote, width).Y : 0f,
            Typography.LineHeight(TextStyles.Footnote), Typography.LineHeight(TextStyles.Headline),
            Typography.LineHeight(TextStyles.Footnote), Typography.LineHeight(TextStyles.SubheadlineEmphasized),
            Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, GetChipsLayout.AutoTextWidth(width, scale)).Y,
            note.Length > 0
                ? Typography.MeasureWrappedBlock(note, TextStyles.Footnote,
                    width - GetChipsLayout.NotePad * 2f * scale).Y
                : 0f);
        return GetChipsLayout.Compute(left, top, width, blocks, optionCount, scale);
    }

    private string NeedText()
    {
        if (need <= 0)
        {
            return string.Empty;
        }

        return texts.Compacts(kind == ChipsNeedKind.BuyIn ? L.Chips.NeedBuyIn : L.Chips.NeedBet, need, desk.Stack);
    }

    private void RefreshOptions()
    {
        var stack = desk.Stack;
        var wallet = desk.WalletCoins;
        var rate = desk.Rate;
        if (need == optionsNeed && stack == optionsStack && wallet == optionsWallet && rate == optionsRate
            && kind == optionsKind && ReferenceEquals(optionsLanguage, Loc.Current))
        {
            return;
        }

        optionsNeed = need;
        optionsStack = stack;
        optionsWallet = wallet;
        optionsRate = rate;
        optionsKind = kind;
        optionsLanguage = Loc.Current;
        optionCount = ChipsAmounts.ForNeed(need, stack, wallet, rate, kind, options);
        var plural = kind == ChipsNeedKind.BuyIn ? L.Chips.BuyIns : L.Chips.Bets;
        for (var index = 0; index < optionCount; index++)
        {
            var option = options[index];
            tileChips[index] = NumberText.Compact(option.Chips);
            tileCoins[index] = Loc.T(L.Strip.CoinsAmount, NumberText.Group(option.Coins));
            tileCaptions[index] = Loc.Plural(plural, (int)Math.Min(option.Covers, int.MaxValue));
        }
    }

    private void DrawBalances(ImDrawListPtr drawList, in GetChipsLayout layout)
    {
        var walletText = NumberText.Group(desk.WalletCoins);
        DrawRow(drawList, layout.WalletRow, Loc.T(L.Casino.WalletRow), walletText, CurrencyKind.Coins, skin.TitleInk,
            string.Empty);
        var stack = desk.Stack;
        DrawRow(drawList, layout.ChipsRow, Loc.T(L.Casino.ChipsRow), NumberText.Compact(stack), CurrencyKind.Chips,
            CasinoColors.Money, stackValue.Full(stack, desk.Rate));
    }

    private void DrawRow(ImDrawListPtr drawList, Rect row, string label, string amount, CurrencyKind currency,
        Vector4 ink, string trailing)
    {
        var style = TextStyles.SubheadlineEmphasized;
        var amountSize = CurrencyGlyph.MeasureAmount(amount, style);
        var gap = Metrics.Space.Sm * UiScale.Current;
        var trailingWidth = trailing.Length > 0 ? Typography.Measure(trailing, TextStyles.Footnote).X + gap : 0f;
        var labelWidth = row.Width - amountSize.X - gap;
        if (trailingWidth > 0f && labelWidth - trailingWidth < Typography.Measure(label, TextStyles.Subheadline).X)
        {
            trailingWidth = 0f;
        }

        var right = row.Max.X - trailingWidth;
        var labelHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, right - amountSize.X - gap - row.Min.X), TextStyles.Subheadline),
            skin.BodyInk, TextStyles.Subheadline);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(right - amountSize.X, row.Center.Y - amountSize.Y * 0.5f),
            amount, currency, ink, style);
        if (trailingWidth <= 0f)
        {
            return;
        }

        var footnote = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(right + gap, row.Center.Y - footnote * 0.5f), trailing, skin.MutedInk,
            TextStyles.Footnote);
    }

    private void DrawTile(ImDrawListPtr drawList, Rect rect, int index, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, skin.Ink, ButtonStyle.Gray, ButtonRole.Normal, interactive,
            hovered, ImGui.GetID($"getchips.tile{index}"), 1f, Metrics.Radius.Grouped * scale);
        var area = face.Face;
        var pad = GetChipsLayout.TilePad * scale;
        var width = MathF.Max(1f, area.Width - pad * 2f);
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var amountHeight = Typography.LineHeight(TextStyles.Headline);
        var top = area.Center.Y - (captionHeight + amountHeight + captionHeight) * 0.5f;
        var caption = tileCaptions[index];
        var captionScale = Typography.FitScale(caption, width, TextStyles.Footnote.Scale, TextStyles.Caption2.Scale,
            TextStyles.Footnote.Weight);
        var captionSize = Typography.Measure(caption, captionScale, TextStyles.Footnote.Weight);
        Typography.Draw(drawList, new Vector2(area.Center.X - captionSize.X * 0.5f, top), caption,
            Palette.WithAlpha(skin.BodyInk, face.LabelInk.W), captionScale, TextStyles.Footnote.Weight);

        var chips = tileChips[index];
        var amountSize = CurrencyGlyph.MeasureAmount(chips, TextStyles.Headline);
        var amountTop = top + captionHeight;
        if (amountSize.X <= width)
        {
            CurrencyGlyph.DrawAmount(drawList, new Vector2(area.Center.X - amountSize.X * 0.5f, amountTop), chips,
                CurrencyKind.Chips, CasinoColors.Money with { W = face.LabelInk.W }, TextStyles.Headline,
                face.LabelInk.W);
        }
        else
        {
            Typography.DrawCentered(drawList, new Vector2(area.Center.X, amountTop + amountHeight * 0.5f), chips,
                CasinoColors.Money with { W = face.LabelInk.W }, TextStyles.Headline);
        }

        var coins = tileCoins[index];
        var coinsScale = Typography.FitScale(coins, width, TextStyles.Footnote.Scale, TextStyles.Caption2.Scale,
            TextStyles.Footnote.Weight);
        var coinsSize = Typography.Measure(coins, coinsScale, TextStyles.Footnote.Weight);
        Typography.Draw(drawList, new Vector2(area.Center.X - coinsSize.X * 0.5f, amountTop + amountHeight), coins,
            Palette.WithAlpha(skin.MutedInk, face.LabelInk.W), coinsScale, TextStyles.Footnote.Weight);
        if (interactive && UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            note = string.Empty;
            desk.Buy(options[index].Coins);
        }
    }

    private void DrawCustom(ImDrawListPtr drawList, in GetChipsLayout layout, bool interactive)
    {
        var field = layout.Field;
        SearchBar.Surface(drawList, field, skin.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var glyph = Typography.LineHeight(TextStyles.Body) * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(capsule.Min.X + inset + glyph * 0.5f,
            field.Center.Y), glyph);
        var textLeft = capsule.Min.X + inset + glyph + inset * 0.5f;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(textLeft, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, capsule.Max.X - inset - textLeft));
        using (ImRaii.Disabled(!interactive))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, skin.TitleInk))
        {
            ImGui.InputTextWithHint(FieldId, Loc.T(L.Strip.CoinsFieldHint), ref buffer, FieldDigits + 1,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        ImGui.SetCursorScreenPos(cursor);
        var coins = long.TryParse(buffer, NumberStyles.None, CultureInfo.InvariantCulture, out var typed) ? typed : 0;
        var allowed = coins >= ChipsAmounts.MinimumCoins && coins <= desk.WalletCoins;
        var label = allowed ? texts.Compact(L.Strip.GetChipsFor, coins * desk.Rate) : Loc.T(L.Strip.GetChips);
        if (!Button.Draw(drawList, layout.Buy, label, skin.Ink, ButtonStyle.Prominent, enabled: interactive && allowed,
                overlay: true, id: "getchips.custom"))
        {
            return;
        }

        note = string.Empty;
        desk.Buy(coins);
    }

    private void DrawAuto(ImDrawListPtr drawList, in GetChipsLayout layout)
    {
        var text = layout.AutoText;
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, text.Min, Typography.FitText(Loc.T(L.Chips.AutoTopUp), text.Width,
            TextStyles.SubheadlineEmphasized), skin.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(text.Min.X, text.Min.Y + titleHeight), Loc.T(L.Chips.AutoTopUpHint),
            skin.MutedInk, TextStyles.Footnote, text.Width);
        var row = layout.AutoRow;
        var hovered = sheet.IsOpen && UiInteract.HoverWindowOnly(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var on = desk.AutoTopUpOn;
        if (UiInteract.Click(row.Min, row.Max, hovered, false))
        {
            on = !on;
            desk.SetAutoTopUp(on);
            UiFeedback.Play(on ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        Toggle.Draw("casino.getchips.auto", layout.AutoToggle, on, skin.Theme, 1f, false);
    }

    private void DrawNote(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, NoteFillAlpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, NoteStrokeAlpha)), Metrics.Stroke.Hairline);
        var pad = GetChipsLayout.NotePad * scale;
        Typography.DrawWrappedLeft(new Vector2(rect.Min.X + pad, rect.Min.Y + pad), note, skin.TitleInk,
            TextStyles.Footnote, rect.Width - pad * 2f);
    }
}
