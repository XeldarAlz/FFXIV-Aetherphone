using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Jobs;

internal sealed class JobsApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 2f;
    private const float RowHeight = 64f;
    private const float CardRounding = 18f;
    private const float SectionGap = 12f;
    private const string ColorMenuId = "jobs.color";

    private const ImGuiWindowFlags HostFlags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration |
                                                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                ImGuiWindowFlags.NoMove;

    public string Id => "jobs";
    public string DisplayName => Loc.T(L.Apps.Jobs);
    public string Glyph => "J";
    public int BadgeCount => 0;

    public Vector4 Accent => HexColor.TryParse(configuration.JobsAccentName, out var custom)
        ? custom
        : ThemeCatalog.ResolveAccent(configuration.JobsAccentName);

    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.JobsFor(AppAccents.For("jobs")));
    private readonly DropdownMenu colorMenu = new();
    private JobRoleSection[] sections = Array.Empty<JobRoleSection>();
    private bool loaded;
    private float sinceRefresh;
    private Rect colorButtonRect;
    private bool showHexInput;
    private string hexDigits = string.Empty;
    private string hexColorName = string.Empty;
    private string lastAppliedHexDigits = string.Empty;
    private int hexInputOpenedFrame;
    private int editingSavedColorIndex = -1;

    public JobsApp(GameData gameData, ITextureProvider textures, Configuration configuration, ConfirmService confirm)
    {
        this.gameData = gameData;
        this.textures = textures;
        this.configuration = configuration;
        this.confirm = confirm;
    }

    public void OnOpened() => Rebuild();

    public void OnClosed()
    {
        sections = Array.Empty<JobRoleSection>();
        loaded = false;
        colorMenu.Close();
        showHexInput = false;
        editingSavedColorIndex = -1;
    }

    private void Rebuild()
    {
        sections = gameData.LocalPlayer is null ? Array.Empty<JobRoleSection>() : JobsReader.Build(gameData);
        loaded = true;
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        ui.Theme = theme;
        ui.Palette = AppPalettes.JobsFor(Accent);
        ui.Backdrop(SceneChrome.ScreenFrom(content, theme, scale));
        colorMenu.Gate();
        if (showHexInput)
        {
            UiInteract.BlockThisFrame();
        }

        DrawHeader(content, scale);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        if (!loaded)
        {
            Rebuild();
        }

        if (gameData.LocalPlayer is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Jobs.LogInToView), ui.MutedInk, TextStyles.Subheadline);
        }
        else
        {
            sinceRefresh += ImGui.GetIO().DeltaTime;
            if (sinceRefresh >= RefreshIntervalSeconds)
            {
                Rebuild();
            }

            using (AppSurface.Begin(body))
            {
                if (sections.Length == 0)
                {
                    DrawHint();
                }
                else
                {
                    for (var index = 0; index < sections.Length; index++)
                    {
                        var section = sections[index];
                        ui.SectionHeading(Loc.T(section.Title), index == 0 ? 8f : 4f);
                        DrawSectionCard(section, scale);
                        ImGui.Dummy(new Vector2(0f, SectionGap * scale));
                    }

                    ImGui.Dummy(new Vector2(0f, 8f * scale));
                }
            }
        }

        // Mirrors AethergramApp.DrawRoot: menus are drawn last, after the content they float above.
        DrawColorMenu(content, theme);
        if (showHexInput)
        {
            DrawHexInput(scale);
        }
    }

    private void DrawHeader(Rect content, float scale)
    {
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), DisplayName, ui.TitleInk, 1.15f,
            FontWeight.SemiBold);
        var radius = 15f * scale;
        var buttonCenter = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius, rowCenterY);
        colorButtonRect = new Rect(buttonCenter - new Vector2(radius, radius), buttonCenter + new Vector2(radius, radius));
        if (ui.IconButton(buttonCenter, radius, FontAwesomeIcon.Palette.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.55f, Loc.T(L.Jobs.BackgroundColor)))
        {
            colorMenu.Toggle(ColorMenuId, colorButtonRect);
        }
    }

    private void DrawColorMenu(Rect content, PhoneTheme theme)
    {
        if (!colorMenu.IsOpenFor(ColorMenuId))
        {
            return;
        }

        var savedColors = configuration.JobsCustomColors;
        var savedOffset = ThemeCatalog.Accents.Count;
        var customIndex = savedOffset + savedColors.Count;
        var items = new DropdownMenu.Item[customIndex + 1];
        for (var index = 0; index < ThemeCatalog.Accents.Count; index++)
        {
            var swatch = ThemeCatalog.Accents[index];
            items[index] = new DropdownMenu.Item(CatalogLabels.Accent(swatch.Name),
                Selected: swatch.Name == configuration.JobsAccentName);
        }

        for (var index = 0; index < savedColors.Count; index++)
        {
            items[savedOffset + index] = new DropdownMenu.Item(savedColors[index].Name,
                Selected: savedColors[index].Hex == configuration.JobsAccentName, CanEdit: true, CanDelete: true);
        }

        items[customIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.CustomColor),
            Glyph: FontAwesomeIcon.EyeDropper.ToIconString());

        var picked = colorMenu.Draw(content, theme, items, out var rowAction);
        if (picked < 0)
        {
            return;
        }

        if (picked == customIndex)
        {
            OpenHexEditor(-1);
            return;
        }

        if (picked >= savedOffset && rowAction == DropdownMenu.RowAction.Delete)
        {
            DeleteSavedColor(picked - savedOffset);
            return;
        }

        if (picked >= savedOffset && rowAction == DropdownMenu.RowAction.Edit)
        {
            OpenHexEditor(picked - savedOffset);
            return;
        }

        configuration.JobsAccentName = picked < savedOffset
            ? ThemeCatalog.Accents[picked].Name
            : savedColors[picked - savedOffset].Hex;
        configuration.Save();
    }

    private void OpenHexEditor(int savedIndex)
    {
        editingSavedColorIndex = savedIndex;
        var startColor = savedIndex >= 0 &&
                          HexColor.TryParse(configuration.JobsCustomColors[savedIndex].Hex, out var saved)
            ? saved
            : Accent;
        hexDigits = HexColor.ToDigits(startColor);
        hexColorName = savedIndex >= 0 ? configuration.JobsCustomColors[savedIndex].Name : string.Empty;
        lastAppliedHexDigits = string.Empty;
        hexInputOpenedFrame = ImGui.GetFrameCount();
        showHexInput = true;
    }

    private void DeleteSavedColor(int index)
    {
        if (index < 0 || index >= configuration.JobsCustomColors.Count)
        {
            return;
        }

        var entry = configuration.JobsCustomColors[index];
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Jobs.DeleteColorConfirm, entry.Name),
            ConfirmLabel = Loc.T(L.Jobs.DeleteColor),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () =>
            {
                configuration.JobsCustomColors.Remove(entry);
                configuration.Save();
            },
        });
    }

    private void DrawHexInput(float scale)
    {
        var width = 220f * scale;
        var pad = Metrics.Space.Lg * scale;
        var swatchRadius = 14f * scale;
        var hexRowHeight = swatchRadius * 2f;
        var nameRowHeight = 26f * scale;
        var saveRowHeight = 30f * scale;
        var rowGap = 10f * scale;
        var height = pad * 2f + hexRowHeight + rowGap + nameRowHeight + rowGap + saveRowHeight;
        var top = colorButtonRect.Max.Y + 8f * scale;
        var min = new Vector2(colorButtonRect.Max.X - width, top);
        var max = min + new Vector2(width, height);
        var justOpened = hexInputOpenedFrame == ImGui.GetFrameCount();

        var hexRowTop = min.Y + pad;
        var nameRowTop = hexRowTop + hexRowHeight + rowGap;
        var saveRowTop = nameRowTop + nameRowHeight + rowGap;

        // Real (but fully transparent) InputText widgets host the actual keyboard capture: ImGui/Dalamud only
        // routes keystrokes away from the game while an active widget holds focus, which our own hand-drawn
        // rectangles never would. The visible card is drawn separately below, on the foreground list, so it
        // still stays on top of the job cards regardless of these host windows' place in the draw order.
        using (ImRaii.PushColor(ImGuiCol.FrameBg, default(Vector4))
                   .Push(ImGuiCol.FrameBgHovered, default(Vector4))
                   .Push(ImGuiCol.FrameBgActive, default(Vector4))
                   .Push(ImGuiCol.Text, default(Vector4))
                   .Push(ImGuiCol.Border, default(Vector4)))
        {
            var hexHostMin = new Vector2(min.X + pad + hexRowHeight + 12f * scale, hexRowTop);
            var hexHostSize = new Vector2(max.X - pad - hexHostMin.X, hexRowHeight);
            ImGui.SetCursorScreenPos(hexHostMin);
            using (ImRaii.Child("##jobsHexHost", hexHostSize, false, HostFlags))
            {
                if (justOpened)
                {
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText("##jobsHexField", ref hexDigits, 6,
                    ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase);
            }

            var nameHostMin = new Vector2(min.X + pad, nameRowTop);
            var nameHostSize = new Vector2(width - pad * 2f, nameRowHeight);
            ImGui.SetCursorScreenPos(nameHostMin);
            using (ImRaii.Child("##jobsColorNameHost", nameHostSize, false, HostFlags))
            {
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText("##jobsColorNameField", ref hexColorName, 24);
            }
        }

        if (hexDigits.Length == 6 && hexDigits != lastAppliedHexDigits)
        {
            lastAppliedHexDigits = hexDigits;
            configuration.JobsAccentName = "#" + hexDigits;
            configuration.Save();
        }

        var drawList = ImGui.GetForegroundDrawList();
        Elevation.Floating(drawList, min, max, 16f * scale, scale, 0.85f);
        ui.Card(drawList, min, max, 16f * scale, elevated: true);

        var swatchCenter = new Vector2(min.X + pad + swatchRadius, hexRowTop + swatchRadius);
        var validHex = HexColor.TryParse(hexDigits, out var previewColor);
        drawList.AddCircleFilled(swatchCenter, swatchRadius,
            ImGui.GetColorU32(validHex ? previewColor : Palette.WithAlpha(ui.MutedInk, 0.25f)), 24);
        drawList.AddCircle(swatchCenter, swatchRadius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.18f)), 24,
            1.4f * scale);

        var hexTextLeft = swatchCenter.X + swatchRadius + 12f * scale;
        var hexDisplay = "#" + hexDigits.PadRight(6, '_');
        Typography.Draw(drawList, new Vector2(hexTextLeft, hexRowTop + hexRowHeight * 0.5f - 8f * scale), hexDisplay,
            ui.TitleInk, TextStyles.BodyEmphasized);

        var nameDisplay = hexColorName.Length > 0 ? hexColorName : Loc.T(L.Jobs.ColorNamePlaceholder);
        var nameInk = hexColorName.Length > 0 ? ui.TitleInk : Palette.WithAlpha(ui.MutedInk, 0.7f);
        Typography.Draw(drawList, new Vector2(min.X + pad, nameRowTop + nameRowHeight * 0.5f - 8f * scale), nameDisplay,
            nameInk, TextStyles.Body);

        DrawSaveButton(drawList, new Rect(new Vector2(max.X - pad - 74f * scale, saveRowTop),
            new Vector2(max.X - pad, saveRowTop + saveRowHeight)), validHex, scale);

        if (!justOpened && (ImGui.IsKeyPressed(ImGuiKey.Escape) || ImGui.IsKeyPressed(ImGuiKey.Enter) ||
                (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max))))
        {
            showHexInput = false;
        }
    }

    private void DrawSaveButton(ImDrawListPtr drawList, Rect rect, bool enabled, float scale)
    {
        var editing = editingSavedColorIndex >= 0 && editingSavedColorIndex < configuration.JobsCustomColors.Count;
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fillAlpha = !enabled ? 0.08f : hovered ? 0.30f : 0.20f;
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, fillAlpha)),
            rect.Height * 0.5f);
        var label = Loc.T(editing ? L.Jobs.UpdateColor : L.Jobs.SaveColor);
        var labelSize = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var ink = enabled ? ui.Accent : Palette.WithAlpha(ui.MutedInk, 0.5f);
        Typography.Draw(drawList, rect.Center - labelSize * 0.5f, label, ink, TextStyles.SubheadlineEmphasized);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var name = hexColorName.Trim();
        if (name.Length == 0)
        {
            name = "#" + hexDigits;
        }

        if (editing)
        {
            var entry = configuration.JobsCustomColors[editingSavedColorIndex];
            entry.Name = name;
            entry.Hex = "#" + hexDigits;
        }
        else
        {
            configuration.JobsCustomColors.Add(new JobsCustomColor { Name = name, Hex = "#" + hexDigits });
        }

        configuration.Save();
        showHexInput = false;
    }

    private void DrawHint()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 24f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 4f * scale),
            Loc.T(L.Jobs.NoGearsets), ui.MutedInk, TextStyles.Subheadline, width - 40f * scale);
    }

    private void DrawSectionCard(JobRoleSection section, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var rowCount = section.Entries.Length;
        var rowHeight = RowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowCount * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, CardRounding * scale, elevated: true);

        var padding = 16f * scale;
        var separator = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f));
        for (var index = 0; index < rowCount; index++)
        {
            var rowTop = min.Y + index * rowHeight;
            var rowRect = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + rowHeight));
            var contentRect = new Rect(new Vector2(min.X + padding, rowTop), new Vector2(max.X - padding, rowTop + rowHeight));
            DrawJobRow(drawList, rowRect, contentRect, section.Entries[index], scale);
            if (index > 0)
            {
                drawList.AddLine(new Vector2(min.X + padding, rowTop), new Vector2(max.X - padding, rowTop), separator,
                    Metrics.Stroke.Hairline);
            }
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, rowCount * rowHeight));
    }

    private void DrawJobRow(ImDrawListPtr drawList, Rect rowRect, Rect contentRect, JobEntry job, float scale)
    {
        var hovered = UiInteract.Hover(rowRect.Min, rowRect.Max);
        if (hovered)
        {
            var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.14f : 0.07f;
            drawList.AddRectFilled(rowRect.Min, rowRect.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        }

        var iconSize = 42f * scale;
        var iconMin = new Vector2(contentRect.Min.X, contentRect.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        Squircle.Fill(drawList, iconMin, iconMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)));
        if (job.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(job.IconId)).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 10f * scale);
        }

        Material.EdgeSquircle(drawList, iconMin, iconMax, 10f * scale, scale, 0.5f);

        var textLeft = iconMax.X + 14f * scale;
        var textRight = contentRect.Max.X - (job.IsActive ? 78f * scale : 0f);
        var name = Typography.FitText(job.Name, textRight - textLeft, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, contentRect.Min.Y + 12f * scale), name, ui.TitleInk,
            TextStyles.Headline);
        var subtitle = job.ItemLevel >= 0
            ? Loc.T(L.Jobs.LevelItemLevel, job.Abbreviation, job.Level, job.ItemLevel)
            : Loc.T(L.Jobs.LevelOnly, job.Abbreviation, job.Level);
        var fittedSubtitle = Typography.FitText(subtitle, textRight - textLeft, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, contentRect.Min.Y + 36f * scale), fittedSubtitle, ui.MutedInk,
            TextStyles.Footnote);

        if (job.IsActive)
        {
            DrawActiveBadge(drawList, new Vector2(contentRect.Max.X, contentRect.Center.Y), scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!job.IsActive && UiInteract.Click(rowRect.Min, rowRect.Max, hovered) && TryEquip(job))
        {
            Rebuild();
        }
    }

    private bool TryEquip(JobEntry job) =>
        job.Kind == JobEntryKind.Gearset
            ? GearsetActions.Equip(job.GearsetId)
            : GearsetActions.EquipTool(gameData, job.ClassJobId);

    private void DrawActiveBadge(ImDrawListPtr drawList, Vector2 rightCenter, float scale)
    {
        var text = Loc.T(L.Jobs.Active);
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var padX = 8f * scale;
        var padY = 4f * scale;
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        var min = new Vector2(max.X - width, rightCenter.Y - height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.2f)));
        Typography.Draw(drawList, new Vector2(min.X + padX, rightCenter.Y - textSize.Y * 0.5f), text, ui.Accent,
            TextStyles.Caption2);
    }

    public void Dispose()
    {
    }
}
