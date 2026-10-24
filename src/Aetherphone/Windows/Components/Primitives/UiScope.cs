using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UiScope
{
    public static ColorScope PushColor(ImGuiCol index, uint color, bool condition = true) =>
        default(ColorScope).Push(index, color, condition);

    public static ColorScope PushColor(ImGuiCol index, Vector4 color, bool condition = true) =>
        default(ColorScope).Push(index, color, condition);

    public static StyleScope PushStyle(ImGuiStyleVar index, float value, bool condition = true) =>
        default(StyleScope).Push(index, value, condition);

    public static StyleScope PushStyle(ImGuiStyleVar index, Vector2 value, bool condition = true) =>
        default(StyleScope).Push(index, value, condition);

    public static IdScope PushId(ImU8String id, bool enabled = true) => default(IdScope).Push(id, enabled);

    public static IdScope PushId(int id, bool enabled = true) => default(IdScope).Push(id, enabled);

    public static IdScope PushId(nint id, bool enabled = true) => default(IdScope).Push(id, enabled);

    public static DisabledScope Disabled(bool disabled = true) => default(DisabledScope).Push(disabled);

    public readonly struct ColorScope : IDisposable
    {
        private readonly int count;

        private ColorScope(int count)
        {
            this.count = count;
        }

        public ColorScope Push(ImGuiCol index, uint color, bool condition = true)
        {
            if (!condition)
            {
                return this;
            }

            ImGui.PushStyleColor(index, color);
            return new ColorScope(count + 1);
        }

        public ColorScope Push(ImGuiCol index, Vector4 color, bool condition = true)
        {
            if (!condition)
            {
                return this;
            }

            ImGui.PushStyleColor(index, color);
            return new ColorScope(count + 1);
        }

        public void Dispose()
        {
            if (count > 0)
            {
                ImGui.PopStyleColor(count);
            }
        }
    }

    public readonly struct StyleScope : IDisposable
    {
        private readonly int count;

        private StyleScope(int count)
        {
            this.count = count;
        }

        public StyleScope Push(ImGuiStyleVar index, float value, bool condition = true)
        {
            if (!condition)
            {
                return this;
            }

            ImGui.PushStyleVar(index, value);
            return new StyleScope(count + 1);
        }

        public StyleScope Push(ImGuiStyleVar index, Vector2 value, bool condition = true)
        {
            if (!condition)
            {
                return this;
            }

            ImGui.PushStyleVar(index, value);
            return new StyleScope(count + 1);
        }

        public void Dispose()
        {
            if (count > 0)
            {
                ImGui.PopStyleVar(count);
            }
        }
    }

    public readonly struct IdScope : IDisposable
    {
        private readonly int count;

        private IdScope(int count)
        {
            this.count = count;
        }

        public IdScope Push(ImU8String id, bool enabled = true)
        {
            if (!enabled)
            {
                id.Recycle();
                return this;
            }

            ImGui.PushID(id);
            return new IdScope(count + 1);
        }

        public IdScope Push(int id, bool enabled = true)
        {
            if (!enabled)
            {
                return this;
            }

            ImGui.PushID(id);
            return new IdScope(count + 1);
        }

        public IdScope Push(nint id, bool enabled = true)
        {
            if (!enabled)
            {
                return this;
            }

            ImGui.PushID(id);
            return new IdScope(count + 1);
        }

        public void Dispose()
        {
            for (var index = 0; index < count; index++)
            {
                ImGui.PopID();
            }
        }
    }

    public readonly struct DisabledScope : IDisposable
    {
        private readonly int count;

        private DisabledScope(int count)
        {
            this.count = count;
        }

        public DisabledScope Push(bool disabled = true)
        {
            if (!disabled)
            {
                return this;
            }

            ImGui.BeginDisabled(true);
            return new DisabledScope(count + 1);
        }

        public void Dispose()
        {
            for (var index = 0; index < count; index++)
            {
                ImGui.EndDisabled();
            }
        }
    }
}
