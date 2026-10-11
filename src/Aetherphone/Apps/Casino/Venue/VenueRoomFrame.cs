using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal readonly ref struct VenueRoomFrame
{
    public readonly CasinoStage Stage;
    public readonly CasinoStageFrame Frame;
    public readonly AppSkin Ui;
    public readonly VenueRoomView View;
    public readonly ImDrawListPtr DrawList;
    public readonly Rect World;
    public readonly string Me;
    public readonly bool Hosting;
    public readonly bool Busy;
    public readonly long NowUnixMs;
    public readonly float Scale;

    public VenueRoomFrame(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, VenueRoomView view,
        ImDrawListPtr drawList, Rect world, string me, bool hosting, bool busy, long nowUnixMs, float scale)
    {
        Stage = stage;
        Frame = frame;
        Ui = ui;
        View = view;
        DrawList = drawList;
        World = world;
        Me = me;
        Hosting = hosting;
        Busy = busy;
        NowUnixMs = nowUnixMs;
        Scale = scale;
    }

    public bool Gil => View.Gil;

    public bool Enabled => !Busy && !Frame.Blocked;

    public bool IsMe(string userId) => Me.Length > 0 && string.Equals(userId, Me, StringComparison.Ordinal);
}
