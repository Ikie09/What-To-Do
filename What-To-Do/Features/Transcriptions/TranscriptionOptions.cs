namespace What_To_Do.Features.Transcriptions;

// Bound from the "Transcription" configuration section.
public class TranscriptionOptions
{
    public bool Enabled { get; set; } = true;

    // Whisper model size: Tiny (~75 MB, fastest), Base (~142 MB, good default), Small (~466 MB, most accurate here).
    public string Model { get; set; } = "Base";

    // Where the ggml model file lives. Empty = a "models" folder next to the app.
    public string? ModelDirectory { get; set; }

    // Download the model on first start when it is missing (needs internet once).
    public bool AutoDownload { get; set; } = true;

    public int MaxAudioSeconds { get; set; } = 900;

    // "auto" lets Whisper detect the spoken language; or a code such as "en", "sw", "fr".
    public string Language { get; set; } = "auto";

    public string ResolveModelDirectory() =>
        string.IsNullOrWhiteSpace(ModelDirectory) ? Path.Combine(AppContext.BaseDirectory, "models") : ModelDirectory;

    public string ModelFileName => $"ggml-{Model.ToLowerInvariant()}.bin";
}
