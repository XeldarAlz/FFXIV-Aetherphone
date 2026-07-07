using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace Aetherphone.Windows;

public sealed class AboutWindow : Window, IDisposable
{
    private const string Name = "Aetherphone";
    private const string RepoUrl = "https://github.com/XeldarAlz/FFXIV-Aetherphone";
    private const string IconFile = "Icon.png";
    private const string WindowId = "AetherphoneAbout";
    private const string SponsorUrl = "https://github.com/sponsors/XeldarAlz";
    private const string HubUrl = "https://github.com/XeldarAlz/DalamudPlugins";
    private const string Author = "XeldarAlz";
    private const string IssuesUrl = RepoUrl + "/issues";
    private const string DiscussionsUrl = RepoUrl + "/discussions";
    private const string SecurityUrl = RepoUrl + "/security/advisories/new";

    private static readonly (FontAwesomeIcon Icon, string Label, string Url, int AccentId)[] Links =
    {
        (FontAwesomeIcon.CodeBranch, "GitHub", RepoUrl, 0),
        (FontAwesomeIcon.Comments, "Discussions", DiscussionsUrl, 1),
        (FontAwesomeIcon.Bug, "Report a bug", IssuesUrl, 2), (FontAwesomeIcon.ThLarge, "More plugins", HubUrl, 3),
        (FontAwesomeIcon.ShieldAlt, "Security", SecurityUrl, 4),
    };

    private static readonly Vector2[] BloomOffsets = { new(1.6f, 0f), new(-1.6f, 0f), new(0f, 1.6f), new(0f, -1.6f), };

    private static readonly FactCategory[] Categories =
    {
        new(FontAwesomeIcon.Heart, L.About.ReminderHeader, Styling.AccentRose, L.About.Reminders),
        new(FontAwesomeIcon.Lightbulb, L.About.FactHeader, Styling.AccentAmberSoft, L.About.Facts),
        new(FontAwesomeIcon.Star, L.About.QuoteHeader, Styling.AccentMintSoft, L.About.Quotes),
        new(FontAwesomeIcon.GrinBeam, L.About.FunHeader, Styling.AccentBlueSoft, L.About.Fun),
    };

    private static readonly Dictionary<string, float> pillHover = new();
    private static int factCat = -1;
    private static int factLine;
    private static bool iconHovered;
    private static readonly int[][] factBags = new int[Categories.Length][];
    private static readonly int[] factBagPos = new int[Categories.Length];
    private static readonly int[] factLastServed = new int[Categories.Length];
    private long openTick = long.MinValue / 2;

