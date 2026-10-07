using System.Text;
using Microsoft.Extensions.Options;

namespace What_To_Do.Features.Transcriptions;

// Processes one recording at a time so Whisper never competes with itself for the CPU.
public class TranscriptionWorker : BackgroundService
{
    private readonly TranscriptionQueue _queue;
    private readonly WhisperModelService _model;
    private readonly TranscriptionOptions _options;
    private readonly ILogger<TranscriptionWorker> _logger;

    public TranscriptionWorker(
        TranscriptionQueue queue,
        WhisperModelService model,
        IOptions<TranscriptionOptions> options,
        ILogger<TranscriptionWorker> logger)
    {
        _queue = queue;
        _model = model;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Transcription is disabled (Transcription:Enabled = false).");
            return;
        }

        // Fetch/load the model in the background so the first recording doesn't wait for a download.
        _ = Task.Run(() => _model.EnsureReadyAsync(stoppingToken), stoppingToken);

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _queue.Purge(DateTime.UtcNow);
            await ProcessAsync(job, stoppingToken);
        }
    }

    private async Task ProcessAsync(TranscriptionJob job, CancellationToken ct)
    {
        try
        {
            // The user may have cancelled while it was waiting.
            if (!_queue.Exists(job.Id)) return;

            job.Status = TranscriptionStatus.Processing;

            if (!await _model.EnsureReadyAsync(ct))
            {
                Fail(job, _model.LastError ?? "The speech model is not ready yet.");
                return;
            }

            var text = new StringBuilder();
            await using var audio = File.OpenRead(job.FilePath);
            using var processor = _model.CreateProcessor();

            await foreach (var segment in processor.ProcessAsync(audio, ct))
            {
                text.Append(segment.Text);
            }

            job.Text = text.ToString().Trim();
            job.Status = TranscriptionStatus.Completed;
            job.FinishedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription {Id} failed", job.Id);
            Fail(job, "The recording could not be transcribed.");
        }
        finally
        {
            DeleteAudio(job);
        }
    }

    private static void Fail(TranscriptionJob job, string error)
    {
        job.Error = error;
        job.Status = TranscriptionStatus.Failed;
        job.FinishedAt = DateTime.UtcNow;
    }

    // The audio is only needed while transcribing, so it is removed as soon as the job ends.
    private static void DeleteAudio(TranscriptionJob job)
    {
        try { File.Delete(job.FilePath); } catch (IOException) { /* temp folder is cleaned up on restart */ }
    }
}
