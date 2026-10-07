using System.Buffers.Binary;

namespace What_To_Do.Features.Transcriptions;

public record WavInfo(double DurationSeconds);

// The browser sends 16 kHz mono 16-bit PCM WAV (what Whisper wants), so the server needs no ffmpeg.
// This only reads the header; it never decodes the audio.
public static class WavValidator
{
    public const int SampleRate = 16_000;
    private const int BytesPerSecond = SampleRate * 2;

    public static bool TryValidate(ReadOnlySpan<byte> header, long fileLength, out WavInfo? info, out string error)
    {
        info = null;
        error = string.Empty;

        if (header.Length < 44 || !header[..4].SequenceEqual("RIFF"u8) || !header.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            error = "That is not a WAV file.";
            return false;
        }

        var position = 12;
        var haveFormat = false;
        long dataBytes = -1;

        while (position + 8 <= header.Length)
        {
            var id = header.Slice(position, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(position + 4, 4));
            var body = position + 8;

            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 16 || body + 16 > header.Length)
                {
                    error = "The WAV header is damaged.";
                    return false;
                }

                var format = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(body, 2));
                var channels = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(body + 2, 2));
                var rate = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(body + 4, 4));
                var bits = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(body + 14, 2));

                if (format != 1 || channels != 1 || rate != SampleRate || bits != 16)
                {
                    error = $"Audio must be 16 kHz, mono, 16-bit PCM WAV (got {rate} Hz, {channels} channel(s), {bits}-bit).";
                    return false;
                }
                haveFormat = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                // A streamed WAV can leave the size as 0 / 0xFFFFFFFF; fall back to the file length then.
                dataBytes = size == 0 || size == uint.MaxValue ? Math.Max(0, fileLength - body) : size;
                break;
            }

            position = body + (int)size + (int)(size & 1);
        }

        if (!haveFormat || dataBytes < 0)
        {
            error = "The WAV header is incomplete.";
            return false;
        }
        if (dataBytes < BytesPerSecond / 4)
        {
            error = "The recording is too short to transcribe.";
            return false;
        }

        info = new WavInfo(dataBytes / (double)BytesPerSecond);
        return true;
    }
}