    public AboutWindow() : base($"{Name}: About###{WindowId}")
    {
        Size = new Vector2(540, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 100), MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void OnOpen() => openTick = Environment.TickCount64;

    public override void Draw()
    {
        using var font = Plugin.Fonts.Push(1f);
        using var style = Styling.PushWindowStyle();
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, MathF.Max(0.0001f, Reveal(0)))) AmbientBackground();
        RevealSection(0, () =>
        {
            DrawHero();
            Styling.VSpace(16);
        });
        RevealSection(1, () =>
        {
            DrawSupport();
            Styling.VSpace(16);
        });
        RevealSection(2, () =>
        {
            SectionHeader(FontAwesomeIcon.Link, Loc.T(L.About.Connect), Styling.AccentBlue);
            Styling.VSpace(6);
            DrawConnect();
            Styling.VSpace(16);
        });
        RevealSection(3, DrawFooter);
    }

    private float Reveal(int index)
    {
        const float dur = 420f;
        const float stagger = 95f;
        var elapsed = Environment.TickCount64 - openTick;
        var x = (elapsed - index * stagger) / dur;
        return Easing.SmoothStep(Math.Clamp((float)x, 0f, 1f));
    }

    private void RevealSection(int index, Action draw)
    {
        var a = Reveal(index);
        if (a < 1f) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1f - a) * 12f * ImGuiHelpers.GlobalScale);
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, MathF.Max(0.0001f, a))) draw();
    }

    private static void AmbientBackground()
    {
        var wpos = ImGui.GetWindowPos();
        var rmin = wpos + ImGui.GetWindowContentRegionMin();
        var rmax = wpos + ImGui.GetWindowContentRegionMax();
        var w = rmax.X - rmin.X;
        var h = rmax.Y - rmin.Y;
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(rmin, rmax, true);
        SoftBlob(rmin + new Vector2(w * (0.26f + 0.12f * Wave(11000)), h * (0.20f + 0.10f * Wave(13700))), w * 0.55f,
            Styling.AccentViolet, 0.075f);
        SoftBlob(rmin + new Vector2(w * (0.80f + 0.12f * Wave(15500)), h * (0.32f + 0.10f * Wave(9300))), w * 0.48f,
            Styling.AccentPink, 0.060f);
        SoftBlob(rmin + new Vector2(w * (0.55f + 0.14f * Wave(17900)), h * (0.82f + 0.08f * Wave(12100))), w * 0.52f,
            Styling.AccentBlue, 0.050f);
        dl.PopClipRect();
    }

    private static void SoftBlob(Vector2 c, float radius, Vector4 color, float peak)
    {
        var dl = ImGui.GetWindowDrawList();
        const int layers = 5;
        for (var i = layers; i >= 1; i--)
        {
            var r = radius * i / layers;
            var a = peak * (1f - (i - 1f) / layers);
            dl.AddCircleFilled(c, r, ImGui.GetColorU32(Palette.WithAlpha(color, a)), 40);
        }
    }

    private static void DrawHero()
    {
        var s = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        Styling.VSpace(32);
        const float iconSize = 148f;
        const float ringR = 120f;
        var start = ImGui.GetCursorScreenPos();
        var availX = ImGui.GetContentRegionAvail().X;
        var bob = Wave(3000) * 3f * s;
        var center = new Vector2(start.X + availX * 0.5f, start.Y + ringR * s + bob);
        ProgressRing.Glow(center, ringR * s, Styling.AccentViolet, 0.55f + 0.5f * Styling.Pulse(Styling.PulseBreath));
        ProgressRing.Track(center, ringR * s, 1.5f * s, Palette.WithAlpha(Styling.BorderDim, 0.7f));
        ProgressRing.Sweep(center, ringR * s, 2.6f * s, Styling.AccentVioletSoft, Styling.PulseOrbit, MathF.PI * 0.55f,
            1f);
        OrbitParticles(center, ringR * s, 3, 4600, +1, Styling.AccentVioletSoft, 2.4f * s);
        OrbitParticles(center, ringR * s * 0.74f, 2, 6000, -1, Styling.AccentPink, 2.0f * s);
        var half = iconSize * 0.5f * s;
        var imin = new Vector2(center.X - half, center.Y - half);
        var imax = new Vector2(center.X + half, center.Y + half);
        var rounding = iconSize * 0.20f * s;
        var iconPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? "", "Images", IconFile);
        if (File.Exists(iconPath))
        {
            var tex = Plugin.TextureProvider.GetFromFile(iconPath).GetWrapOrEmpty();
            if (tex != null)
            {
                var alpha = 0.92f + 0.08f * Styling.Pulse(2200.0);
                dl.AddImageRounded(tex.Handle, imin, imax, Vector2.Zero, Vector2.One,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
            }
        }

        dl.AddRect(imin, imax, ImGui.GetColorU32(Palette.WithAlpha(Styling.AccentVioletSoft, 0.55f)), rounding,
            ImDrawFlags.RoundCornersAll, 1.5f * s);
        IconEasterEgg(imin, imax, s);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(availX, ringR * 2f * s));
        Styling.VSpace(10);
        ShimmerCentered(Name, Styling.TextStrong, Styling.AccentVioletSoft, 1.85f, Styling.PulseOrbit, 0.42f);
        Styling.VSpace(9);
        var version = typeof(AboutWindow).Assembly.GetName().Version?.ToString() ?? "?";
        CenteredPill($"v {version}", Styling.TextSecondary, Palette.WithAlpha(Styling.AccentViolet, 0.45f),
            Styling.CardBgSoft);
    }

    private static void OrbitParticles(Vector2 c, float r, int count, double periodMs, int dir, Vector4 color,
        float dotR)
    {
        var dl = ImGui.GetWindowDrawList();
        var baseA = -MathF.PI / 2f + dir * Styling.Phase(periodMs) * MathF.PI * 2f;
        for (var i = 0; i < count; i++)
        {
            var a = baseA + i * (MathF.PI * 2f / count);
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            dl.AddCircleFilled(p, dotR * 2.4f, ImGui.GetColorU32(Palette.WithAlpha(color, 0.16f)));
            dl.AddCircleFilled(p, dotR * 1.5f, ImGui.GetColorU32(Palette.WithAlpha(color, 0.32f)));
            dl.AddCircleFilled(p, dotR, ImGui.GetColorU32(color));
        }
    }

    private static void IconEasterEgg(Vector2 min, Vector2 max, float s)
    {
        if (!ImGui.IsMouseHoveringRect(min, max))
        {
            iconHovered = false;
            return;
        }

        if (!iconHovered)
        {
            iconHovered = true;
            factCat = (factCat + 1) % Categories.Length;
            factLine = NextLineInCategory(factCat);
        }

        var cat = Categories[Math.Max(0, factCat)];
        var line = cat.Lines[factLine];
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        using (ImRaii.Tooltip())
        {
            ImGui.PushTextWrapPos(320f * s);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, cat.Color))
                ImGui.TextUnformatted(cat.Icon.ToIconString());
            ImGui.SameLine(0, 6f * s);
            using (ImRaii.PushColor(ImGuiCol.Text, cat.Color)) ImGui.TextUnformatted(Loc.T(cat.Header));
            ImGui.Spacing();
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextSecondary)) ImGui.TextUnformatted(Loc.T(line));
            ImGui.PopTextWrapPos();
        }
    }

    private readonly record struct FactCategory(
        FontAwesomeIcon Icon,
        LocString Header,
        Vector4 Color,
        LocString[] Lines);

    private static int NextLineInCategory(int cat)
    {
        var count = Categories[cat].Lines.Length;
        if (factBags[cat] == null || factBagPos[cat] >= count)
        {
            var avoidFirst = factBags[cat] == null ? -1 : factLastServed[cat];
            factBags[cat] = Shuffle(count, avoidFirst);
            factBagPos[cat] = 0;
        }

        var line = factBags[cat][factBagPos[cat]++];
        factLastServed[cat] = line;
        return line;
    }

    private static int[] Shuffle(int n, int avoidFirst)
    {
        var a = new int[n];
        for (var i = 0; i < n; i++) a[i] = i;
        for (var i = n - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }

        if (n > 1 && a[0] == avoidFirst)
        {
            var j = 1 + Random.Shared.Next(n - 1);
            (a[0], a[j]) = (a[j], a[0]);
        }

        return a;
    }

    private static void DrawSupport()
    {
        var s = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var pulse = Styling.Pulse(Styling.PulseBreath);
        var accent = Styling.PulseColor(Styling.AccentPink, Styling.AccentViolet, 5200.0);
        var title = Loc.T(L.About.MadeWithCare);
        var body = Loc.T(L.About.SupportBody);
        var slotOrigin = ImGui.GetCursorScreenPos();
        var fullAvail = ImGui.GetContentRegionAvail().X;
        var margin = 24f * s;
        var origin = new Vector2(slotOrigin.X + margin, slotOrigin.Y);
        var availX = fullAvail - margin * 2f;
        var pad = 16f * s;
        var medR = 22f * s;
        var btnH = 36f * s;
        var innerW = availX - pad * 2f;
        var lineH = ImGui.GetTextLineHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var titleH = lineH * 1.12f;
        var bodyLines = WrapLines(body, innerW);
        var bodyBlockH = bodyLines.Count * lineH + MathF.Max(0, bodyLines.Count - 1) * spacing;
        var height = pad + medR * 2f + 12f * s + titleH + spacing + bodyBlockH + 14f * s + btnH + pad;
        var end = new Vector2(origin.X + availX, origin.Y + height);
        var centerX = origin.X + availX * 0.5f;
        dl.AddRectFilled(origin, end, ImGui.GetColorU32(Vector4.Lerp(Styling.CardBg, Styling.AccentPink, 0.07f)),
            Styling.CardRounding);
        dl.AddRect(origin, end, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.55f + 0.35f * pulse)),
            Styling.CardRounding, ImDrawFlags.None, 1.5f);
        var beat = Heartbeat(1400.0);
        var medC = new Vector2(centerX, origin.Y + pad + medR);
        ProgressRing.Glow(medC, medR, accent, 0.4f + 0.7f * beat);
        dl.AddCircleFilled(medC, medR, ImGui.GetColorU32(Vector4.Lerp(Styling.CardBg, accent, 0.28f)));
        ProgressRing.Track(medC, medR, 1.5f * s, Palette.WithAlpha(accent, 0.85f));
        ProgressRing.CenterIcon(medC, FontAwesomeIcon.Heart, Palette.Lighten(accent, 0.25f), medR * (0.80f + 0.22f * beat));
        ImGui.SetCursorScreenPos(new Vector2(slotOrigin.X, origin.Y + pad + medR * 2f + 12f * s));
        Styling.TextCentered(title, Styling.TextStrong, 1.12f);
        foreach (var ln in bodyLines) Styling.TextCentered(ln, Styling.TextSecondary);
        var btnOrigin = new Vector2(origin.X + pad, end.Y - pad - btnH);
        var btnSize = new Vector2(innerW, btnH);
        SponsorButton(btnOrigin, btnSize, accent);
        ImGui.SetCursorScreenPos(slotOrigin);
        ImGui.Dummy(new Vector2(fullAvail, height));
    }

    private static List<string> WrapLines(string text, float maxWidth)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var word in text.Split(' '))
        {
            var test = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && ImGui.CalcTextSize(test).X > maxWidth)
            {
                lines.Add(cur);
                cur = word;
            }
            else
            {
                cur = test;
            }
        }

        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }

    private static void SponsorButton(Vector2 origin, Vector2 size, Vector4 accent)
    {
        var s = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var end = origin + size;
        var hover = ImGui.IsMouseHoveringRect(origin, end);
        var rounding = size.Y * 0.5f;
        var fill = (hover ? Palette.Lighten(accent, 0.16f) : accent) with { W = 1f };
        var glowPulse = 0.5f + 0.5f * Styling.Pulse(Styling.PulseBreath);
        for (var i = 3; i >= 1; i--)
        {
            var grow = i * 2.6f * s;
            var a = 0.06f * i * glowPulse * (hover ? 1.8f : 1f);
            dl.AddRectFilled(origin - new Vector2(grow, grow), end + new Vector2(grow, grow),
                ImGui.GetColorU32(Palette.WithAlpha(fill, a)), rounding + grow);
        }

        dl.AddRectFilled(origin, end, ImGui.GetColorU32(fill), rounding);
        dl.AddLine(new Vector2(origin.X + rounding, origin.Y + 1.5f * s),
            new Vector2(end.X - rounding, origin.Y + 1.5f * s), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), 1f);
        Sheen(origin, size, 3000.0);
        dl.AddRect(origin, end, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hover ? 0.42f : 0.18f)), rounding,
            ImDrawFlags.None, 1f);
        var label = Loc.T(L.About.BecomeSponsor);
        var iconStr = FontAwesomeIcon.HandHoldingHeart.ToIconString();
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconSize = ImGui.CalcTextSize(iconStr);
        var labelSize = ImGui.CalcTextSize(label);
        var innerGap = 9f * s;
        var contentW = iconSize.X + innerGap + labelSize.X;
        var startX = origin.X + (size.X - contentW) * 0.5f;
        var midY = origin.Y + size.Y * 0.5f;
        var breathe = Styling.Pulse(2200.0);
        ImGui.SetWindowFontScale(1f + 0.09f * breathe);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var hs = ImGui.CalcTextSize(iconStr);
            ImGui.SetCursorScreenPos(new Vector2(startX, midY - hs.Y * 0.5f));
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextStrong)) ImGui.TextUnformatted(iconStr);
        }

        ImGui.SetWindowFontScale(1f);
        ImGui.SetCursorScreenPos(new Vector2(startX + iconSize.X + innerGap, midY - labelSize.Y * 0.5f));
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextStrong)) ImGui.TextUnformatted(label);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        if (!hover) return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        using (ImRaii.Tooltip()) ImGui.TextUnformatted(Loc.T(L.About.SponsorTooltip));
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            OpenUrl(SponsorUrl);
        else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) ImGui.SetClipboardText(SponsorUrl);
    }

    private static void Sheen(Vector2 origin, Vector2 size, double periodMs)
    {
        var p = Styling.Phase(periodMs);
        if (p > 0.35f) return;
        var sweep = p / 0.35f;
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(origin, origin + size, true);
        var slant = size.Y * 0.55f;
        var travel = size.X + slant + 40f;
        var cx = origin.X - 20f + sweep * travel;
        const int half = 15;
        for (var k = -half; k <= half; k++)
        {
            var a = 0.16f * (1f - MathF.Abs(k) / (float)half);
            var x = cx + k;
            dl.AddLine(new Vector2(x + slant, origin.Y), new Vector2(x, origin.Y + size.Y),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, a)), 1.3f);
        }

        dl.PopClipRect();
    }

    private static void DrawConnect()
    {
        var s = ImGuiHelpers.GlobalScale;
        var gap = 7f * s;
        var avail = ImGui.GetContentRegionAvail().X;
        var pillH = ImGui.GetFrameHeight() * 1.15f;
        var accents = new[]
        {
            Styling.AccentViolet, Styling.AccentBlue, Styling.AccentRose, Styling.AccentMint, Styling.AccentAmber,
        };
        var widths = new float[Links.Length];
        for (var i = 0; i < Links.Length; i++) widths[i] = PillWidth(Links[i].Icon, LinkLabel(Links[i].Label));
        var rows = new List<List<int>>();
        var cur = new List<int>();
        var curW = 0f;
        for (var i = 0; i < Links.Length; i++)
        {
            var next = cur.Count == 0 ? widths[i] : curW + gap + widths[i];
            if (cur.Count > 0 && next > avail)
            {
                rows.Add(cur);
                cur = new List<int>();
                curW = 0f;
            }

            curW = cur.Count == 0 ? widths[i] : curW + gap + widths[i];
            cur.Add(i);
        }

        if (cur.Count > 0) rows.Add(cur);
        foreach (var row in rows)
        {
            var rowW = gap * (row.Count - 1);
            foreach (var idx in row) rowW += widths[idx];
            var startX = ImGui.GetCursorPosX() + MathF.Max(0f, (avail - rowW) * 0.5f);
            for (var j = 0; j < row.Count; j++)
            {
                if (j == 0)
                    ImGui.SetCursorPosX(startX);
                else
                    ImGui.SameLine(0, gap);
                var (icon, label, url, accentId) = Links[row[j]];
                LinkPill(icon, LinkLabel(label), url, accents[accentId % accents.Length],
                    new Vector2(widths[row[j]], pillH));
            }
        }
    }

    private static string LinkLabel(string identifier) =>
        identifier switch
        {
            "Discussions" => Loc.T(L.About.LinkDiscussions),
            "Report a bug" => Loc.T(L.About.LinkReportBug),
            "More plugins" => Loc.T(L.About.LinkMorePlugins),
            "Security" => Loc.T(L.About.LinkSecurity),
            _ => identifier,
        };

    private static float PillWidth(FontAwesomeIcon icon, string label)
    {
        var s = ImGuiHelpers.GlobalScale;
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconSize = ImGui.CalcTextSize(icon.ToIconString());
        var labelSize = ImGui.CalcTextSize(label);
        return iconSize.X + 6f * s + labelSize.X + 14f * s * 2f;
    }

    private static void LinkPill(FontAwesomeIcon icon, string label, string url, Vector4 accent, Vector2 size)
    {
        var s = ImGuiHelpers.GlobalScale;
        var slotOrigin = ImGui.GetCursorScreenPos();
        var hovered = ImGui.IsMouseHoveringRect(slotOrigin, slotOrigin + size);
        pillHover.TryGetValue(url, out var h);
        var dt = ImGui.GetIO().DeltaTime;
        h += ((hovered ? 1f : 0f) - h) * (1f - MathF.Exp(-14f * dt));
        if (h < 0.001f) h = 0f;
        pillHover[url] = h;
        var lift = h * 2.5f * s;
        var origin = slotOrigin - new Vector2(0, lift);
        var end = origin + size;
        var dl = ImGui.GetWindowDrawList();
        var rounding = size.Y * 0.5f;
        if (h > 0.01f)
            for (var i = 2; i >= 1; i--)
            {
                var grow = i * 2.4f * s;
                dl.AddRectFilled(origin - new Vector2(grow, grow), end + new Vector2(grow, grow),
                    ImGui.GetColorU32(Palette.WithAlpha(accent, 0.05f * i * h)), rounding + grow);
            }

        var bg = Vector4.Lerp(Styling.CardBgSoft, Vector4.Lerp(Styling.CardBg, accent, 0.24f), h);
        var border = Vector4.Lerp(Styling.BorderDim, accent, h);
        dl.AddRectFilled(origin, end, ImGui.GetColorU32(bg), rounding);
        dl.AddRect(origin, end, ImGui.GetColorU32(border), rounding, ImDrawFlags.None, 1f);
        var iconStr = icon.ToIconString();
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconSize = ImGui.CalcTextSize(iconStr);
        var labelSize = ImGui.CalcTextSize(label);
        var innerGap = 6f * s;
        var contentW = iconSize.X + innerGap + labelSize.X;
        var startX = origin.X + (size.X - contentW) * 0.5f;
        var midY = origin.Y + size.Y * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(startX, midY - iconSize.Y * 0.5f));
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.Lerp(accent, Styling.TextStrong, h)))
            ImGui.TextUnformatted(iconStr);
        ImGui.SetCursorScreenPos(new Vector2(startX + iconSize.X + innerGap, midY - labelSize.Y * 0.5f));
        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, h)))
            ImGui.TextUnformatted(label);
        ImGui.SetCursorScreenPos(slotOrigin);
        ImGui.Dummy(size);
        if (!hovered) return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        using (ImRaii.Tooltip()) ImGui.TextUnformatted(Loc.T(L.About.LinkTooltip));
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            OpenUrl(url);
        else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) ImGui.SetClipboardText(url);
    }

    private static void DrawFooter()
    {
        var s = ImGuiHelpers.GlobalScale;
        HairlineRule();
        Styling.VSpace(5);
        var madeBy = Loc.T(L.About.MadeBy, Author);
        var glyph = FontAwesomeIcon.Code.ToIconString();
        var twinkle = Styling.Pulse(2600.0);
        Vector2 glyphSize;
        using (ImRaii.PushFont(UiBuilder.IconFont)) glyphSize = ImGui.CalcTextSize(glyph);
        var gap = 6f * s;
        var total = glyphSize.X + gap + ImGui.CalcTextSize(madeBy).X;
        Styling.CenterNextItem(total);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text,
                   Vector4.Lerp(Styling.AccentBlue, Palette.Lighten(Styling.AccentBlueSoft, 0.3f), twinkle)))
            ImGui.TextUnformatted(glyph);
        ImGui.SameLine(0, gap);
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextDim)) ImGui.TextUnformatted(madeBy);
    }

    private static void SectionHeader(FontAwesomeIcon icon, string label, Vector4 accent)
    {
        var s = ImGuiHelpers.GlobalScale;
        var iconStr = icon.ToIconString();
        var labelUp = label.ToUpperInvariant();
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconSize = ImGui.CalcTextSize(iconStr);
        var labelSize = ImGui.CalcTextSize(labelUp);
        var iconGap = 8f * s;
        var sidePad = 12f * s;
        var contentW = iconSize.X + iconGap + labelSize.X;
        var startScreen = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var leftX = startScreen.X;
        var rightX = startScreen.X + avail;
        var contentStartX = startScreen.X + MathF.Max(0f, (avail - contentW) * 0.5f);
        var lineY = startScreen.Y + iconSize.Y * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(contentStartX, startScreen.Y));
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, accent))
            ImGui.TextUnformatted(iconStr);
        var labelX = contentStartX + iconSize.X + iconGap;
        ImGui.SetCursorScreenPos(new Vector2(labelX, startScreen.Y + (iconSize.Y - labelSize.Y) * 0.5f));
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextDim)) ImGui.TextUnformatted(labelUp);
        RuleLine(leftX, contentStartX - sidePad, lineY, accent, brightAtStart: false);
        RuleLine(labelX + labelSize.X + sidePad, rightX, lineY, accent, brightAtStart: true);
        ImGui.SetCursorScreenPos(startScreen);
        ImGui.Dummy(new Vector2(avail, iconSize.Y));
    }

    private static void RuleLine(float x0, float x1, float y, Vector4 accent, bool brightAtStart)
    {
        if (x1 - x0 < 1f) return;
        var dl = ImGui.GetWindowDrawList();
        var glowPhase = Styling.Phase(3200.0);
        const int seg = 22;
        for (var i = 0; i < seg; i++)
        {
            var t0 = i / (float)seg;
            var t1 = (i + 1) / (float)seg;
            var edge = brightAtStart ? t0 : 1f - t0;
            var fade = 0.5f * (1f - edge);
            var travel = MathF.Max(0f, 1f - MathF.Abs(t0 - glowPhase) * 6f);
            var a = fade + 0.35f * travel;
            dl.AddLine(new Vector2(x0 + (x1 - x0) * t0, y), new Vector2(x0 + (x1 - x0) * t1, y),
                ImGui.GetColorU32(Palette.WithAlpha(accent, a)), 1f);
        }
    }

    private static void ShimmerCentered(string text, Vector4 baseColor, Vector4 shimmerColor, float fontScale,
        double periodMs, float bandFrac)
    {
        ImGui.SetWindowFontScale(fontScale);
        var size = ImGui.CalcTextSize(text);
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > size.X) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size.X) * 0.5f);
        var startScreen = ImGui.GetCursorScreenPos();
        var bloom = Palette.WithAlpha(Styling.AccentViolet, 0.22f);
        foreach (var off in BloomOffsets)
        {
            ImGui.SetCursorScreenPos(startScreen + off * ImGuiHelpers.GlobalScale);
            using (ImRaii.PushColor(ImGuiCol.Text, bloom)) ImGui.TextUnformatted(text);
        }

        ImGui.SetCursorScreenPos(startScreen);
        using (ImRaii.PushColor(ImGuiCol.Text, baseColor)) ImGui.TextUnformatted(text);
        var dl = ImGui.GetWindowDrawList();
        var bandW = size.X * bandFrac;
        var phase = Styling.Phase(periodMs);
        var bandCenter = startScreen.X - bandW + phase * (size.X + bandW * 2f);
        dl.PushClipRect(new Vector2(bandCenter - bandW * 0.5f, startScreen.Y),
            new Vector2(bandCenter + bandW * 0.5f, startScreen.Y + size.Y), true);
        ImGui.SetCursorScreenPos(startScreen);
        using (ImRaii.PushColor(ImGuiCol.Text, shimmerColor)) ImGui.TextUnformatted(text);
        dl.PopClipRect();
        ImGui.SetWindowFontScale(1f);
    }

    private static void CenteredPill(string text, Vector4 textColor, Vector4 borderColor, Vector4 bgColor)
    {
        var s = ImGuiHelpers.GlobalScale;
        var padX = 11f * s;
        var padY = 3f * s;
        var ts = ImGui.CalcTextSize(text);
        var w = ts.X + padX * 2f;
        var h = ts.Y + padY * 2f;
        Styling.CenterNextItem(w);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + new Vector2(w, h);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(origin, end, ImGui.GetColorU32(bgColor), h * 0.5f);
        dl.AddRect(origin, end, ImGui.GetColorU32(borderColor), h * 0.5f, ImDrawFlags.None, 1f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + padX, origin.Y + padY));
        using (ImRaii.PushColor(ImGuiCol.Text, textColor)) ImGui.TextUnformatted(text);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(w, h));
    }

    private static void HairlineRule()
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        dl.AddLine(p, p + new Vector2(w, 0), ImGui.GetColorU32(Styling.Hairline), 1f);
        ImGui.Dummy(new Vector2(w, 1f));
    }

    private static float Wave(double periodMs) =>
        MathF.Sin((float)(Environment.TickCount % periodMs / periodMs) * MathF.PI * 2f);

    private static float Heartbeat(double periodMs)
    {
        var p = Styling.Phase(periodMs);
        return MathF.Max(Bump(p, 0.06f, 0.06f), Bump(p, 0.20f, 0.06f) * 0.6f);
    }

    private static float Bump(float p, float center, float width)
    {
        var d = (p - center) / width;
        if (d < -1f || d > 1f) return 0f;
        return 0.5f * (1f + MathF.Cos(d * MathF.PI));
    }

    private static void OpenUrl(string url) =>
        UrlActions.OpenInBrowser(url,
            ex => Plugin.Log.Warning(ex, $"failed to launch browser for {url}, copied to clipboard instead"));
}
