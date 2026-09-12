using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Telephony;

internal enum AddContactOutcome
{
    Added,
    InvalidNumber,
    NotFound,
    RateLimited,
    Failed,
}

internal sealed class ContactBook : IDisposable
{
    private const long RefreshIntervalMs = 15_000;

    private static readonly Dictionary<string, string> NoAliases = new(StringComparer.Ordinal);

    private readonly ContactsClient client;
    private readonly AethernetSession session;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private volatile ContactDto[] contacts = Array.Empty<ContactDto>();
    private volatile Dictionary<string, string> aliases = NoAliases;
    private volatile int version;
    private volatile string myNumber = string.Empty;
    private volatile NumberChangeStatusDto? numberChange;
    private volatile bool loading;
    private volatile bool numberChangeLoaded;
    private long lastRefreshTicks;
    private string? lastAccountId;

    public ContactBook(ContactsClient client, AethernetSession session)
    {
        this.client = client;
        this.session = session;
        session.Changed += OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        Publish(Array.Empty<ContactDto>());
        myNumber = string.Empty;
        numberChange = null;
        numberChangeLoaded = false;
        lastRefreshTicks = 0;
    }

    public ContactDto[] Contacts => contacts;
    public int Version => version;
    public string MyNumber => myNumber;
    public NumberChangeStatusDto? NumberChange => numberChange;
    public bool Loading => loading;

    public void Refresh(bool force = false)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (!force && now - lastRefreshTicks < RefreshIntervalMs)
        {
            return;
        }

        bool withNumberChange;
        lock (gate)
        {
            if (loading || (!force && now - lastRefreshTicks < RefreshIntervalMs))
            {
                return;
            }

            loading = true;
            lastRefreshTicks = now;
            withNumberChange = force || !numberChangeLoaded;
        }

        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var list = await client.ListAsync(token).ConfigureAwait(false);
                if (list is not null)
                {
                    Publish(list.Contacts);
                    myNumber = list.MyNumber;
                }

                if (withNumberChange)
                {
                    var status = await client.NumberChangeStatusAsync(token).ConfigureAwait(false);
                    if (status is not null)
                    {
                        numberChange = status.Request;
                        numberChangeLoaded = true;
                    }
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Contact refresh failed");
            }
            finally
            {
                loading = false;
            }
        });
    }

    public void Add(string number, string? alias, Action<AddContactOutcome, ContactDto?> done)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var status = 0;
            ContactDto? added = null;
            try
            {
                added = await client.AddAsync(number, alias, token, code => status = code).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Contact add failed");
            }

            if (added is not null)
            {
                Merge(added);
                done(AddContactOutcome.Added, added);
                return;
            }

            done(status switch
            {
                400 => AddContactOutcome.InvalidNumber,
                404 => AddContactOutcome.NotFound,
                429 => AddContactOutcome.RateLimited,
                _ => AddContactOutcome.Failed,
            }, null);
        });
    }

    public void Rename(string userId, string alias, Action<bool> done)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            ContactDto? updated = null;
            try
            {
                updated = await client.UpdateAliasAsync(userId, alias, token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Contact rename failed");
            }

            if (updated is not null)
            {
                Merge(updated);
            }

            done(updated is not null);
        });
    }

    public void Remove(string userId, Action<bool> done)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await client.RemoveAsync(userId, token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Contact remove failed");
            }

            if (ok)
            {
                RemoveLocal(userId);
            }

            done(ok);
        });
    }

    public void RequestNumberChange(string reason, Action<bool> done)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            NumberChangeStatusResult? result = null;
            try
            {
                result = await client.RequestNumberChangeAsync(reason, token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Number change request failed");
            }

            if (result?.Request is not null)
            {
                numberChange = result.Request;
            }

            done(result?.Request is not null);
        });
    }

    public ContactDto? Find(string userId)
    {
        var snapshot = contacts;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].UserId == userId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public string NameFor(string? userId, string fallback) =>
        userId is not null && aliases.TryGetValue(userId, out var alias) ? alias : fallback;

    public static string DisplayLabel(ContactDto contact) =>
        contact.Alias.Length > 0 ? contact.Alias
        : contact.DisplayName.Length > 0 ? contact.DisplayName
        : contact.Handle;

    public static string Format(string number)
    {
        if (number.Length != 7)
        {
            return number;
        }

        var builder = new StringBuilder(8);
        builder.Append(number, 0, 3);
        builder.Append('-');
        builder.Append(number, 3, 4);
        return builder.ToString();
    }

    private void Merge(ContactDto added)
    {
        lock (gate)
        {
            var snapshot = contacts;
            var list = new List<ContactDto>(snapshot.Length + 1);
            var replaced = false;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].UserId == added.UserId)
                {
                    list.Add(added);
                    replaced = true;
                }
                else
                {
                    list.Add(snapshot[index]);
                }
            }

            if (!replaced)
            {
                list.Add(added);
            }

            Publish(list.ToArray());
        }
    }

    private void RemoveLocal(string userId)
    {
        lock (gate)
        {
            var snapshot = contacts;
            var list = new List<ContactDto>(snapshot.Length);
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].UserId != userId)
                {
                    list.Add(snapshot[index]);
                }
            }

            Publish(list.ToArray());
        }
    }

    private void Publish(ContactDto[] list)
    {
        var aliasIndex = NoAliases;
        for (var contactIndex = 0; contactIndex < list.Length; contactIndex++)
        {
            var contact = list[contactIndex];
            if (contact.Alias.Length == 0)
            {
                continue;
            }

            if (ReferenceEquals(aliasIndex, NoAliases))
            {
                aliasIndex = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            aliasIndex[contact.UserId] = contact.Alias;
        }

        lock (gate)
        {
            aliases = aliasIndex;
            contacts = list;
            version++;
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
