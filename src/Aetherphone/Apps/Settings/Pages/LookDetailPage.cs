using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LookDetailPage : ISettingsPage
{
    private const int NameMaxLength = 40;

    public string Title => LooksPage.NameOf(look);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly HomeLook look;
    private readonly HomeLookService looks;
    private readonly ISettingsNavigator navigator;
    private readonly ConfirmService confirm;
    private string editName;
    private bool deleted;

    public LookDetailPage(HomeLook look, HomeLookService looks, ISettingsNavigator navigator, ConfirmService confirm)
    {
        this.look = look;
        this.looks = looks;
        this.navigator = navigator;
        this.confirm = confirm;
        editName = look.Name;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        if (deleted)
        {
            deleted = false;
            navigator.Back();
            return;
        }

        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawNameField(theme, scale);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
            SettingsSection.Hint(Loc.T(L.Home.LookDetailHint), theme);
            if (!looks.CanDelete)
            {
                return;
            }

            ImGui.Dummy(new Vector2(0f, 14f * scale));
            var card = GroupCard.Begin(theme, 1);
            if (SettingsRow.Action(card.NextRow(), Loc.T(L.Home.LookDelete), theme.Danger, theme))
            {
                AskDelete();
            }

            card.End();
        }
    }

    private void DrawNameField(PhoneTheme theme, float scale)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Plain(Loc.T(L.Home.LookName));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)).Push(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputText("##lookName", ref editName, NameMaxLength);
            if (!ImGui.IsItemActive() && !string.Equals(editName, look.Name, StringComparison.Ordinal))
            {
                looks.Rename(look, editName);
                editName = look.Name;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void AskDelete()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Home.LookDelete),
            Message = Loc.T(L.Home.LookDeleteMessage, LooksPage.NameOf(look)),
            ConfirmLabel = Loc.T(L.Home.LookDeleteConfirm),
            CancelLabel = Loc.T(L.Photos.DeleteCancel),
            Danger = true,
            Confirm = DeleteLook,
        });
    }

    private void DeleteLook()
    {
        deleted = looks.Delete(look.Id);
    }
}
