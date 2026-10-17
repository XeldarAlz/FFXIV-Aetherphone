using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum PrismScreen : byte
{
    Cover,
    Entry,
    Pick,
    Runway,
    Reveal,
    Final,
}

internal sealed partial class PrismApp : IPhoneApp
{
    private const float RefreshSeconds = 120f;
    private const float HeaderGapUnits = 8f;
    private const float NoticeMaxUnits = 40f;
    private const float NoticeMinUnits = 24f;
    private const int NoticeLines = 3;

    private readonly PrismStore store;
    private readonly RemoteImageCache images;
    private readonly PhotoLibrary library;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly PhotoComposeSession pickSession;
    private readonly ImageIntake pickIntake = new();
    private readonly Action withdraw;
    private readonly Action<bool> ballotSettled;
    private readonly Action<bool> entrySettled;

    private PrismScreen screen = PrismScreen.Cover;
    private PrismScreen pickReturn = PrismScreen.Cover;
    private readonly Dictionary<string, double> loadedAt = new(StringComparer.Ordinal);
    private float screenAge;
    private int enterDirection = 1;
    private INavigator navigation = null!;
    private string? pendingSharedPhoto;
    private float sinceRefresh;

    public PrismApp(AethernetSession session, AethernetApi api, RemoteImageCache images, PhotoLibrary library,
        WallpaperImageCache wallpaperImages, Configuration configuration, ConfirmService confirm,
        ReportService report)
    {
        store = new PrismStore(session, api);
        this.images = images;
        this.library = library;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        pickSession = new PhotoComposeSession(library, wallpaperImages);
        withdraw = store.Withdraw;
        ballotSettled = SettleBallot;
        entrySettled = SettleEntry;
    }

    public string Id => "prism";
    public string DisplayName => Loc.T(L.Apps.Prism);
    public string Glyph => "Pr";
    public int BadgeCount => 0;

    public ShareKindSet AcceptedShares => store.IsSignedIn ? ShareKindSet.Photo : ShareKindSet.None;

    public LocString? ShareLabel(ShareKind kind) => L.Prism.ShareLabel;

    public void OnShare(in ShareItem item)
    {
        if (item.Kind == ShareKind.Photo)
        {
            pendingSharedPhoto = item.LocalPath;
        }
    }

    public void OnOpened()
    {
        Go(PrismScreen.Cover);
        sinceRefresh = 0f;
        store.ClearBallot();
        store.Refresh();
    }

    public void OnClosed()
    {
        pickIntake.Discard();
    }

    public void Draw(in PhoneContext context)
    {
        navigation = context.Navigation;
        var scale = UiScale.Current;
        var content = context.Content;
        var full = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ImGui.GetWindowDrawList().AddRectFilled(full.Min, full.Max, ImGui.GetColorU32(PrismArt.Black));
        TickRefresh();
        ConsumeSharedPhoto();
        screenAge += MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var enter = PrismMotion.At(screenAge, 0f, TransitionTiming.PresentSmoothTime);
        var stage = ScreenLayer.Begin("prism.stage", full, enter < TransitionTiming.InteractiveProgress);
        try
        {
            DrawScreen(ImGui.GetWindowDrawList(), full, content, scale);
        }
        finally
        {
            stage.Transform(PrismMotion.Stage(full, enter, enterDirection, scale));
        }
    }

    private void DrawScreen(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        switch (screen)
        {
            case PrismScreen.Entry:
                DrawEntry(drawList, full, content, scale);
                break;
            case PrismScreen.Pick:
                DrawPick(drawList, content, scale);
                break;
            case PrismScreen.Runway:
                DrawRunway(drawList, content, scale);
                break;
            case PrismScreen.Reveal:
                DrawReveal(drawList, full, content, scale);
                break;
            case PrismScreen.Final:
                DrawFinal(drawList, content, scale);
                break;
            default:
                DrawCover(drawList, full, content, scale);
                break;
        }
    }

    private void Go(PrismScreen next, int direction = 1)
    {
        screen = next;
        enterDirection = direction;
        screenAge = 0f;
    }

    private float DrawHeader(ImDrawListPtr drawList, Rect content, string left, string right, bool back, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var row = new Rect(new Vector2(content.Min.X + gutter, top), new Vector2(content.Max.X - gutter, top));
        float height;
        if (back)
        {
            if (PrismArt.Link(drawList, row.Min, PrismType.Upper(Loc.T(L.Prism.Back)), PrismArt.White, scale))
            {
                Go(screen == PrismScreen.Pick ? pickReturn : PrismScreen.Cover, -1);
            }

            height = PrismType.LineHeight(EditorialFace.Mono, PrismArt.MonoPixels());
            if (right.Length > 0)
            {
                PrismType.DrawRight(drawList, new Vector2(row.Max.X, top), right, PrismArt.White, EditorialFace.Mono,
                    PrismArt.MonoPixels());
            }
        }
        else
        {
            height = PrismArt.Programme(drawList, row, left, right, PrismArt.White);
        }

        var rule = top + height + HeaderGapUnits * 0.5f * scale;
        PrismArt.Hairline(drawList, row.Min.X, row.Max.X, rule, PrismArt.White, scale);
        return rule + HeaderGapUnits * scale;
    }

