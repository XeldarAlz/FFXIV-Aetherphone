namespace Aetherphone.Core.Shortcuts;

internal static class ShortcutMacro
{
    public static int Append(string text, List<ShortcutStep> steps, int limit, out bool truncated)
    {
        truncated = false;
        var added = 0;
        if (string.IsNullOrEmpty(text))
        {
            return added;
        }

        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (steps.Count >= limit)
            {
                truncated = true;
                return added;
            }

            steps.Add(StepFor(line));
            added++;
        }

        return added;
    }

    private static ShortcutStep StepFor(string line)
    {
        if (ShortcutCommandText.Split(line, out var wait).Length == 0 && wait > 0f)
        {
            return new ShortcutStep
            {
                Kind = ShortcutStepKind.Wait,
                Seconds = Math.Clamp(wait, ShortcutRunner.MinWaitSeconds, ShortcutRunner.MaxWaitSeconds),
            };
        }

        return new ShortcutStep
        {
            Kind = ShortcutStepKind.Command,
            Text = line.Length <= ShortcutStore.CommandMaxLength
                ? line
                : line.Substring(0, ShortcutStore.CommandMaxLength),
        };
    }
}
