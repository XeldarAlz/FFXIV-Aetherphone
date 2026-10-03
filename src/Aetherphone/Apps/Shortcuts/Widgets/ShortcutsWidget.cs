using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Shortcuts.Widgets;

internal sealed class ShortcutsWidget : IHomeWidget
{
    private const string AppKey = "shortcuts";
    private const int MaxSlots = 8;
    private const int RefreshMilliseconds = 1000;
    private const float GlyphUnits = 20f;
    private const float RingUnits = 9f;
    private const float GradientShift = 0.12f;
    private const float TintedFillAlpha = 0.5f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly string[] SlotKeys =
        { "slot1", "slot2", "slot3", "slot4", "slot5", "slot6", "slot7", "slot8" };
    private static readonly LocString[] SlotLabels =
    {
        L.WidgetsUtility.ShortcutSlot1, L.WidgetsUtility.ShortcutSlot2, L.WidgetsUtility.ShortcutSlot3,
        L.WidgetsUtility.ShortcutSlot4, L.WidgetsUtility.ShortcutSlot5, L.WidgetsUtility.ShortcutSlot6,
        L.WidgetsUtility.ShortcutSlot7, L.WidgetsUtility.ShortcutSlot8,
    };

    private readonly struct Tile
    {
        public readonly ShortcutEntry? Entry;
        public readonly string Name;
        public readonly int Glyph;
        public readonly Vector4 Tint;
        public readonly IDalamudTextureWrap? Icon;

        public Tile(ShortcutEntry? entry, string name, int glyph, Vector4 tint, IDalamudTextureWrap? icon)
        {
            Entry = entry;
            Name = name;
            Glyph = glyph;
            Tint = tint;
            Icon = icon;
        }
    }

    private sealed class Slots
    {
        public readonly ShortcutEntry?[] Entries = new ShortcutEntry?[MaxSlots];
        public string Config = string.Empty;
        public int Stamp = -1;
        public int Count;
        public WidgetRefresh Refresh;
    }

    private readonly ShortcutStore store;
    private readonly ShortcutRunner runner;
    private readonly WidgetOption[] options;
    private readonly WidgetStates<Slots> slotsByInstance = new();
    private readonly Dictionary<string, string> monograms = new(StringComparer.Ordinal);
    private int stamp;

    public ShortcutsWidget(ShortcutStore store, ShortcutRunner runner)
    {
        this.store = store;
        this.runner = runner;
        options = new WidgetOption[MaxSlots];
        for (var index = 0; index < MaxSlots; index++)
        {
            options[index] = new WidgetOption(SlotKeys[index], SlotLabels[index], FillChoices);
        }

        store.Changed += OnStoreChanged;
    }

