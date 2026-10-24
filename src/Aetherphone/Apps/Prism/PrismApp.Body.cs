using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int NoGender = -1;
    private const float BodyRaceShare = 0.55f;
    private const float BodyChevronUnits = 16f;

    private readonly DropdownMenu bodyRaceMenu = new() { FadeOnClose = true, Compact = true };
    private readonly DropdownMenu.Item[] bodyRaceItems = new DropdownMenu.Item[RaceCount];
    private readonly string[] genderLabels = new string[2];
    private int composeRace;
    private int composeGender = NoGender;
    private int entryRace;
    private int entryGender = NoGender;
    private CachedText bodyRaceText;

    private static bool BodyChosen(int race, int gender) => race > 0 && gender != NoGender;

    private float DrawBodyField(ImDrawListPtr drawList, float left, float top, float width, string raceId,
        string genderId, ref int race, ref int gender, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.Prism.BodyLabel), width, PrismType.Caption), PrismArt.Muted,
            PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + Metrics.Size.TapTarget * scale));
        DrawBodyPicker(drawList, row, raceId, genderId, ref race, ref gender, scale);
        return row.Max.Y + Metrics.Space.Md * scale;
    }

    private void DrawBodyPicker(ImDrawListPtr drawList, Rect row, string raceId, string genderId, ref int race,
        ref int gender, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var split = row.Min.X + (row.Width - gap) * BodyRaceShare;
        var field = new Rect(row.Min, new Vector2(split, row.Max.Y));
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)));
        var hovered = UiInteract.Hover(field.Min, field.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = Metrics.Space.Md * scale;
        var chevron = BodyChevronUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(field.Max.X - inset - chevron * 0.5f, field.Center.Y),
            PhoneIcons.ChevronDown, PrismArt.Muted, chevron);
        var room = MathF.Max(1f, field.Width - inset * 2f - chevron - Metrics.Space.Xs * scale);
        var label = race > 0 ? BodyRaceName(race, gender) : Loc.T(L.Prism.FilterRace);
        var style = new TextStyle(PrismType.Headline.Scale * TrackLabelShare, FontWeight.SemiBold);
        var fitted = Typography.FitText(label, room, style);
        Typography.Draw(drawList,
            new Vector2(field.Min.X + inset, field.Center.Y - Typography.LineHeight(style) * 0.5f), fitted,
            race > 0 ? PrismArt.Fore : PrismArt.Muted, style);
        if (UiInteract.Click(field.Min, field.Max, hovered))
        {
            FillBodyRaceItems(race, gender);
            bodyRaceMenu.Header = Loc.T(L.Prism.FilterRace);
            bodyRaceMenu.Toggle(raceId, field);
        }

        genderLabels[0] = Loc.T(L.Prism.GenderMale);
        genderLabels[1] = Loc.T(L.Prism.GenderFemale);
        gender = DrawSegments(genderId, new Rect(new Vector2(split + gap, row.Min.Y), row.Max), genderLabels, gender);
    }

    private void DrawBodyRaceMenu(Rect screen, ref int race)
    {
        var picked = bodyRaceMenu.Draw(screen, DropdownMenu.Style.From(ui), bodyRaceItems);
        if (picked >= 0)
        {
            race = picked + 1;
        }
    }

    private void FillBodyRaceItems(int race, int gender)
    {
        var female = gender == FemaleGender;
        for (var index = 0; index < bodyRaceItems.Length; index++)
        {
            bodyRaceItems[index] = new DropdownMenu.Item(gameData.RaceName((uint)(index + 1), female),
                Selected: race == index + 1);
        }
    }

    private string BodyRaceName(int race, int gender)
    {
        var key = HashCode.Combine(race, gender, Loc.Current.Code);
        return bodyRaceText.IsCurrent(key)
            ? bodyRaceText.Value
            : bodyRaceText.Store(key, gameData.RaceName((uint)race, gender == FemaleGender));
    }
}
