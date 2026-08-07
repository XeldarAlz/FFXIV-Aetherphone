using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float PluginRowHeight = 60f;
    private const float CommandRowHeight = 54f;

    private readonly Action<PluginEntry> openPluginDetail;
    private readonly Action<PluginEntry> pickStepPlugin;
    private readonly Action<PluginEntry> pickIconPlugin;

    private string pluginQuery = string.Empty;
    private string detailPlugin = string.Empty;
    private bool pickingIcon;

    private void DrawPluginsTab(Rect content, float bodyTop, float scale)
    {
        var margin = Metrics.Space.Lg * scale;
        var searchRow = new Rect(new Vector2(content.Min.X + margin, bodyTop),
            new Vector2(content.Max.X - margin, bodyTop + 36f * scale));
        SearchField.Draw(searchRow, "##shortcuts.pluginSearch", Loc.T(L.Shortcuts.SearchPlugins), ref pluginQuery,
            ui.Palette);

        var body = new Rect(new Vector2(content.Min.X, searchRow.Max.Y + Metrics.Space.Sm * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawPluginList(body, scale, openPluginDetail);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void OpenPluginPicker(bool forIcon)
    {
        pluginQuery = string.Empty;
        pickingIcon = forIcon;
        router.Push(ShortcutsScreen.PluginPicker);
    }

    private void DrawPluginPicker(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(pickingIcon ? L.Shortcuts.ChooseIcon : L.Shortcuts.ChoosePlugin), back);

        var margin = Metrics.Space.Lg * scale;
        var searchTop = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Xs * scale;
        var searchRow = new Rect(new Vector2(content.Min.X + margin, searchTop),
            new Vector2(content.Max.X - margin, searchTop + 36f * scale));
        SearchField.Draw(searchRow, "##shortcuts.pickerSearch", Loc.T(L.Shortcuts.SearchPlugins), ref pluginQuery,
            ui.Palette);

        var body = new Rect(new Vector2(content.Min.X, searchRow.Max.Y + Metrics.Space.Sm * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawPluginList(body, scale, pickingIcon ? pickIconPlugin : pickStepPlugin);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawPluginList(Rect body, float scale, Action<PluginEntry> onPick)
    {
        var entries = catalog.Entries;
        var matches = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            if (Matches(entries[index], pluginQuery))
            {
                matches++;
            }
        }

        if (matches == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                Loc.T(L.Shortcuts.NoPluginsFound), ui.MutedInk, TextStyles.Subheadline);
            return;
        }

        var card = GroupCard.Begin(theme, matches, PluginRowHeight);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!Matches(entry, pluginQuery))
            {
                continue;
            }

            DrawPluginRow(card.NextRow(), entry, scale, onPick);
        }

        card.End();
    }

    private static bool Matches(PluginEntry entry, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (entry.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            entry.InternalName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            entry.Punchline.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < entry.Commands.Count; index++)
        {
            if (entry.Commands[index].Command.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawPluginRow(Rect row, PluginEntry entry, float scale, Action<PluginEntry> onPick)
    {
        var tile = 36f * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        DrawPluginTile(tileCenter, tile, entry, scale);

        var chevronX = row.Max.X - 10f * scale;
        var textLeft = row.Min.X + tile + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, chevronX - 14f * scale - textLeft);
        var ink = entry.Loaded ? ui.TitleInk : ui.MutedInk;
        Marquee.DrawLeftAuto("shortcuts.plugin." + entry.InternalName, entry.Name, textLeft,
            row.Center.Y - 15f * scale, textWidth, TextStyles.Headline, ink);
        Marquee.DrawLeftAuto("shortcuts.plugin.sub." + entry.InternalName, Subtitle(entry), textLeft,
            row.Center.Y + 4f * scale, textWidth, TextStyles.Footnote, ui.MutedInk);

        AppSkin.Icon(new Vector2(chevronX, row.Center.Y), FontAwesomeIcon.ChevronRight.ToIconString(),
            Palette.WithAlpha(ui.MutedInk, 0.7f), 0.6f);
        if (UiInteract.HoverClick(row.Min, row.Max))
        {
            onPick(entry);
        }
    }

    private string Subtitle(PluginEntry entry)
    {
        if (!entry.Loaded)
        {
            return Loc.T(L.Shortcuts.PluginDisabled);
        }

        if (entry.Commands.Count > 0)
        {
            return Loc.T(L.Shortcuts.PluginCommandCount, entry.Commands.Count);
        }

        return entry.Punchline.Length > 0 ? entry.Punchline : entry.InternalName;
    }

    private void DrawPluginTile(Vector2 center, float size, PluginEntry entry, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * 0.26f;
        var icon = catalog.Icon(entry.InternalName);
        if (icon is not null)
        {
            Squircle.FillImage(drawList, min, max, radius, icon.Handle,
                entry.Loaded ? 0xFFFFFFFFu : 0x80FFFFFFu);
            return;
        }

        var surface = IconTile.Surface(AccentFor(entry.InternalName));
        IconTile.FillShaded(drawList, min, max, radius, surface, entry.Loaded ? 1f : 0.55f);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        var monogram = ShortcutStore.Monogram(entry.Name);
        var measured = Typography.Measure(monogram, TextStyles.Title2);
        var glyphScale = measured.Y > 0f ? size * 0.42f / measured.Y : 1f;
        Typography.DrawCentered(drawList, center, monogram, new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Title2.Scale * glyphScale, FontWeight.SemiBold);
    }

    private static Vector4 AccentFor(string internalName)
    {
        var hash = 2166136261u;
        for (var index = 0; index < internalName.Length; index++)
        {
            hash = (hash ^ internalName[index]) * 16777619u;
        }

        return ShortcutPalette.Wheel[(int)(hash % (uint)ShortcutPalette.Wheel.Length)];
    }

    private void OpenPluginDetail(PluginEntry entry)
    {
        detailPlugin = entry.InternalName;
        router.Push(ShortcutsScreen.Plugin);
    }

    private void DrawPluginDetail(Rect content, float scale)
    {
        var entry = catalog.Find(detailPlugin);
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, entry?.Name ?? Loc.T(L.Shortcuts.TabPlugins), back);
        if (entry is null)
        {
            return;
        }

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawPluginHero(entry, scale);
            DrawPluginActions(entry, scale);
            DrawPluginCommands(body, entry, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawPluginHero(PluginEntry entry, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var tile = 62f * scale;
        var height = tile + Metrics.Space.Md * scale;
        DrawPluginTile(new Vector2(origin.X + tile * 0.5f, origin.Y + tile * 0.5f), tile, entry, scale);

        var textLeft = origin.X + tile + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, origin.X + width - textLeft);
        Marquee.DrawLeftAuto("shortcuts.detail.name", entry.Name, textLeft, origin.Y + 6f * scale, textWidth,
            TextStyles.Title3, ui.TitleInk);
        if (entry.Author.Length > 0)
        {
            Marquee.DrawLeftAuto("shortcuts.detail.author", Loc.T(L.Shortcuts.PluginBy, entry.Author), textLeft,
                origin.Y + 27f * scale, textWidth, TextStyles.Footnote, ui.MutedInk);
        }

        if (entry.Punchline.Length > 0)
        {
            Marquee.DrawLeftAuto("shortcuts.detail.punch", entry.Punchline, textLeft, origin.Y + 45f * scale,
                textWidth, TextStyles.Footnote, ui.BodyInk);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawPluginActions(PluginEntry entry, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 36f * scale;
        var gap = Metrics.Space.Sm * scale;
        var canOpen = entry.Loaded && entry.HasMainUi;
        var canConfigure = entry.Loaded && entry.HasConfigUi;
        var buttons = 1 + (canOpen ? 1 : 0) + (canConfigure ? 1 : 0);
        var buttonWidth = (width - gap * (buttons - 1)) / buttons;
        var cursorX = origin.X;

        var addRect = new Rect(new Vector2(cursorX, origin.Y), new Vector2(cursorX + buttonWidth, origin.Y + height));
        if (ui.PillButton(addRect, Loc.T(L.Shortcuts.AddToHome), true))
        {
            CreateLauncherShortcut(entry);
        }

        cursorX += buttonWidth + gap;
        if (canOpen)
        {
            var openRect = new Rect(new Vector2(cursorX, origin.Y),
                new Vector2(cursorX + buttonWidth, origin.Y + height));
            if (ui.PillButton(openRect, Loc.T(L.Shortcuts.OpenPlugin), false))
            {
                PluginCatalog.TryOpenMainUi(entry.InternalName);
            }

            cursorX += buttonWidth + gap;
        }

        if (canConfigure)
        {
            var configRect = new Rect(new Vector2(cursorX, origin.Y),
                new Vector2(cursorX + buttonWidth, origin.Y + height));
            if (ui.PillButton(configRect, Loc.T(L.Shortcuts.PluginSettings), false))
            {
                PluginCatalog.TryOpenConfigUi(entry.InternalName);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private void DrawPluginCommands(Rect body, PluginEntry entry, float scale)
    {
        ui.SectionLabel(Loc.T(L.Shortcuts.Commands), TextStyles.FootnoteEmphasized, 6f);
        if (entry.Commands.Count == 0)
        {
            ui.HelpText(Loc.T(L.Shortcuts.NoCommands));
            return;
        }

        ui.HelpText(Loc.T(L.Shortcuts.CommandsHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var card = GroupCard.Begin(theme, entry.Commands.Count, CommandRowHeight);
        for (var index = 0; index < entry.Commands.Count; index++)
        {
            DrawCommandRow(card.NextRow(), entry.Commands[index], scale);
        }

        card.End();
    }

    private void DrawCommandRow(Rect row, PluginCommand command, float scale)
    {
        var plusRadius = 14f * scale;
        var plusCenter = new Vector2(row.Max.X - plusRadius, row.Center.Y);
        var textWidth = MathF.Max(1f, plusCenter.X - plusRadius - 8f * scale - row.Min.X);
        var hasHelp = command.Help.Length > 0;
        var titleY = hasHelp ? row.Center.Y - 15f * scale : row.Center.Y - 9f * scale;
        Marquee.DrawLeftAuto("shortcuts.cmd." + command.Command, command.Command, row.Min.X, titleY, textWidth,
            TextStyles.BodyEmphasized, ui.TitleInk);
        if (hasHelp)
        {
            Marquee.DrawLeftAuto("shortcuts.cmd.help." + command.Command, command.Help, row.Min.X,
                row.Center.Y + 4f * scale, textWidth, TextStyles.Footnote, ui.MutedInk);
        }

        if (ui.IconButton(plusCenter, plusRadius, FontAwesomeIcon.Plus.ToIconString(), ui.Accent,
                Palette.WithAlpha(ui.Accent, 0.16f), 0.58f, Loc.T(L.Shortcuts.NewFromCommand)))
        {
            CreateCommandShortcut(command);
        }
    }

    private void CreateLauncherShortcut(PluginEntry entry)
    {
        if (WarnIfFull())
        {
            return;
        }

        draft = new ShortcutEntry
        {
            Name = entry.Name,
            IconPlugin = entry.InternalName,
            Tint = HexColor.ToDigits(AccentFor(entry.InternalName)),
        };
        draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.OpenPlugin, Text = entry.InternalName });
        draftId = Guid.Empty;
        draftPinned = true;
        router.Push(ShortcutsScreen.Editor);
    }

    private void CreateCommandShortcut(PluginCommand command)
    {
        if (WarnIfFull())
        {
            return;
        }

        draft = new ShortcutEntry { Name = command.Command.TrimStart('/') };
        draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = command.Command });
        draftId = Guid.Empty;
        draftPinned = false;
        router.Push(ShortcutsScreen.Editor);
    }

    private void AddOpenPluginStep(PluginEntry entry)
    {
        if (draft is null || draft.Steps.Count >= ShortcutStore.MaxSteps)
        {
            router.Pop();
            return;
        }

        draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.OpenPlugin, Text = entry.InternalName });
        router.Pop();
    }

    private void UsePluginIcon(PluginEntry entry)
    {
        if (draft is not null)
        {
            draft.IconPlugin = entry.InternalName;
        }

        router.Pop();
    }
}