    public string Id => "shortcuts.grid";
    public string DisplayName => Loc.T(L.Apps.Shortcuts);
    public string Description => Loc.T(L.WidgetsUtility.ShortcutsDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
    public IReadOnlyList<WidgetOption> Options => options;

    public void Draw(in WidgetContext context)
    {
        var capacity = context.Size switch
        {
            WidgetSize.Small => 1,
            WidgetSize.Medium => 4,
            _ => MaxSlots,
        };
        var slots = Resolve(context.InstanceKey, context.Config);
        var sample = slots.Count == 0 && context.Preview;
        var count = sample ? capacity : Math.Min(capacity, slots.Count);
        var ink = WidgetInk.From(context);
        var run = runner.Snapshot();
        if (count == 0)
        {
            WidgetChrome.Container(context);
            WidgetChrome.Message(context, ink, WidgetMetrics.Content(context), FontAwesomeIcon.Bolt,
                AppAccents.For(AppKey), Loc.T(L.WidgetsUtility.NoShortcuts), Loc.T(L.WidgetsUtility.NoShortcutsHint));
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            var single = sample ? SampleTile(0) : TileFor(slots.Entries[0]!);
            DrawSmall(context, ink, single, run);
            return;
        }

        WidgetChrome.Container(context);
        var content = WidgetMetrics.Content(context);
        var gutter = WidgetMetrics.Gutter * context.Scale;
        var rows = capacity / 2;
        var tileWidth = (content.Width - gutter) * 0.5f;
        var tileHeight = (content.Height - gutter * (rows - 1)) / rows;
        for (var index = 0; index < count; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(content.Min.X + column * (tileWidth + gutter),
                content.Min.Y + row * (tileHeight + gutter));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var tile = sample ? SampleTile(index) : TileFor(slots.Entries[index]!);
            DrawGridTile(context, ink, index, rect, tile, run);
        }
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, in Tile tile, in ShortcutRunView run)
    {
        var colored = ink.Mode is WidgetMode.FullColor or WidgetMode.Dark;
        if (colored)
        {
            WidgetChrome.Container(context, Palette.Lighten(tile.Tint, GradientShift),
                Palette.Darken(tile.Tint, GradientShift));
        }
        else
        {
            WidgetChrome.Container(context);
        }

        var fired = WidgetControls.Pressable(context, 0, context.Bounds, out _, out var pressScale);
        if (fired)
        {
            Activate(tile, run);
        }

        var content = WidgetControls.Scaled(WidgetMetrics.Content(context), pressScale);
        var textColor = colored ? ink.Fade(White) : ink.Primary;
        DrawFace(context, ink, content, tile, run, textColor, WidgetType.Title, 2);
    }

    private void DrawGridTile(in WidgetContext context, in WidgetInk ink, int index, Rect rect, in Tile tile,
        in ShortcutRunView run)
    {
        var fired = WidgetControls.Pressable(context, index, rect, out var hovered, out var pressScale);
        if (fired)
        {
            Activate(tile, run);
        }

        var drawList = context.DrawList;
        var drawRect = WidgetControls.Scaled(rect, pressScale);
        var radius = WidgetMetrics.InnerRadius(context);
        var colored = ink.Mode is WidgetMode.FullColor or WidgetMode.Dark;
        Vector4 top;
        Vector4 bottom;
        if (colored)
        {
            top = ink.Accent(Palette.Lighten(tile.Tint, GradientShift + (hovered ? 0.05f : 0f)));
            bottom = ink.Accent(Palette.Darken(tile.Tint, GradientShift));
        }
        else if (ink.Mode == WidgetMode.Tinted)
        {
            top = ink.Accent(tile.Tint) with { W = TintedFillAlpha * ink.Opacity };
            bottom = top;
        }
        else
        {
            top = hovered ? ink.Fill with { W = MathF.Min(1f, ink.Fill.W * 1.4f) } : ink.Fill;
            bottom = top;
        }

        Squircle.FillVerticalGradient(drawList, drawRect.Min, drawRect.Max, radius, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        var inset = 10f * context.Scale;
        var face = new Rect(drawRect.Min + new Vector2(inset), drawRect.Max - new Vector2(inset));
        var textColor = colored ? ink.Fade(White) : ink.Primary;
        DrawFace(context, ink, face, tile, run, textColor, WidgetType.Headline, 2);
    }

    private void DrawFace(in WidgetContext context, in WidgetInk ink, Rect face, in Tile tile,
        in ShortcutRunView run, Vector4 textColor, in TextStyle nameStyle, int maxLines)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var glyph = GlyphUnits * scale;
        var glyphCenter = new Vector2(face.Min.X + glyph * 0.5f, face.Min.Y + glyph * 0.5f);
        DrawGlyph(drawList, glyphCenter, glyph, tile, textColor, ink);

        var running = tile.Entry is not null && run.IsRunning && run.Id == tile.Entry.Id;
        if (running)
        {
            var ringRadius = RingUnits * scale;
            var ringCenter = new Vector2(face.Max.X - ringRadius, glyphCenter.Y);
            var thickness = 2.2f * scale;
            WidgetChrome.Ring(drawList, ringCenter, ringRadius, thickness, Math.Clamp(run.Progress, 0.06f, 1f), textColor,
                textColor with { W = textColor.W * 0.3f });
            var stop = ringRadius * 0.36f;
            drawList.AddRectFilled(ringCenter - new Vector2(stop), ringCenter + new Vector2(stop),
                ImGui.GetColorU32(textColor), 1.5f * scale);
        }

        var lineHeight = WidgetText.SpacedLineHeight(nameStyle);
        var lines = WidgetText.Clamp(tile.Name, nameStyle, face.Width, maxLines);
        var available = (int)((face.Max.Y - face.Min.Y - glyph) / lineHeight);
        if (available <= 0)
        {
            return;
        }

        if (lines.Length > available)
        {
            lines = WidgetText.Clamp(tile.Name, nameStyle, face.Width, available);
        }

        WidgetText.Lines(drawList, lines, new Vector2(face.Min.X, face.Max.Y - lines.Length * lineHeight),
            textColor, nameStyle, lineHeight);
    }

    private void DrawGlyph(ImDrawListPtr drawList, Vector2 center, float size, in Tile tile, Vector4 color,
        in WidgetInk ink)
    {
        if (tile.Icon is not null)
        {
            var half = new Vector2(size * 0.55f);
            Squircle.FillImage(drawList, center - half, center + half, size * 0.3f, tile.Icon.Handle,
                ImGui.GetColorU32(ink.ImageTint));
            return;
        }

        if (tile.Glyph != 0)
        {
            ProgressRing.CenterIcon(drawList, center, (FontAwesomeIcon)tile.Glyph, color, size * 0.9f);
            return;
        }

        var monogram = Monogram(tile.Name);
        var measured = Typography.Measure(monogram, WidgetType.Headline);
        Typography.Draw(drawList, center - measured * 0.5f, monogram, color, WidgetType.Headline);
    }

    private void Activate(in Tile tile, in ShortcutRunView run)
    {
        if (tile.Entry is null)
        {
            return;
        }

        if (run.IsRunning && run.Id == tile.Entry.Id)
        {
            runner.Cancel();
            return;
        }

        runner.Run(tile.Entry);
    }

    private Tile TileFor(ShortcutEntry entry)
    {
        var name = entry.Name.Length > 0 ? entry.Name : Loc.T(L.Shortcuts.Untitled);
        return new Tile(entry, name, entry.Glyph, ShortcutTint.Resolve(entry.Tint), store.Icon(entry));
    }

    private static Tile SampleTile(int index)
    {
        var slot = index % WidgetSamples.Shortcuts.Length;
        return new Tile(null, Loc.T(WidgetSamples.Shortcuts[slot]), (int)WidgetSamples.ShortcutGlyphs[slot],
            WidgetSamples.ShortcutTints[slot], null);
    }

    private string Monogram(string name)
    {
        ref var monogram = ref WidgetCaches.Slot(monograms, name);
        monogram ??= ShortcutStore.Monogram(name);
        return monogram;
    }

    private Slots Resolve(string instanceKey, string config)
    {
        var slots = slotsByInstance.For(instanceKey);
        if (!slots.Refresh.Due(RefreshMilliseconds) && slots.Stamp == stamp && ReferenceEquals(slots.Config, config))
        {
            return slots;
        }

        slots.Stamp = stamp;
        slots.Config = config;
        Fill(slots, config);
        return slots;
    }

    private void Fill(Slots slots, string config)
    {
        var entries = slots.Entries;
        Array.Clear(entries);
        var all = store.All;
        for (var index = 0; index < MaxSlots; index++)
        {
            if (Guid.TryParse(WidgetConfig.Get(config, SlotKeys[index]), out var wanted))
            {
                entries[index] = store.Find(wanted);
            }
        }

        var cursor = 0;
        for (var index = 0; index < MaxSlots; index++)
        {
            if (entries[index] is not null)
            {
                continue;
            }

            while (cursor < all.Count && Chosen(entries, all[cursor]))
            {
                cursor++;
            }

            if (cursor >= all.Count)
            {
                break;
            }

            entries[index] = all[cursor++];
        }

        var count = 0;
        for (var index = 0; index < MaxSlots; index++)
        {
            if (entries[index] is not null)
            {
                entries[count++] = entries[index];
            }
        }

        for (var index = count; index < MaxSlots; index++)
        {
            entries[index] = null;
        }

        slots.Count = count;
    }

    private static bool Chosen(ShortcutEntry?[] entries, ShortcutEntry candidate)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            if (ReferenceEquals(entries[index], candidate))
            {
                return true;
            }
        }

        return false;
    }

    private void FillChoices(List<WidgetChoice> target)
    {
        target.Add(new WidgetChoice(string.Empty, L.WidgetsUtility.Automatic));
        var all = store.All;
        for (var index = 0; index < all.Count; index++)
        {
            var entry = all[index];
            target.Add(new WidgetChoice(entry.Id.ToString(),
                entry.Name.Length > 0 ? entry.Name : Loc.T(L.Shortcuts.Untitled)));
        }
    }

    private void OnStoreChanged() => stamp++;

    public void Dispose()
    {
        store.Changed -= OnStoreChanged;
    }
}
