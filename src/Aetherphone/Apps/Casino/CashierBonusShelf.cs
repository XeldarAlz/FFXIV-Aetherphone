using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierBonusShelf
{
    public const float RowHeight = 62f;

    private const float TileSize = 34f;
    private const float ClaimHeight = Button.SmallHeight;
    private const float ClaimMinWidth = 78f;
    private const float StreakDot = 6f;
    private const float StreakGap = 5f;
    private const float ShowerSeconds = 1.1f;
    private const float ShowerRate = 70f;
    private const int ShowerBurst = 36;

    private static readonly LocString[] Titles =
    {
        L.Strip.BonusWelcome,
        L.Strip.BonusTimed,
        L.Strip.BonusReload,
        L.Strip.BonusStreak,
        L.Strip.BonusLevelUp,
        L.Strip.BonusBroke,
        L.Strip.BonusRebate,
    };

    private static readonly LocString[] Hints =
    {
        L.Strip.BonusWelcomeHint,
        L.Strip.BonusTimedHint,
        L.Strip.BonusReloadHint,
        L.Strip.BonusStreakHint,
        L.Strip.BonusLevelUpHint,
        L.Strip.BonusBrokeHint,
        L.Strip.BonusRebateHint,
    };

    private static readonly FontAwesomeIcon[] Icons =
    {
        FontAwesomeIcon.Gift,
        FontAwesomeIcon.Clock,
        FontAwesomeIcon.Redo,
        FontAwesomeIcon.CalendarCheck,
        FontAwesomeIcon.Star,
        FontAwesomeIcon.HandHoldingHeart,
        FontAwesomeIcon.Undo,
    };

    private static readonly Vector4[] Tints =
    {
        AccentRing.Rose,
        AccentRing.Gold,
        AccentRing.Violet,
        AccentRing.Orange,
        AccentRing.Azure,
        AccentRing.Emerald,
        AccentRing.Indigo,
    };

    private readonly CasinoStore store;
    private readonly CasinoTextCache texts = new();
    private readonly ParticleSystem shower = new(240);
    private readonly Vector2[] claimCenters = new Vector2[CasinoBonusKinds.All.Length];
    private Emitter emitter;
    private float showerLeft;
    private Vector2 showerOrigin;
    private string note = string.Empty;
    private string refusal = string.Empty;
    private bool noteIsGrant;

    public CashierBonusShelf(CasinoStore store)
    {
        this.store = store;
    }

    public string Note => note;

    public void Claim(string kind, Vector2 origin)
    {
        var index = CasinoBonusKinds.IndexOf(kind);
        if (index < 0 || store.ClaimingBonus.Length > 0)
        {
            return;
        }

        claimCenters[index] = origin;
        ClearNote();
        store.ClaimBonus(kind);
        UiFeedback.Play(UiSound.CasinoChips);
    }

    public void Shower(Vector2 origin, float scale)
    {
        showerOrigin = origin;
        shower.Emit(CasinoLights.CoinShower(scale), showerOrigin, ShowerBurst);
        emitter = CasinoLights.CoinShowerEmitter(scale, ShowerRate);
        showerLeft = ShowerSeconds;
        UiFeedback.Play(UiSound.CoinShower);
    }

    public string TakeRefusal()
    {
        var taken = refusal;
        refusal = string.Empty;
        return taken;
    }

    public bool NoteIsGrant => noteIsGrant;

    public void ClearNote()
    {
        note = string.Empty;
        noteIsGrant = false;
    }

    public void Reset()
    {
        ClearNote();
        shower.Clear();
        emitter.Reset();
        showerLeft = 0f;
    }

    public void Update(float delta, float scale)
    {
        ConsumeResults(scale);
        shower.Update(delta);
        if (showerLeft <= 0f)
        {
            return;
        }

        emitter.Advance(delta, showerOrigin, shower);
        showerLeft -= delta;
    }

    public void DrawShower(ImDrawListPtr drawList, float scale)
    {
        shower.Draw(drawList, scale);
    }

    public int RowCount(bool readyOnly)
    {
        var bonuses = store.Bonuses;
        var count = 0;
        for (var index = 0; index < bonuses.Length; index++)
        {
            if (Shows(bonuses[index], readyOnly))
            {
                count++;
            }
        }

        return count;
    }

    public float Height(bool readyOnly, float scale)
    {
        return RowCount(readyOnly) * RowHeight * scale;
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        bool readyOnly, bool overlay, bool interactive)
    {
        var bonuses = store.Bonuses;
        var rows = RowCount(readyOnly);
        if (rows == 0)
        {
            return top;
        }

        var rowHeight = RowHeight * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + rows * rowHeight);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var drawn = 0;
        for (var index = 0; index < bonuses.Length; index++)
        {
            var bonus = bonuses[index];
            if (!Shows(bonus, readyOnly))
            {
                continue;
            }

            var rowTop = top + drawn * rowHeight;
            var row = new Rect(new Vector2(left, rowTop), new Vector2(left + width, rowTop + rowHeight));
            if (drawn > 0)
            {
                drawList.AddLine(new Vector2(left + (Metrics.Space.Lg + TileSize + 12f) * scale, rowTop),
                    new Vector2(left + width, rowTop), ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            DrawRow(drawList, ui, bonus, row, nowUnix, scale, overlay, interactive);
            drawn++;
        }

        return max.Y;
    }

    internal static bool Shows(CasinoBonusDto bonus, bool readyOnly)
    {
        var kind = CasinoBonusKinds.IndexOf(bonus.Kind);
        if (kind < 0)
        {
            return false;
        }

        if (readyOnly || string.Equals(bonus.Kind, CasinoBonusKinds.Welcome, StringComparison.Ordinal))
        {
            return bonus.Ready;
        }

        return true;
    }

    internal static long SecondsUntil(CasinoBonusDto bonus, long nowUnix)
    {
        return bonus.Ready || bonus.NextAtUnix <= 0 ? 0 : Math.Max(0, bonus.NextAtUnix - nowUnix);
    }

    private void DrawRow(ImDrawListPtr drawList, AppSkin ui, CasinoBonusDto bonus, in Rect row, long nowUnix,
        float scale, bool overlay, bool interactive)
    {
        var kind = CasinoBonusKinds.IndexOf(bonus.Kind);
        var pad = Metrics.Space.Lg * scale;
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, bonus.Ready ? Tints[kind] : AccentRing.Slate, Icons[kind]);

        var claimLabel = bonus.Ready ? texts.Compact(L.Strip.BonusClaim, bonus.Amount) : StateLabel(bonus, nowUnix);
        var buttonHeight = ClaimHeight * scale;
        var buttonWidth = MathF.Max(ClaimMinWidth * scale, Button.WidthFor(claimLabel, ButtonSize.Small));
        var buttonRect = new Rect(new Vector2(row.Max.X - pad - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + buttonHeight * 0.5f));
        var textLeft = tileCenter.X + tile * 0.5f + 12f * scale;
        var textRight = buttonRect.Min.X - Metrics.Space.Sm * scale;
        var hint = HintFor(bonus, kind);
        CoinArt.Labels(drawList, textLeft, textRight, row.Center.Y, Loc.T(Titles[kind]), hint,
            bonus.Ready ? ui.TitleInk : ui.BodyInk, ui.MutedInk, scale);
        if (kind == CasinoBonusKinds.IndexOf(CasinoBonusKinds.Streak))
        {
            DrawStreak(drawList, ui, bonus, new Vector2(textLeft, row.Max.Y - 9f * scale), scale);
        }

        claimCenters[kind] = buttonRect.Center;
        if (!bonus.Ready)
        {
            var size = Typography.Measure(claimLabel, TextStyles.FootnoteEmphasized);
            var fitted = Typography.FitText(claimLabel, buttonWidth + pad * 0.5f, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(buttonRect.Max.X - MathF.Min(size.X, buttonWidth + pad * 0.5f),
                    buttonRect.Center.Y - size.Y * 0.5f), fitted, ui.MutedInk, TextStyles.FootnoteEmphasized);
            return;
        }

        var claiming = store.ClaimingBonus.Length > 0;
        if (Button.Draw(drawList, buttonRect, claimLabel, ui.Ink, ButtonStyle.Prominent,
                enabled: interactive && !claiming, overlay: overlay, id: bonus.Kind))
        {
            ClearNote();
            store.ClaimBonus(bonus.Kind);
            UiFeedback.Play(UiSound.CasinoChips);
        }
    }

    private string HintFor(CasinoBonusDto bonus, int kind)
    {
        if (kind == CasinoBonusKinds.IndexOf(CasinoBonusKinds.Streak))
        {
            return texts.Count(L.Strip.BonusStreakHint, Math.Max(1, bonus.StreakDay));
        }

        return Loc.T(Hints[kind]);
    }

    private string StateLabel(CasinoBonusDto bonus, long nowUnix)
    {
        if (!bonus.Eligible)
        {
            return bonus.Kind switch
            {
                CasinoBonusKinds.Reload => texts.Named(L.Strip.BonusNeedsTier,
                    Loc.T(CashierClubCard.TierName(CasinoClubTiers.ReloadFloor))),
                CasinoBonusKinds.Rebate => texts.Named(L.Strip.BonusNeedsTier,
                    Loc.T(CashierClubCard.TierName(CasinoClubTiers.RebateFloor))),
                CasinoBonusKinds.Broke => Loc.T(L.Strip.BonusNeedsBroke),
                _ => Loc.T(L.Strip.BonusLocked),
            };
        }

        var seconds = SecondsUntil(bonus, nowUnix);
        if (seconds > 0)
        {
            return texts.Duration(L.Strip.BonusReadyIn, (int)Math.Min(seconds, int.MaxValue));
        }

        return bonus.Kind switch
        {
            CasinoBonusKinds.LevelUp => Loc.T(L.Strip.BonusNeedsLevel),
            CasinoBonusKinds.Broke => Loc.T(L.Strip.BonusNeedsBroke),
            _ => Loc.T(L.Strip.BonusLocked),
        };
    }

    private static void DrawStreak(ImDrawListPtr drawList, AppSkin ui, CasinoBonusDto bonus, Vector2 leftCenter,
        float scale)
    {
        var radius = StreakDot * 0.5f * scale;
        var step = (StreakDot + StreakGap) * scale;
        var paid = Math.Clamp(bonus.Ready ? bonus.StreakDay - 1 : bonus.StreakDay, 0, CasinoFaucets.StreakDays);
        for (var day = 0; day < CasinoFaucets.StreakDays; day++)
        {
            var center = new Vector2(leftCenter.X + radius + day * step, leftCenter.Y);
            if (day < paid)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.Money), 12);
                continue;
            }

            if (day == paid && bonus.Ready)
            {
                drawList.AddCircleFilled(center, radius,
                    ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, 0.45f + 0.4f * Pulse.Wave(Pulse.Breath))),
                    12);
                continue;
            }

            drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.6f)), 12,
                1f * scale);
        }
    }

    private void ConsumeResults(float scale)
    {
        if (store.TakeBonusFailure())
        {
            note = Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable));
            refusal = note;
            noteIsGrant = false;
        }

        var result = store.TakeBonusResult();
        if (result is null)
        {
            return;
        }

        if (!result.Granted)
        {
            note = Loc.T(CasinoReasons.MessageFor(result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable));
            refusal = note;
            noteIsGrant = false;
            return;
        }

        note = texts.Compact(L.Strip.BonusLanded, result.Amount);
        noteIsGrant = true;
        var kind = CasinoBonusKinds.IndexOf(result.Kind);
        showerOrigin = kind >= 0 ? claimCenters[kind] : showerOrigin;
        shower.Emit(CasinoLights.CoinShower(scale), showerOrigin, ShowerBurst);
        emitter = CasinoLights.CoinShowerEmitter(scale, ShowerRate);
        showerLeft = ShowerSeconds;
        UiFeedback.Play(UiSound.CoinShower);
    }
}
