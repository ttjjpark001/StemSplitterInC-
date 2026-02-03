using NAudio.Wave;

namespace StemSplitter.Services;

/// <summary>
/// Handles audio file operations like format detection and conversion.
/// </summary>
public class AudioProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac"
    };

    /// <summary>
    /// Validates that the input file exists and is a supported audio format.
    /// </summary>
    public (bool IsValid, string? Error) ValidateInputFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return (false, "Input file path is required.");

        if (!File.Exists(filePath))
            return (false, $"File not found: {filePath}");

        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
            return (false, $"Unsupported audio format: {extension}. Supported: {string.Join(", ", SupportedExtensions)}");

        return (true, null);
    }

    /// <summary>
    /// Gets audio file information.
    /// </summary>
    public AudioFileInfo GetAudioInfo(string filePath)
    {
        var info = new AudioFileInfo { FilePath = filePath };

        try
        {
            using var reader = CreateAudioReader(filePath);
            if (reader != null)
            {
                info.Duration = reader.TotalTime;
                info.SampleRate = reader.WaveFormat.SampleRate;
                info.Channels = reader.WaveFormat.Channels;
                info.BitsPerSample = reader.WaveFormat.BitsPerSample;
            }
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }

        return info;
    }

    /// <summary>
    /// Converts an audio file to WAV format if needed.
    /// </summary>
    public string? ConvertToWavIfNeeded(string inputPath, string outputDirectory)
    {
        var extension = Path.GetExtension(inputPath).ToLowerInvariant();

        if (extension == ".wav")
            return inputPath;

        var outputPath = Path.Combine(outputDirectory,
            Path.GetFileNameWithoutExtension(inputPath) + "_converted.wav");

        try
        {
            using var reader = CreateAudioReader(inputPath);
            if (reader == null)
                return null;

            WaveFileWriter.CreateWaveFile16(outputPath, reader.ToSampleProvider());
            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    private static WaveStream? CreateAudioReader(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".mp3" => new Mp3FileReader(filePath),
            ".wav" => new WaveFileReader(filePath),
            _ => new AudioFileReader(filePath)
        };
    }
}

public class AudioFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public string? Error { get; set; }

    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Error))
            return $"Error: {Error}";

        return $"Duration: {Duration:hh\\:mm\\:ss}, Sample Rate: {SampleRate}Hz, Channels: {Channels}, Bits: {BitsPerSample}";
    }
}
