using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal interface ICabinetIdle
{
    Backdrop IdleBackdrop { get; }

    void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds);
}

internal sealed class CabinetPreview
{
    public const float SafeInset = 12f;

    private const float ScrimFrom = 0.55f;
    private const float ScrimAlpha = 0.55f;

    private readonly StageBackdrop backdrop = new();
    private ICabinetIdle? cabinet;

    public void Prepare(ICabinetIdle idle)
    {
        if (ReferenceEquals(cabinet, idle))
        {
            return;
        }

        cabinet = idle;
        backdrop.Set(idle.IdleBackdrop);
    }

    public void Draw(ImDrawListPtr drawList, Rect card, float radius, Vector4 accent, bool live, bool hovered,
        float deltaSeconds, float scale)
    {
        if (cabinet is null)
        {
            return;
        }

        drawList.PushClipRect(card.Min, card.Max, true);
        if (live)
        {
            backdrop.Update(deltaSeconds, card, ImGui.GetMousePos(), hovered);
        }

        backdrop.Draw(drawList, card, accent, scale);
        cabinet.DrawIdle(drawList, card.Inset(SafeInset * scale), live ? deltaSeconds : 0f);
        LivePreview.Scrim(drawList, card, ScrimFrom, ScrimAlpha);
        drawList.PopClipRect();
        LivePreview.Rim(drawList, card, radius, scale);
    }
}
