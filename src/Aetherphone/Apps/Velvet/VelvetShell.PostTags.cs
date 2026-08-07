using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private void OpenPostTags() => router.Push(VelvetView.PostTags);

    private void DrawPostTags(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.PostTagsTitle), theme))
        {
            router.Pop();
            return;
        }

        if (post.TagCount > 0 && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            post.ClearTags();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(8f);
            ui.HelpText(Loc.T(L.Velvet.PostTagsHint));
            Gap(6f);
            ui.HelpText(Loc.Plural(L.Velvet.PostTagsRemaining, VelvetPostComposer.MaxPostTags - post.TagCount));
            Gap(14f);

            var width = ImGui.GetContentRegionAvail().X;
            var categories = VelvetSuggestions.PostTagCategories;
            for (var index = 0; index < categories.Length; index++)
            {
                var category = categories[index];
                VSectionHeader.Card(FontAwesomeIcon.Hashtag, Loc.T(category.Title));
                Gap(6f);
                DrawPostTagChips(category.Tags, category.Hue, width, scale);
                Gap(16f);
            }

            Gap(8f);
            if (ui.PillButton(Reserve(46f), Loc.T(L.Velvet.FilterDone), true))
            {
                router.Pop();
            }

            Gap(40f);
        }
    }

    private void DrawPostTagChips(string[] options, Vector4 accent, float width, float scale)
    {
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            var token = options[index];
            models[index] = post.HasTag(token)
                ? new VChipModel(token, VChipStyle.Solid, accent, FontAwesomeIcon.Check)
                : new VChipModel(token, VChipStyle.Ghost, VelvetTheme.Moonlight);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return;
        }

        post.ToggleTag(options[clicked]);
    }
}
