using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.KupoAi;

internal sealed partial class KupoAiApp : IPhoneApp, IChatTranscriptInteractions
{
    private enum KupoAiView : byte
    {
        List,
        Thread,
    }

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public string Id => "kupoai";
    public string DisplayName => Loc.T(L.Apps.KupoAi);
    public string Glyph => "Ku";
    public int BadgeCount => 0;

    private readonly KupoAiStore store;
    private readonly AppSkin ui = new(AppPalettes.KupoAi);
    private readonly ViewRouter<KupoAiView> router;
    private readonly RouterDraw<KupoAiView> drawView;
    private readonly ChatTranscript transcript = new();
    private readonly DropdownMenu messageMenu = new();
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private KupoAiConversation? activeConversation;
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private int transcriptVersion = -1;
    private string transcriptConversationId = string.Empty;
    private string draft = string.Empty;
    private string? menuMessageId;
    private bool composerFocus;

    public KupoAiApp(KupoAiStore store, RemoteImageCache images)
    {
        this.store = store;
        router = new ViewRouter<KupoAiView>(KupoAiView.List, Id);
        drawView = DrawView;
        back = HandleBack;
    }

    public void OnOpened()
    {
        router.Reset();
        activeConversation = null;
        draft = string.Empty;
        store.EnsureLoaded();
        store.RefreshStatus();
    }

    public void OnClosed()
    {
        router.Reset();
        activeConversation = null;
        draft = string.Empty;
        messageMenu.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        messageMenu.Gate();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(KupoAiView view, Rect area, int depth)
    {
        ui.Body(area);
        if (view == KupoAiView.Thread && activeConversation is not null)
        {
            DrawThread(area, activeConversation);
        }
        else
        {
            DrawList(area);
        }
    }

    private void HandleBack()
    {
        if (activeConversation is { } conversation && conversation.Messages.Count == 0)
        {
            store.DeleteConversation(conversation.Id);
            activeConversation = null;
        }

        messageMenu.Close();
        router.Pop();
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
