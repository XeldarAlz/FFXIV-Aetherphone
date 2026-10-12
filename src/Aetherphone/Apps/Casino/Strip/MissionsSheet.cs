using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class MissionsSheet
{
    private const float HeightShare = 0.78f;

    private readonly SheetSurface sheet = new("casino.missions");
    private readonly MissionsCard card = new();
    private readonly CashierBonusShelf shower;
    private readonly Action<Rect> drawBody;
    private AppSkin skin = null!;
    private CasinoMissionsDto? missions;
    private string claiming = string.Empty;
    private long nowUnix;
    private string tapped = string.Empty;

    public MissionsSheet(CashierBonusShelf shower)
    {
        this.shower = shower;
        drawBody = DrawBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public Vector2 ClaimCenter => card.ClaimCenter;

    public void Open() => sheet.Open();

    public void Close() => sheet.Close();

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public string TakeClaim()
    {
        var taken = tapped;
        tapped = string.Empty;
        return taken;
    }

    public void Draw(Rect screen, AppSkin ui, CasinoMissionsDto? missions, string claiming, long nowUnix)
    {
        skin = ui;
        this.missions = missions;
        this.claiming = claiming;
        this.nowUnix = nowUnix;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Floor.MissionsTitle), HeightShare, drawBody);
    }

    private void DrawBody(Rect content)
    {
        if (missions is null || MissionsCard.Count(missions) == 0)
        {
            sheet.Close();
            return;
        }

        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##casinoMissions", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.NativeScrollContentWidth();
            var origin = ImGui.GetCursorScreenPos();
            var claim = card.Draw(drawList, skin, missions, origin, width, claiming, nowUnix, scale, out var bottom);
            if (claim.Length > 0)
            {
                tapped = claim;
            }

            shower.DrawShower(drawList, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }
}
