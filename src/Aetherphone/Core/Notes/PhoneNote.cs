namespace Aetherphone.Core.Notes;

[Serializable]
internal sealed class PhoneNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Body { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string Title()
    {
        var body = Body;
        for (var index = 0; index < body.Length; index++)
        {
            var lineEnd = body.IndexOf('\n', index);
            var line = lineEnd < 0 ? body.Substring(index) : body.Substring(index, lineEnd - index);
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }

            if (lineEnd < 0)
            {
                break;
            }

            index = lineEnd;
        }

        return string.Empty;
    }

    public string Preview()
    {
        var title = Title();
        if (title.Length == 0)
        {
            return string.Empty;
        }

        var titleIndex = Body.IndexOf(title, StringComparison.Ordinal);
        var rest = titleIndex < 0 ? Body : Body.Substring(titleIndex + title.Length);
        return rest.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }
}
