using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace What_To_Do.Features.Transcriptions;

[ApiController]
[Route("api/transcriptions")]
[Authorize]
public class TranscriptionController : ControllerBase
{
    private const long MaxUploadBytes = 30_000_000; // 15 min of 16 kHz mono PCM is ~29 MB

    private readonly TranscriptionQueue _queue;
    private readonly WhisperModelService _model;
    private readonly TranscriptionOptions _options;

    public TranscriptionController(TranscriptionQueue queue, WhisperModelService model, IOptions<TranscriptionOptions> options)
    {
        _queue = queue;
        _model = model;
        _options = options.Value;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Lets the web app decide whether to show the "record & transcribe" button.
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new
    {
        enabled = _options.Enabled,
        ready = _options.Enabled && _model.State == WhisperModelState.Ready,
        downloading = _model.State == WhisperModelState.Downloading,
        error = _model.State == WhisperModelState.Failed || _model.State == WhisperModelState.Missing ? _model.LastError : null,
        model = _options.Model,
        maxSeconds = _options.MaxAudioSeconds
    });

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes + 1_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 1_000_000)]
    public async Task<IActionResult> Create(IFormFile? file, CancellationToken ct)
    {
        if (!_options.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Transcription is turned off on the server." });
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Attach a WAV recording as the 'file' field." });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { message = "The recording is too large." });

        // Look at the header before saving anything.
        var header = new byte[4096];
        int read;
        await using (var peek = file.OpenReadStream())
        {
            read = await peek.ReadAsync(header.AsMemory(), ct);
        }

        if (!WavValidator.TryValidate(header.AsSpan(0, read), file.Length, out var info, out var error))
            return BadRequest(new { message = error });
        if (info!.DurationSeconds > _options.MaxAudioSeconds)
            return BadRequest(new { message = $"Recordings can be at most {_options.MaxAudioSeconds / 60} minutes." });

        var folder = Path.Combine(Path.GetTempPath(), "what-to-do-transcriptions");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{Guid.NewGuid():N}.wav");

        await using (var target = System.IO.File.Create(path))
        {
            await file.CopyToAsync(target, ct);
        }

        var job = new TranscriptionJob { UserId = CurrentUserId, FilePath = path, DurationSeconds = info.DurationSeconds };
        if (!_queue.TryEnqueue(job))
        {
            System.IO.File.Delete(path);
            return Conflict(new { message = "You already have a recording being transcribed. Wait for it to finish." });
        }

        return Accepted($"/api/transcriptions/{job.Id}", new { id = job.Id, status = job.Status.ToString().ToLowerInvariant() });
    }

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        var job = _queue.Get(id, CurrentUserId);
        if (job is null) return NotFound(new { message = "That transcription was not found (results are kept for 30 minutes)." });

        return Ok(new
        {
            id = job.Id,
            status = job.Status.ToString().ToLowerInvariant(),
            text = job.Text,
            error = job.Error,
            durationSeconds = Math.Round(job.DurationSeconds, 1)
        });
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var job = _queue.Remove(id, CurrentUserId);
        return job is null ? NotFound(new { message = "That transcription was not found." }) : NoContent();
    }
}
