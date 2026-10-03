namespace Aetherphone.Core.Songs;

internal enum QueueSection : byte
{
    PlayingNext,
    Upcoming,
    Autoplay,
}

internal readonly struct QueueEntry
{
    public readonly int Id;
    public readonly Song Song;
    public readonly QueueSection Section;
    public readonly int ContextIndex;

    public QueueEntry(int id, in Song song, QueueSection section, int contextIndex)
    {
        Id = id;
        Song = song;
        Section = section;
        ContextIndex = contextIndex;
    }

    public QueueEntry WithSection(QueueSection section)
    {
        return new QueueEntry(Id, Song, section, ContextIndex);
    }
}

internal sealed class PlayQueue
{
    public const int HistoryCapacity = 100;
    public const int AppendedContextIndex = int.MaxValue;

    private readonly List<QueueEntry> history = new();
    private readonly List<QueueEntry> playingNext = new();
    private readonly List<QueueEntry> upcoming = new();
    private readonly List<QueueEntry> autoplay = new();
    private Song[] context = Array.Empty<Song>();
    private QueueEntry current;
    private bool hasCurrent;
    private bool shuffled;
    private int nextId = 1;

    public int Version { get; private set; }
    public string ContextId { get; private set; } = string.Empty;
    public string ContextTitle { get; private set; } = string.Empty;
    public bool HasCurrent => hasCurrent;
    public QueueEntry Current => current;
    public bool Shuffled => shuffled;
    public int HistoryCount => history.Count;
    public int PlayingNextCount => playingNext.Count;
    public int UpcomingCount => upcoming.Count;
    public int AutoplayCount => autoplay.Count;
    public int QueuedCount => playingNext.Count + upcoming.Count;
    public bool NeedsAutoplay => hasCurrent && QueuedCount < 2 && autoplay.Count == 0;

    public QueueEntry HistoryAt(int index) => history[index];
    public QueueEntry PlayingNextAt(int index) => playingNext[index];
    public QueueEntry UpcomingAt(int index) => upcoming[index];
    public QueueEntry AutoplayAt(int index) => autoplay[index];

    public QueueEntry QueuedAt(int index)
    {
        return index < playingNext.Count ? playingNext[index] : upcoming[index - playingNext.Count];
    }

    public bool SetContext(Song[] songs, int startIndex, bool shuffle, string contextId, string contextTitle)
    {
        if (songs.Length == 0)
        {
            return false;
        }

        var start = Math.Clamp(startIndex, 0, songs.Length - 1);
        context = songs;
        ContextId = contextId;
        ContextTitle = contextTitle;
        shuffled = shuffle;
        PushCurrentToHistory();
        playingNext.Clear();
        autoplay.Clear();
        current = new QueueEntry(nextId++, songs[start], QueueSection.Upcoming, start);
        hasCurrent = true;
        FillUpcomingFromContext(start);
        Version++;
        return true;
    }

    public bool PlayNext(in Song song)
    {
        if (string.IsNullOrEmpty(song.VideoId))
        {
            return false;
        }

        if (!hasCurrent)
        {
            return SetContext(new[] { song }, 0, false, string.Empty, string.Empty);
        }

        playingNext.Insert(0, new QueueEntry(nextId++, song, QueueSection.PlayingNext, AppendedContextIndex));
        Version++;
        return true;
    }

    public bool PlayLast(in Song song)
    {
        if (string.IsNullOrEmpty(song.VideoId))
        {
            return false;
        }

        if (!hasCurrent)
        {
            return SetContext(new[] { song }, 0, false, string.Empty, string.Empty);
        }

        upcoming.Add(new QueueEntry(nextId++, song, QueueSection.Upcoming, AppendedContextIndex));
        Version++;
        return true;
    }

    public void SetAutoplay(Song[] songs)
    {
        autoplay.Clear();
        for (var index = 0; index < songs.Length; index++)
        {
            if (IsQueuedOrCurrent(songs[index].VideoId))
            {
                continue;
            }

            autoplay.Add(new QueueEntry(nextId++, songs[index], QueueSection.Autoplay, AppendedContextIndex));
        }

        Version++;
    }

    public void ClearAutoplay()
    {
        if (autoplay.Count == 0)
        {
            return;
        }

        autoplay.Clear();
        Version++;
    }

