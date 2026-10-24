using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoKnockNotifier : IDisposable
{
    private const long HostPollMilliseconds = 20_000;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoTablesStore tables;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;
    private readonly StoreWork work = new("CasinoKnocks");
    private readonly HashSet<string> heardKnocks = new(StringComparer.Ordinal);

    private string heardTableId = string.Empty;
    private string answeredRoomId = string.Empty;
    private KnockStage answeredStage;
    private long polledAtTick;
    private int polling;

    public CasinoKnockNotifier(AethernetSession session, CasinoClient casino, CasinoTablesStore tables,
        NotificationService notifications, Vector4 accent)
    {
        this.session = session;
        this.casino = casino;
        this.tables = tables;
        this.notifications = notifications;
        this.accent = accent;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    internal static bool AnswerWorthTelling(KnockStage stage)
    {
        return stage is KnockStage.Admitted or KnockStage.Denied;
    }

    internal static string KnockKey(CasinoTableKnockDto knock)
    {
        return string.Concat(knock.UserId, ":", knock.CreatedAtUnixMs.ToString(Loc.Culture));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var nowTick = Environment.TickCount64;
        WatchOwnKnock(nowTick);
        WatchHostedDoor(nowTick);
    }

    private void WatchOwnKnock(long nowTick)
    {
        var held = tables.HeldKnock;
        if (held.Stage == KnockStage.Waiting)
        {
            if (!tables.KnockAttended(nowTick))
            {
                tables.PollWaitingKnock();
            }

            return;
        }

        if (!AnswerWorthTelling(held.Stage)
            || (held.Stage == answeredStage && string.Equals(held.RoomId, answeredRoomId, StringComparison.Ordinal)))
        {
            return;
        }

        answeredRoomId = held.RoomId;
        answeredStage = held.Stage;
        if (tables.KnockAttended(nowTick))
        {
            return;
        }

        var admitted = held.Stage == KnockStage.Admitted;
        Tell(Loc.T(admitted ? L.Casino.NotifyAdmittedTitle : L.Casino.NotifyDeniedTitle),
            Loc.T(admitted ? L.Casino.NotifyAdmittedBody : L.Casino.NotifyDeniedBody), held.RoomId);
    }

    private void WatchHostedDoor(long nowTick)
    {
        if (polledAtTick != 0 && nowTick - polledAtTick < HostPollMilliseconds)
        {
            return;
        }

        var tableId = tables.HostedTableId();
        if (tableId.Length == 0 || Interlocked.Exchange(ref polling, 1) != 0)
        {
            return;
        }

        polledAtTick = nowTick;
        StartKnockPoll(tableId);
    }

    private void StartKnockPoll(string tableId)
    {
        work.Run("hosted knocks", async token =>
        {
            var answer = await casino.TableKnocksAsync(tableId, token).ConfigureAwait(false);
            if (answer?.Knocks is { } knocks)
            {
                Hear(tableId, knocks);
            }
        }, () => Interlocked.Exchange(ref polling, 0));
    }

    private void Hear(string tableId, CasinoTableKnockDto[] knocks)
    {
        if (!string.Equals(heardTableId, tableId, StringComparison.Ordinal))
        {
            heardKnocks.Clear();
            heardTableId = tableId;
        }

        var attended = tables.DoorAttended(tableId, Environment.TickCount64);
        for (var index = 0; index < knocks.Length; index++)
        {
            if (!heardKnocks.Add(KnockKey(knocks[index])) || attended)
            {
                continue;
            }

            Tell(Loc.T(L.Casino.NotifyKnockTitle), Loc.T(L.Casino.NotifyKnockBody, knocks[index].DisplayName),
                tableId);
        }
    }

    private void Tell(string title, string body, string roomId)
    {
        notifications.Notify(new PhoneNotification(CasinoTurnNotifier.AppId, title, body, DateTime.Now, accent,
            string.Concat(CasinoTurnNotifier.GroupPrefix, roomId))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
