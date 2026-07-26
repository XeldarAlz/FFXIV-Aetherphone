using Aetherphone.Core.ControlCenter;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ControlLayoutServiceInstallTests
{
    [Fact]
    public void FreshInstall_EnablesEveryAvailableModule()
    {
        var modules = MakeModules();
        var service = BuildService(modules, new FakeControlConfiguration());

        for (var index = 0; index < modules.Count; index++)
        {
            Assert.True(SlotIndexOf(service, modules[index].Id) >= 0,
                $"Module '{modules[index].Id}' should ship on the grid");
        }
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

    private static List<IControlModule> MakeModules() =>
        new()
        {
            new FakeControlModule("a"),
            new FakeControlModule("b"),
            new FakeControlModule("c"),
        };

    private sealed class FakeControlModule : IControlModule
    {
        public FakeControlModule(string id) => Id = id;
        public string Id { get; }
        public string GalleryLabel => Id;
        public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Circle;
        public IReadOnlyList<ControlSpan> Sizes { get; } = new[] { ControlSpan.Small };
        public ControlSpan DefaultSpan => ControlSpan.Small;
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
        public bool TryGet(string id, out IControlModule module) => byId.TryGetValue(id, out module!);
    }

    private sealed class FakeControlConfiguration : IControlConfiguration
    {
        public ControlLayout? ControlPanel { get; set; }
        public void Save() { }
    }
}