    public bool TryAdvance(SongRepeatMode repeat, out QueueEntry next)
    {
        if (!hasCurrent)
        {
            next = default;
            return false;
        }

        if (playingNext.Count == 0 && upcoming.Count == 0 && autoplay.Count == 0 &&
            repeat == SongRepeatMode.All && context.Length > 0)
        {
            PushCurrentToHistory();
            var restart = shuffled ? Random.Shared.Next(context.Length) : 0;
            current = new QueueEntry(nextId++, context[restart], QueueSection.Upcoming, restart);
            hasCurrent = true;
            FillUpcomingFromContext(restart);
            Version++;
            next = current;
            return true;
        }

        if (!TryTakeHead(out var head))
        {
            next = default;
            return false;
        }

        PushCurrentToHistory();
        current = head;
        hasCurrent = true;
        Version++;
        next = current;
        return true;
    }

    public bool TryBack(out QueueEntry previous)
    {
        if (history.Count == 0)
        {
            previous = default;
            return false;
        }

        var last = history[^1];
        history.RemoveAt(history.Count - 1);
        if (hasCurrent)
        {
            ReturnToFront(current);
        }

        current = last;
        hasCurrent = true;
        Version++;
        previous = current;
        return true;
    }

    public bool JumpTo(int entryId, out QueueEntry target)
    {
        if (TryJumpWithin(playingNext, entryId, out target) || TryJumpWithin(upcoming, entryId, out target))
        {
            return true;
        }

        if (!TryJumpWithin(autoplay, entryId, out target))
        {
            return false;
        }

        playingNext.Clear();
        upcoming.Clear();
        return true;
    }

    public bool Remove(int entryId)
    {
        if (RemoveFrom(playingNext, entryId) || RemoveFrom(upcoming, entryId) || RemoveFrom(autoplay, entryId))
        {
            Version++;
            return true;
        }

        return false;
    }

    public bool Move(int entryId, int targetQueuedIndex)
    {
        var sourceIndex = QueuedIndexOf(entryId);
        if (sourceIndex < 0)
        {
            return false;
        }

        var entry = QueuedAt(sourceIndex);
        RemoveQueuedAt(sourceIndex);
        var clamped = Math.Clamp(targetQueuedIndex, 0, QueuedCount);
        if (clamped <= playingNext.Count && (entry.Section == QueueSection.PlayingNext || clamped < playingNext.Count))
        {
            playingNext.Insert(clamped, entry.WithSection(QueueSection.PlayingNext));
        }
        else
        {
            var upcomingIndex = Math.Clamp(clamped - playingNext.Count, 0, upcoming.Count);
            upcoming.Insert(upcomingIndex, entry.WithSection(QueueSection.Upcoming));
        }

        Version++;
        return true;
    }

    public void ClearQueued()
    {
        if (QueuedCount == 0)
        {
            return;
        }

        playingNext.Clear();
        upcoming.Clear();
        Version++;
    }

    public void SetShuffle(bool enabled)
    {
        if (shuffled == enabled)
        {
            return;
        }

        shuffled = enabled;
        if (enabled)
        {
            ShuffleInPlace(upcoming);
        }
        else
        {
            RestoreContextOrder();
        }

        Version++;
    }

    public void Clear()
    {
        history.Clear();
        playingNext.Clear();
        upcoming.Clear();
        autoplay.Clear();
        context = Array.Empty<Song>();
        ContextId = string.Empty;
        ContextTitle = string.Empty;
        current = default;
        hasCurrent = false;
        Version++;
    }

    public int QueuedIndexOf(int entryId)
    {
        for (var index = 0; index < playingNext.Count; index++)
        {
            if (playingNext[index].Id == entryId)
            {
                return index;
            }
        }

        for (var index = 0; index < upcoming.Count; index++)
        {
            if (upcoming[index].Id == entryId)
            {
                return playingNext.Count + index;
            }
        }

        return -1;
    }

    private bool TryTakeHead(out QueueEntry head)
    {
        if (playingNext.Count > 0)
        {
            head = playingNext[0];
            playingNext.RemoveAt(0);
            return true;
        }

        if (upcoming.Count > 0)
        {
            head = upcoming[0];
            upcoming.RemoveAt(0);
            return true;
        }

        if (autoplay.Count > 0)
        {
            head = autoplay[0];
            autoplay.RemoveAt(0);
            return true;
        }

        head = default;
        return false;
    }

