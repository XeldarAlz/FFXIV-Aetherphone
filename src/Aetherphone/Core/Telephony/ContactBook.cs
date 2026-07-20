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
    private const long RefreshIntervalMs = 30_000;

    private readonly ContactsClient client;
    private readonly AethernetSession session;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private volatile ContactDto[] contacts = Array.Empty<ContactDto>();
    private volatile string myNumber = string.Empty;
    private volatile NumberChangeStatusDto? numberChange;
    private volatile bool loading;
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
        contacts = Array.Empty<ContactDto>();
        myNumber = string.Empty;
        numberChange = null;
        lastRefreshTicks = 0;
    }

    public ContactDto[] Contacts => contacts;
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
        lock (gate)
        {
            if (loading || (!force && now - lastRefreshTicks < RefreshIntervalMs))
            {
                return;
            }

            loading = true;
            lastRefreshTicks = now;
        }

        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var list = await client.ListAsync(token).ConfigureAwait(false);
                if (list is not null)
                {
                    contacts = list.Contacts;
                    myNumber = list.MyNumber;
                }

                var status = await client.NumberChangeStatusAsync(token).ConfigureAwait(false);
                if (status is not null)
                {
                    numberChange = status.Request;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Contact refresh failed: {exception.Message}");
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
                AepLog.Warning($"Contact add failed: {exception.Message}");
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
                AepLog.Warning($"Contact rename failed: {exception.Message}");
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
                AepLog.Warning($"Contact remove failed: {exception.Message}");
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
                AepLog.Warning($"Number change request failed: {exception.Message}");
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

    public static string DisplayLabel(ContactDto contact) =>
        contact.Alias.Length > 0 ? contact.Alias : contact.DisplayName;

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

            contacts = list.ToArray();
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

            contacts = list.ToArray();
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
