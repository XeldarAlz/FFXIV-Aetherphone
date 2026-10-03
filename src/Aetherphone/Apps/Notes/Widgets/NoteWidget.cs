using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notes.Widgets;

internal sealed class NoteWidget : IHomeWidget
{
    private const string AppKey = "notes";
    private const string NoteKey = "note";
    private const int RefreshMilliseconds = 1000;
    private const int SampleHour = 18;
    private const int SampleMinute = 30;

    private sealed class NoteText
    {
        public string Body = string.Empty;
        public string Title = string.Empty;
        public string Preview = string.Empty;
        public CachedText Stamp;
    }

    private sealed class Selection
    {
        public string Config = string.Empty;
        public PhoneNote? Note;
        public WidgetRefresh Refresh;
    }

    private readonly Configuration configuration;
    private readonly WidgetOption[] options;
    private readonly Dictionary<Guid, NoteText> texts = new();
    private readonly WidgetStates<Selection> selections = new();
    private CachedText sampleStamp;

    public NoteWidget(Configuration configuration)
    {
        this.configuration = configuration;
        options = new[] { new WidgetOption(NoteKey, L.WidgetsUtility.NoteOption, FillChoices) };
    }

    public string Id => "notes.note";
    public string DisplayName => Loc.T(L.WidgetsUtility.NoteName);
    public string Description => Loc.T(L.WidgetsUtility.NoteDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
    public IReadOnlyList<WidgetOption> Options => options;

    public WidgetRoute Target(in WidgetContext context)
    {
        var note = Resolve(context.InstanceKey, context.Config);
        return note is null
            ? WidgetRoute.To(AppKey, WidgetRouteKind.NewNote, string.Empty)
            : WidgetRoute.To(AppKey, WidgetRouteKind.Note, note.Id.ToString());
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var accent = AppAccents.For(AppKey);
        var headerBottom = WidgetChrome.Header(context, ink, AppKey, L.Apps.Notes, accent);
        var content = WidgetMetrics.Content(context);
        var body = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * context.Scale),
            content.Max);
        var note = Resolve(context.InstanceKey, context.Config);
        if (note is not null)
        {
            var text = TextFor(note);
            var title = text.Title.Length > 0 ? text.Title : Loc.T(L.Notes.Untitled);
            var preview = text.Preview.Length > 0 || text.Title.Length == 0
                ? text.Preview
                : Loc.T(L.Notes.NoAdditionalText);
            DrawNote(context, ink, body, title, text.Title.Length > 0, preview, Stamp(ref text.Stamp, note.UpdatedAt));
            return;
        }

        if (context.Preview)
        {
            var sampleMoment = DateTime.Today.AddHours(SampleHour).AddMinutes(SampleMinute);
            DrawNote(context, ink, body, Loc.T(L.WidgetsUtility.SampleNoteTitle), true,
                Loc.T(L.WidgetsUtility.SampleNoteBody), Stamp(ref sampleStamp, sampleMoment));
            return;
        }

        WidgetChrome.Message(context, ink, body, FontAwesomeIcon.StickyNote, accent,
            Loc.T(L.WidgetsUtility.NoNotes), Loc.T(L.WidgetsUtility.NoNotesHint));
    }

    private static void DrawNote(in WidgetContext context, in WidgetInk ink, Rect body, string title, bool hasTitle,
        string preview, string stamp)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var titleStyle = context.Size == WidgetSize.Small ? WidgetType.Headline : WidgetType.Title;
        var titleHeight = WidgetText.SpacedLineHeight(titleStyle);
        var bodyHeight = WidgetText.SpacedLineHeight(WidgetType.Body);
        var stampHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var stampTop = body.Max.Y - stampHeight;
        var titleLines = WidgetText.Clamp(title, titleStyle, body.Width, context.Size == WidgetSize.Small ? 2 : 1);
        var top = WidgetText.Lines(drawList, titleLines, body.Min, hasTitle ? ink.Primary : ink.Tertiary,
            titleStyle, titleHeight);
        top += WidgetMetrics.RowGap * scale;
        var available = (int)((stampTop - WidgetMetrics.RowGap * scale - top) / bodyHeight);
        if (available > 0 && preview.Length > 0)
        {
            var previewLines = WidgetText.Clamp(preview, WidgetType.Body, body.Width, available);
            WidgetText.Lines(drawList, previewLines, new Vector2(body.Min.X, top), ink.Secondary,
                WidgetType.Body, bodyHeight);
        }

        WidgetText.Draw(drawList, new Vector2(body.Min.X, stampTop), stamp, ink.Tertiary,
            WidgetType.Caption, body.Width);
    }

    private PhoneNote? Resolve(string instanceKey, string config)
    {
        var selection = selections.For(instanceKey);
        if (!selection.Refresh.Due(RefreshMilliseconds) && ReferenceEquals(selection.Config, config))
        {
            return selection.Note;
        }

        selection.Config = config;
        selection.Note = Find(config);
        return selection.Note;
    }

    private PhoneNote? Find(string config)
    {
        var notes = configuration.Notes;
        if (Guid.TryParse(WidgetConfig.Get(config, NoteKey), out var wanted))
        {
            for (var index = 0; index < notes.Count; index++)
            {
                if (notes[index].Id == wanted)
                {
                    return notes[index];
                }
            }
        }

        PhoneNote? latest = null;
        for (var index = 0; index < notes.Count; index++)
        {
            var candidate = notes[index];
            if (candidate.Body.Length == 0)
            {
                continue;
            }

            if (latest is null || candidate.UpdatedAt > latest.UpdatedAt)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    private NoteText TextFor(PhoneNote note)
    {
        ref var slot = ref WidgetCaches.Slot(texts, note.Id);
        slot ??= new NoteText();
        var text = slot;

        if (ReferenceEquals(text.Body, note.Body))
        {
            return text;
        }

        text.Body = note.Body;
        text.Title = note.Title();
        text.Preview = note.Preview();
        return text;
    }

    private static string Stamp(ref CachedText cache, DateTime updatedAt)
    {
        var today = DateTime.Today;
        var key = updatedAt.Ticks ^ (today.Ticks << 1);
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var text = updatedAt.Date == today ? TimeText.Clock(updatedAt) : updatedAt.ToString("d", Loc.Culture);
        return cache.Store(key, text);
    }

    private void FillChoices(List<WidgetChoice> target)
    {
        target.Add(new WidgetChoice(string.Empty, L.WidgetsUtility.MostRecent));
        var notes = configuration.Notes;
        for (var index = 0; index < notes.Count; index++)
        {
            var note = notes[index];
            var title = note.Title();
            target.Add(new WidgetChoice(note.Id.ToString(), title.Length > 0 ? title : Loc.T(L.Notes.Untitled)));
        }
    }

    public void Dispose()
    {
    }
}