    private bool TryJumpWithin(List<QueueEntry> section, int entryId, out QueueEntry target)
    {
        for (var index = 0; index < section.Count; index++)
        {
            if (section[index].Id != entryId)
            {
                continue;
            }

            PushCurrentToHistory();
            for (var skipped = 0; skipped < index; skipped++)
            {
                PushToHistory(section[skipped]);
            }

            target = section[index];
            section.RemoveRange(0, index + 1);
            current = target;
            hasCurrent = true;
            Version++;
            return true;
        }

        target = default;
        return false;
    }

    private void ReturnToFront(in QueueEntry entry)
    {
        switch (entry.Section)
        {
            case QueueSection.PlayingNext:
                playingNext.Insert(0, entry);
                break;
            case QueueSection.Autoplay:
                autoplay.Insert(0, entry);
                break;
            default:
                upcoming.Insert(0, entry);
                break;
        }
    }

    private void RemoveQueuedAt(int queuedIndex)
    {
        if (queuedIndex < playingNext.Count)
        {
            playingNext.RemoveAt(queuedIndex);
            return;
        }

        upcoming.RemoveAt(queuedIndex - playingNext.Count);
    }

    private static bool RemoveFrom(List<QueueEntry> section, int entryId)
    {
        for (var index = 0; index < section.Count; index++)
        {
            if (section[index].Id == entryId)
            {
                section.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    private bool IsQueuedOrCurrent(string videoId)
    {
        if (hasCurrent && string.Equals(current.Song.VideoId, videoId, StringComparison.Ordinal))
        {
            return true;
        }

        return QueuedIndexOfVideo(videoId) >= 0;
    }

    private int QueuedIndexOfVideo(string videoId)
    {
        for (var index = 0; index < QueuedCount; index++)
        {
            if (string.Equals(QueuedAt(index).Song.VideoId, videoId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void FillUpcomingFromContext(int startIndex)
    {
        upcoming.Clear();
        if (shuffled)
        {
            for (var index = 0; index < context.Length; index++)
            {
                if (index != startIndex)
                {
                    upcoming.Add(new QueueEntry(nextId++, context[index], QueueSection.Upcoming, index));
                }
            }

            ShuffleInPlace(upcoming);
            return;
        }

        for (var index = startIndex + 1; index < context.Length; index++)
        {
            upcoming.Add(new QueueEntry(nextId++, context[index], QueueSection.Upcoming, index));
        }
    }

    private void RestoreContextOrder()
    {
        if (upcoming.Count < 2)
        {
            return;
        }

        upcoming.Sort(static (left, right) =>
        {
            var byContext = left.ContextIndex.CompareTo(right.ContextIndex);
            return byContext != 0 ? byContext : left.Id.CompareTo(right.Id);
        });
        if (!hasCurrent || current.ContextIndex == AppendedContextIndex)
        {
            return;
        }

        var firstAfterCurrent = 0;
        while (firstAfterCurrent < upcoming.Count && upcoming[firstAfterCurrent].ContextIndex < current.ContextIndex)
        {
            firstAfterCurrent++;
        }

        if (firstAfterCurrent == 0 || firstAfterCurrent == upcoming.Count)
        {
            return;
        }

        var wrapped = upcoming.GetRange(0, firstAfterCurrent);
        upcoming.RemoveRange(0, firstAfterCurrent);
        var appendedStart = upcoming.Count;
        while (appendedStart > 0 && upcoming[appendedStart - 1].ContextIndex == AppendedContextIndex)
        {
            appendedStart--;
        }

        upcoming.InsertRange(appendedStart, wrapped);
    }

    private static void ShuffleInPlace(List<QueueEntry> entries)
    {
        for (var index = entries.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (entries[index], entries[swapIndex]) = (entries[swapIndex], entries[index]);
        }
    }

    private void PushCurrentToHistory()
    {
        if (!hasCurrent)
        {
            return;
        }

        PushToHistory(current);
        hasCurrent = false;
    }

    private void PushToHistory(in QueueEntry entry)
    {
        history.Add(entry);
        if (history.Count > HistoryCapacity)
        {
            history.RemoveAt(0);
        }
    }
}
