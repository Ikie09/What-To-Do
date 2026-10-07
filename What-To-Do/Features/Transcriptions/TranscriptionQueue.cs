using System.Collections.Concurrent;
using System.Threading.Channels;

namespace What_To_Do.Features.Transcriptions;

public enum TranscriptionStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

public class TranscriptionJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public double DurationSeconds { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public TranscriptionStatus Status { get; set; } = TranscriptionStatus.Queued;
    public string? Text { get; set; }
    public string? Error { get; set; }
    public DateTime? FinishedAt { get; set; }
}

// In-memory on purpose: transcripts are handed back once and are never stored in the database.
public class TranscriptionQueue
{
    private static readonly TimeSpan ResultRetention = TimeSpan.FromMinutes(30);

    private readonly Channel<TranscriptionJob> _channel = Channel.CreateUnbounded<TranscriptionJob>();
    private readonly ConcurrentDictionary<Guid, TranscriptionJob> _jobs = new();
    private readonly object _gate = new();

    public ChannelReader<TranscriptionJob> Reader => _channel.Reader;

    // One unfinished job per user keeps a single account from filling the (single) CPU worker.
    public bool TryEnqueue(TranscriptionJob job)
    {
        lock (_gate)
        {
            if (_jobs.Values.Any(j => j.UserId == job.UserId && IsActive(j))) return false;
            _jobs[job.Id] = job;
        }
        _channel.Writer.TryWrite(job);
        return true;
    }

    public TranscriptionJob? Get(Guid id, string userId) =>
        _jobs.TryGetValue(id, out var job) && job.UserId == userId ? job : null;

    // A job the user deleted while it waited must not be processed.
    public bool Exists(Guid id) => _jobs.ContainsKey(id);

    public TranscriptionJob? Remove(Guid id, string userId)
    {
        var job = Get(id, userId);
        if (job is not null) _jobs.TryRemove(id, out _);
        return job;
    }

    // Old finished jobs (and their text) are dropped; returns audio files that should be deleted too.
    public void Purge(DateTime now)
    {
        foreach (var job in _jobs.Values)
        {
            if (job.FinishedAt is { } finished && now - finished > ResultRetention) _jobs.TryRemove(job.Id, out _);
        }
    }

    private static bool IsActive(TranscriptionJob job) =>
        job.Status is TranscriptionStatus.Queued or TranscriptionStatus.Processing;
}