    private Rect DrawLook(ImDrawListPtr drawList, Rect rect, string url, bool contain, float alpha = 1f,
        float zoom = 1f)
    {
        if (url.Length == 0)
        {
            return rect;
        }

        var texture = images.Sized(url, MathF.Max(rect.Width, rect.Height));
        if (texture is null)
        {
            var placeholder = PrismType.Upper(Loc.T(images.Failed(url) ? L.Common.ImageFailed : L.Common.Loading));
            var size = PrismType.Measure(placeholder, EditorialFace.Mono, PrismArt.MonoPixels());
            PrismType.Draw(drawList, rect.Center - size * 0.5f, placeholder, PrismArt.Faint, EditorialFace.Mono,
                PrismArt.MonoPixels());
            return rect;
        }

        var now = ImGui.GetTime();
        if (!loadedAt.TryGetValue(url, out var seen))
        {
            seen = now;
            loadedAt[url] = seen;
        }

        alpha *= PrismMotion.At((float)(now - seen), 0f, Motion.Appear);
        var frame = contain ? ImageFit.CenteredRect(rect, texture.Size.X / MathF.Max(1f, texture.Size.Y)) : rect;
        var (uv0, uv1) = contain
            ? (Vector2.Zero, Vector2.One)
            : ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        var drawn = zoom == 1f ? frame : new Rect(frame.Center - frame.Size * 0.5f * zoom, frame.Center + frame.Size * 0.5f * zoom);
        if (zoom != 1f)
        {
            drawList.PushClipRect(frame.Min, frame.Max, true);
        }

        drawList.AddImage(texture.Handle, drawn.Min, drawn.Max, uv0, uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        if (zoom != 1f)
        {
            drawList.PopClipRect();
        }

        return frame;
    }

    private static void DrawWipe(ImDrawListPtr drawList, Vector2 position, string text, EditorialFace face,
        float pixels, float progress, Vector4 color)
    {
        var size = PrismType.Measure(text, face, pixels);
        drawList.PushClipRect(position, position + size, true);
        PrismType.Draw(drawList, position + new Vector2(0f, size.Y * (1f - progress)), text, color, face, pixels);
        drawList.PopClipRect();
    }

    private float DrawNotice(ImDrawListPtr drawList, Rect content, string title, string body, float scale,
        int strips = 1)
    {
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var pixels = PrismType.Fit(PrismType.Upper(title), EditorialFace.Headline, width, NoticeMaxUnits * scale,
            NoticeMinUnits * scale, NoticeLines, out var lines);
        var mono = PrismArt.MonoPixels();
        var bodyLines = body.Length > 0 ? PrismType.Wrap(PrismType.Upper(body), EditorialFace.Mono, mono, width) : [];
        var height = lines.Length * PrismType.LineHeight(EditorialFace.Headline, pixels) + Metrics.Space.Md * scale +
                     bodyLines.Length * PrismType.LineHeight(EditorialFace.Mono, mono);
        var bottom = StripRect(content, scale, strips - 1).Min.Y - Metrics.Space.Lg * scale;
        var top = bottom - height;
        top += PrismType.DrawLines(drawList, new Vector2(content.Min.X + gutter, top), lines, PrismArt.White,
            EditorialFace.Headline, pixels) + Metrics.Space.Md * scale;
        PrismType.DrawLines(drawList, new Vector2(content.Min.X + gutter, top), bodyLines, PrismArt.Muted,
            EditorialFace.Mono, mono);
        return bottom;
    }

    private Rect StripRect(Rect content, float scale, int fromBottom = 0)
    {
        var gutter = PrismArt.Gutter(scale);
        var height = PrismArt.StripUnits * scale;
        var bottom = content.Max.Y - gutter - fromBottom * (height + Metrics.Space.Sm * scale);
        return new Rect(new Vector2(content.Min.X + gutter, bottom - height), new Vector2(content.Max.X - gutter, bottom));
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void ConsumeSharedPhoto()
    {
        var path = pendingSharedPhoto;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        pendingSharedPhoto = null;
        if (store.Week?.Phase != PrismPhases.Entering)
        {
            return;
        }

        StartPick();
        pickSession.TakeImported(path);
    }

    private void OpenReport(string lookId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Prism.ReportTitle),
            Submit = (reason, done) => store.Report(lookId, reason, done),
        });
    }

    public void Dispose()
    {
        pickSession.Dispose();
        store.Dispose();
    }
}
