using Aetherphone.Core.ControlCenter;
using Aetherphone.Core.Home;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ControlLayoutServiceInstallTests
{
    [Fact]
    public void FreshInstall_ShipsTheDefaultLayoutInOrder()
    {
        var defaults = ControlDefaults.Layout;
        var service = BuildService(DefaultModulesPlus("spare"), new FakeControlConfiguration());

        Assert.Equal(defaults.Length, service.Slots.Count);
        for (var index = 0; index < defaults.Length; index++)
        {
            Assert.Equal(defaults[index].ModuleId, service.Slots[index].Id);
            Assert.Equal(defaults[index].Span, service.Slots[index].Span);
        }
    }

    [Fact]
    public void FreshInstall_LeavesModulesOutsideTheDefaultsInTheGallery()
    {
        var service = BuildService(DefaultModulesPlus("spare"), new FakeControlConfiguration());

        Assert.Equal(-1, SlotIndexOf(service, "spare"));
        Assert.Contains(service.Hidden(), module => module.Id == "spare");
    }

    [Fact]
    public void FreshInstall_PacksTheDefaultLayoutWithoutHoles()
    {
        var service = BuildService(DefaultModulesPlus("spare"), new FakeControlConfiguration());
        var placements = service.Placements;

        Assert.Equal(new GridCell(0, 0), placements[SlotIndexOf(service, ControlDefaults.ClusterId)]);
        Assert.Equal(new GridCell(2, 0), placements[SlotIndexOf(service, "media")]);
        Assert.Equal(new GridCell(0, 2), placements[SlotIndexOf(service, "brightness")]);
        Assert.Equal(new GridCell(1, 2), placements[SlotIndexOf(service, "volume")]);
        Assert.Equal(new GridCell(2, 2), placements[SlotIndexOf(service, "lock")]);
        Assert.Equal(new GridCell(3, 2), placements[SlotIndexOf(service, "settings")]);
        Assert.Equal(new GridCell(2, 3), placements[SlotIndexOf(service, "accent")]);
        Assert.Equal(4, service.RowsUsed);
    }

    [Fact]
    public void FreshInstall_StampsTheCurrentLayoutVersion()
    {
        var configuration = new FakeControlConfiguration();

        BuildService(DefaultModulesPlus(), configuration);

        Assert.Equal(ControlLayoutService.LayoutVersion, configuration.ControlPanel!.Version);
    }

    [Fact]
    public void LegacySave_FoldsTheClusterMembersIntoOneTileWhereTheFirstMemberSat()
    {
        var configuration = SavedWith("media", "dnd", "brightness", "silent", "calls", "idle", "settings");

        var service = BuildService(ClusterModulesPlus("media", "brightness", "settings"), configuration);

        Assert.Equal(new[] { "media", ControlDefaults.ClusterId, "brightness", "settings" }, SlotIds(service));
        Assert.Equal(ControlSpan.Large, service.Slots[1].Span);
        Assert.Equal(ControlLayoutService.LayoutVersion, configuration.ControlPanel!.Version);
        Assert.DoesNotContain("dnd", configuration.ControlPanel.Enabled);
        Assert.Contains(ControlDefaults.ClusterId, configuration.ControlPanel.Enabled);
    }

    [Fact]
    public void LegacySave_WithoutAnyClusterMember_LeavesTheClusterInTheGallery()
    {
        var configuration = SavedWith("media", "settings");

        var service = BuildService(ClusterModulesPlus("media", "settings"), configuration);

        Assert.Equal(new[] { "media", "settings" }, SlotIds(service));
        Assert.Contains(service.Hidden(), module => module.Id == ControlDefaults.ClusterId);
        Assert.Equal(ControlLayoutService.LayoutVersion, configuration.ControlPanel!.Version);
    }

    [Fact]
    public void VersionedSave_DoesNotReinsertAClusterTheUserRemoved()
    {
        var configuration = SavedWith("media", "dnd");
        configuration.ControlPanel!.Version = ControlLayoutService.LayoutVersion;

        var service = BuildService(ClusterModulesPlus("media"), configuration);

        Assert.Equal(new[] { "media", "dnd" }, SlotIds(service));
        Assert.Contains(service.Hidden(), module => module.Id == ControlDefaults.ClusterId);
    }

    [Fact]
    public void LegacySave_SurvivesAReloadAfterTheFold()
    {
        var modules = ClusterModulesPlus("media");
        var configuration = SavedWith("dnd", "media", "silent");

        BuildService(modules, configuration);
        var reloaded = BuildService(modules, configuration);

        Assert.Equal(new[] { ControlDefaults.ClusterId, "media" }, SlotIds(reloaded));
    }

    [Fact]
    public void FreshInstall_KeepsWorkingWhenAModuleIsMissingFromTheRegistry()
    {
        var modules = DefaultModulesPlus();
        modules.RemoveAll(module => module.Id == "media");

        var service = BuildService(modules, new FakeControlConfiguration());

        Assert.Equal(ControlDefaults.Layout.Length - 1, service.Slots.Count);
        Assert.Equal(-1, SlotIndexOf(service, "media"));
    }

    [Fact]
    public void ModuleShippedAfterTheSaveWasWritten_IsNotEnabled()
    {
        var modules = MakeModules();
        var configuration = SavedWith("a", "b");

        var service = BuildService(modules, configuration);

        Assert.Equal(-1, SlotIndexOf(service, "c"));
    }

    [Fact]
    public void Add_PutsTheModuleOnTheGridAndSurvivesAReload()
    {
        var modules = MakeModules();
        var configuration = SavedWith("a", "b");

        var first = BuildService(modules, configuration);
        first.Add(modules[2]);
        Assert.True(SlotIndexOf(first, "c") >= 0);

        var reloaded = BuildService(modules, configuration);

        Assert.True(SlotIndexOf(reloaded, "c") >= 0, "A module the user added should still be on the grid after a reload");
    }

    [Fact]
    public void Remove_TakesTheModuleOffTheGridAndSurvivesAReload()
    {
        var modules = MakeModules();
        var configuration = SavedWith("a", "b", "c");

        var first = BuildService(modules, configuration);
        var slot = first.Slots[SlotIndexOf(first, "c")];
        first.Remove(slot);
        Assert.Equal(-1, SlotIndexOf(first, "c"));

        var reloaded = BuildService(modules, configuration);

        Assert.True(SlotIndexOf(reloaded, "c") < 0, "A module the user removed must stay removed across a restart");
    }

    [Fact]
    public void UnavailableModule_LeavesTheGridAndTheGalleryButKeepsItsSavedPlace()
    {
        var modules = MakeModules();
        var configuration = SavedWith("a", "b", "c");
        var registry = new FakeControlRegistry(modules);
        var service = new ControlLayoutService(registry, configuration);

        registry.Unavailable.Add("b");

        Assert.Equal(new[] { "a", "c" }, SlotIds(service));
        Assert.Equal(new GridCell(1, 0), service.Placements[SlotIndexOf(service, "c")]);
        Assert.DoesNotContain(service.Hidden(), module => module.Id == "b");

        service.Move(service.Slots[SlotIndexOf(service, "c")], 0);
        registry.Unavailable.Clear();

        Assert.Equal(new[] { "c", "a", "b" }, SlotIds(service));
        Assert.Equal(new[] { "c", "a", "b" }, SlotIds(new ControlLayoutService(registry, configuration)));
    }

    private static int SlotIndexOf(ControlLayoutService service, string moduleId)
    {
        for (var index = 0; index < service.Slots.Count; index++)
        {
            if (service.Slots[index].Id == moduleId)
            {
                return index;
            }
        }

        return -1;
    }

    private static ControlLayoutService BuildService(List<IControlModule> modules, FakeControlConfiguration configuration) =>
        new(new FakeControlRegistry(modules), configuration);

    private static FakeControlConfiguration SavedWith(params string[] moduleIds)
    {
        var layout = new ControlLayout();
        for (var index = 0; index < moduleIds.Length; index++)
        {
            layout.Items.Add(new ControlItem { ModuleId = moduleIds[index], Span = "small" });
        }

        layout.Enabled.AddRange(moduleIds);
        return new FakeControlConfiguration { ControlPanel = layout };
    }

    private static string[] SlotIds(ControlLayoutService service)
    {
        var ids = new string[service.Slots.Count];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = service.Slots[index].Id;
        }

        return ids;
    }

    private static List<IControlModule> MakeModules() =>
        new()
        {
            new FakeControlModule("a"),
            new FakeControlModule("b"),
            new FakeControlModule("c"),
        };

    private static List<IControlModule> ClusterModulesPlus(params string[] extraIds)
    {
        var members = ControlDefaults.ClusterMembers;
        var modules = new List<IControlModule>(members.Length + extraIds.Length + 1);
        for (var index = 0; index < members.Length; index++)
        {
            modules.Add(new FakeControlModule(members[index]));
        }

        modules.Add(new FakeControlModule(ControlDefaults.ClusterId, ControlSpan.Large));
        for (var index = 0; index < extraIds.Length; index++)
        {
            modules.Add(new FakeControlModule(extraIds[index]));
        }

        return modules;
    }

    private static List<IControlModule> DefaultModulesPlus(params string[] extraIds)
    {
        var defaults = ControlDefaults.Layout;
        var modules = new List<IControlModule>(defaults.Length + extraIds.Length);
        for (var index = 0; index < defaults.Length; index++)
        {
            modules.Add(new FakeControlModule(defaults[index].ModuleId, defaults[index].Span));
        }

        for (var index = 0; index < extraIds.Length; index++)
        {
            modules.Add(new FakeControlModule(extraIds[index]));
        }

        return modules;
    }

    private sealed class FakeControlModule : IControlModule
    {
        public FakeControlModule(string id, ControlSpan span = ControlSpan.Small)
        {
            Id = id;
            DefaultSpan = span;
            Sizes = new[] { span };
        }

        public string Id { get; }
        public string GalleryLabel => Id;
        public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Circle;
        public IReadOnlyList<ControlSpan> Sizes { get; }
        public ControlSpan DefaultSpan { get; }
        public void Draw(in ControlModuleContext context) { }
    }

    private sealed class FakeControlRegistry : IControlRegistry
    {
        private readonly Dictionary<string, IControlModule> byId;

        public FakeControlRegistry(List<IControlModule> modules)
        {
            Modules = modules;
            byId = modules.ToDictionary(module => module.Id);
        }

        public IReadOnlyList<IControlModule> Modules { get; }
        public HashSet<string> Unavailable { get; } = new();
        public bool TryGet(string id, out IControlModule module) => byId.TryGetValue(id, out module!);
        public bool IsAvailable(string moduleId) => !Unavailable.Contains(moduleId);
    }

    private sealed class FakeControlConfiguration : IControlConfiguration
    {
        public ControlLayout? ControlPanel { get; set; }
        public void Save() { }
    }
}
