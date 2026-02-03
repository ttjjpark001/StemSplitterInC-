namespace StemSplitter.Models;

/// <summary>
/// Options for audio stem separation.
/// </summary>
public class SeparationOptions
{
    /// <summary>
    /// Path to the input audio file (MP3 or WAV).
    /// </summary>
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// Output directory for separated stems. If null, uses input file directory.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// The Demucs model to use for separation.
    /// htdemucs: 4 stems (drums, bass, vocals, other)
    /// htdemucs_6s: 6 stems (drums, bass, vocals, guitar, piano, other)
    /// </summary>
    public string Model { get; set; } = "htdemucs_6s";

    /// <summary>
    /// Output format for stems (wav or mp3).
    /// </summary>
    public string OutputFormat { get; set; } = "wav";

    /// <summary>
    /// Use CPU only (slower but works without GPU).
    /// </summary>
    public bool CpuOnly { get; set; } = false;

    /// <summary>
    /// Number of parallel jobs for processing.
    /// </summary>
    public int Jobs { get; set; } = 1;

    /// <summary>
    /// Shift count for better separation quality (0-5, higher = better but slower).
    /// </summary>
    public int Shifts { get; set; } = 1;
}
