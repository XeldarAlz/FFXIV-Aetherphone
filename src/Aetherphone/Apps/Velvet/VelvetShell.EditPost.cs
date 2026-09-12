using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Translation;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private void OpenEditPost(VelvetPostDto entry)
    {
        post.OpenEdit(entry);
        router.Push(VelvetView.EditPost(entry.Id));
    }

    private void DrawEditPost(Rect area, string postId)
    {
        var context = new PhoneContext(area, theme, navigation);
        var result = post.Draw(area, ui, context);
        if (result == VelvetComposeResult.Edited)
        {
            translation.Forget(new TranslationKey(TranslationSurface.Post, postId));
        }

        if (result != VelvetComposeResult.Open)
        {
            router.Pop();
        }
    }
}
