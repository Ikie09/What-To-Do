using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace What_To_Do.Features.Transcriptions;

public enum WhisperModelState
{
    Missing,
    Downloading,
    Ready,
    Failed
}

// Owns the Whisper model: downloads it once (if allowed) and keeps one loaded WhisperFactory for the worker.
public class WhisperModelService : IDisposable
{
    private readonly TranscriptionOptions _options;
    private readonly ILogger<WhisperModelService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;

    public WhisperModelService(IOptions<TranscriptionOptions> options, ILogger<WhisperModelService> logger)
    {
        _options = options.Value;
        _logger = logger;
        State = File.Exists(ModelPath) ? WhisperModelState.Ready : WhisperModelState.Missing;
    }

    public WhisperModelState State { get; private set; }
    public string? LastError { get; private set; }
    public string ModelPath => Path.Combine(_options.ResolveModelDirectory(), _options.ModelFileName);

    // Makes sure the model file exists and is loaded. Returns false when it cannot be made ready.
    public async Task<bool> EnsureReadyAsync(CancellationToken ct)
    {
        if (_factory is not null) return true;

        await _gate.WaitAsync(ct);
        try
        {
            if (_factory is not null) return true;

            if (!File.Exists(ModelPath))
            {
                if (!_options.AutoDownload)
                {
                    State = WhisperModelState.Missing;
                    LastError = $"The Whisper model is missing. Place {_options.ModelFileName} in {_options.ResolveModelDirectory()} or enable Transcription:AutoDownload.";
                    return false;
                }
                await DownloadAsync(ct);
            }

            _factory = WhisperFactory.FromPath(ModelPath);
            State = WhisperModelState.Ready;
            LastError = null;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = WhisperModelState.Failed;
            LastError = ex.Message;
            _logger.LogError(ex, "Could not prepare the Whisper model");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public WhisperProcessor CreateProcessor()
    {
        var builder = _factory!.CreateBuilder().WithLanguage(string.IsNullOrWhiteSpace(_options.Language) ? "auto" : _options.Language);
        return builder.Build();
    }

    private async Task DownloadAsync(CancellationToken ct)
    {
        State = WhisperModelState.Downloading;
        Directory.CreateDirectory(_options.ResolveModelDirectory());

        var type = Enum.TryParse<GgmlType>(_options.Model, ignoreCase: true, out var parsed) ? parsed : GgmlType.Base;
        _logger.LogInformation("Downloading Whisper model {Model} to {Path} (one-time)…", type, ModelPath);

        // Download to a temp name first so an interrupted download never leaves a half-written model behind.
        var temp = ModelPath + ".download";
        await using (var source = await new WhisperGgmlDownloader(new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
                         .GetGgmlModelAsync(type, cancellationToken: ct))
        await using (var target = File.Create(temp))
        {
            await source.CopyToAsync(target, ct);
        }
        File.Move(temp, ModelPath, overwrite: true);
        _logger.LogInformation("Whisper model downloaded.");
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _gate.Dispose();
    }
}
