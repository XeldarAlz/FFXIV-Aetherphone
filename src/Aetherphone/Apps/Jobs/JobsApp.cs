using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Jobs;

internal sealed partial class JobsApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 2f;
    private const float PendingEquipIntervalSeconds = 0.1f;
    private const float PendingEquipTimeoutSeconds = 5f;
    private const float BottomBreathing = 28f;
    private const string ColorMenuId = "jobs.color";
    private const string CategoryMenuId = "jobs.categories";
    private const string RowMenuId = "jobs.gearsetMenu";

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
    private readonly CharacterWatch characterWatch;
    private readonly AppSkin ui = new(AppPalettes.JobsFor(AppAccents.For("jobs")));
    private readonly DropdownMenu menu = new();
    private readonly ViewRouter<JobsView> router;
    private readonly RouterDraw<JobsView> drawView;
    private readonly Action back;
    private JobsSnapshot snapshot = JobsSnapshot.Empty;
    private Spring[] tileFills = Array.Empty<Spring>();
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect content;
    private Vector4 paletteAccent;
    private bool loaded;
    private float sinceRefresh;
    private string snapshotLanguage = string.Empty;
    private ulong snapshotContentId;
    private int pendingGearsetId = -1;
    private float sincePendingEquip;
    private int menuGearsetId = -1;
    private bool categoryEditorOpen;
    private Rect colorButtonRect;
    private Rect categoriesButtonRect;
    private bool pickerOpen;

    public JobsApp(GameData gameData, ITextureProvider textures, Configuration configuration, ConfirmService confirm,
        CharacterWatch characterWatch)
    {
        this.gameData = gameData;
        this.textures = textures;
        this.configuration = configuration;
        this.confirm = confirm;
        this.characterWatch = characterWatch;
        router = new ViewRouter<JobsView>(JobsView.Root());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        loaded = false;
        ResetFills();
    }

    public void OnClosed()
    {
        router.Reset();
        snapshot = JobsSnapshot.Empty;
        loaded = false;
        pendingGearsetId = -1;
        sincePendingEquip = 0f;
        menuGearsetId = -1;
        ResetPendingReorder();
        menu.Close();
        CloseColorPicker();
        CloseCategoryEditor();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        content = context.Content;
        ui.Theme = theme;
        SyncPalette();
        menu.Gate();
        if (pickerOpen || categoryEditorOpen)
        {
            UiInteract.BlockThisFrame();
        }

        Refresh(ImGui.GetIO().DeltaTime);
        if (snapshot.HasJobs)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(content, theme, scale));
        router.Draw(content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        ApplyPendingReorder();

        DrawColorMenu(content, theme);
        DrawCategoriesMenu(content, theme);
        DrawRowMenu(content, theme);
        if (pickerOpen)
        {
            DrawColorPicker(content, scale);
        }

        if (categoryEditorOpen)
        {
            DrawCategoryEditor(content, scale);
        }
    }

    private void SyncPalette()
    {
        var accent = Accent;
        if (accent == paletteAccent)
        {
            return;
        }

        paletteAccent = accent;
        ui.Palette = AppPalettes.JobsFor(accent);
    }

    private void DrawView(JobsView view, Rect area, int depth)
    {
        ui.Body(area);
        if (view.Kind == JobsViewKind.Detail)
        {
            DrawDetail(area, view.ClassJobId);
            return;
        }

        DrawRoot(area);
    }

    private void Refresh(float deltaTime)
    {
        sinceRefresh += deltaTime;
        var interval = RefreshIntervalSeconds;
        if (pendingGearsetId >= 0)
        {
            sincePendingEquip += deltaTime;
            interval = PendingEquipIntervalSeconds;
        }

        var stale = !loaded || sinceRefresh >= interval ||
                    !string.Equals(snapshotLanguage, Loc.Current.Code, StringComparison.Ordinal) ||
                    snapshotContentId != characterWatch.CurrentContentId;
        if (stale)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        snapshot = JobsReader.Build(gameData, CurrentCategories());
        snapshotLanguage = Loc.Current.Code;
        snapshotContentId = characterWatch.CurrentContentId;
        loaded = true;
        sinceRefresh = 0f;
        if (tileFills.Length != snapshot.Jobs.Length)
        {
            tileFills = new Spring[snapshot.Jobs.Length];
        }

        ResolvePendingEquip();
    }

    private void ResetFills()
    {
        for (var index = 0; index < tileFills.Length; index++)
        {
            tileFills[index].SnapTo(0f);
        }

        heroFill.SnapTo(0f);
        detailRingFill.SnapTo(0f);
        detailBarFill.SnapTo(0f);
    }

    private void RequestEquip(GearsetRow gearset)
    {
        if (gearset.IsActive || pendingGearsetId >= 0)
        {
            return;
        }

        switch (GearsetActions.Equip(gearset.Id))
        {
            case GearsetEquipResult.Sent:
                UiFeedback.Play(UiSound.Tap);
                pendingGearsetId = gearset.Id;
                sincePendingEquip = 0f;
                sinceRefresh = 0f;
                return;
            case GearsetEquipResult.Busy:
                UiFeedback.Play(UiSound.Blocked);
                ShellToast.Show(Loc.T(L.Jobs.EquipBusy));
                return;
            default:
                UiFeedback.Play(UiSound.Caution);
                ShellToast.Show(Loc.T(L.Jobs.EquipFailed));
                return;
        }
    }

    private void ResolvePendingEquip()
    {
        if (pendingGearsetId < 0)
        {
            return;
        }

        var index = snapshot.IndexOfGearset(pendingGearsetId);
        if (index >= 0 && snapshot.Gearsets[index].IsActive)
        {
            UiFeedback.Play(UiSound.Success);
            ClearPendingEquip();
            return;
        }

        if (index >= 0 && sincePendingEquip < PendingEquipTimeoutSeconds)
        {
            return;
        }

        UiFeedback.Play(UiSound.Caution);
        ShellToast.Show(Loc.T(L.Jobs.EquipFailed));
        ClearPendingEquip();
    }

    private void ClearPendingEquip()
    {
        pendingGearsetId = -1;
        sincePendingEquip = 0f;
    }

    private bool IsPending(GearsetRow gearset) => gearset.Id == pendingGearsetId;

    private bool IsPending(JobRow job)
    {
        if (pendingGearsetId < 0)
        {
            return false;
        }

        var indices = job.GearsetIndices;
        for (var index = 0; index < indices.Length; index++)
        {
            if (snapshot.Gearsets[indices[index]].Id == pendingGearsetId)
            {
                return true;
            }
        }

        return false;
    }

    private void OpenDetail(JobRow job)
    {
        UiFeedback.Play(UiSound.Tap);
        detailRingFill.SnapTo(0f);
        detailBarFill.SnapTo(0f);
        router.Push(JobsView.Detail(job.ClassJobId));
    }

    private void DrawColorMenu(Rect area, PhoneTheme menuTheme)
    {
        if (!menu.IsOpenFor(ColorMenuId))
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
            Glyph: IconGlyph.Of(FontAwesomeIcon.EyeDropper));

        var picked = menu.Draw(area, menuTheme, items, out var rowAction);
        if (picked < 0)
        {
            return;
        }

        if (picked == customIndex)
        {
            OpenColorPicker(-1);
            return;
        }

        if (picked >= savedOffset && rowAction == DropdownMenu.RowAction.Delete)
        {
            DeleteSavedColor(picked - savedOffset);
            return;
        }

        if (picked >= savedOffset && rowAction == DropdownMenu.RowAction.Edit)
        {
            OpenColorPicker(picked - savedOffset);
            return;
        }

        configuration.JobsAccentName = picked < savedOffset
            ? ThemeCatalog.Accents[picked].Name
            : savedColors[picked - savedOffset].Hex;
        configuration.Save();
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    public void Dispose()
    {
    }
}
