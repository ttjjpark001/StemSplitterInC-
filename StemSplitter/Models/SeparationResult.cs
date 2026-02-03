namespace StemSplitter.Models;

/// <summary>
/// Represents the result of audio stem separation.
/// </summary>
public class SeparationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public Dictionary<StemType, string> StemFiles { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }

    public static SeparationResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };

    public static SeparationResult Succeeded(string outputDir, Dictionary<StemType, string> stems, TimeSpan time) => new()
    {
        Success = true,
        OutputDirectory = outputDir,
        StemFiles = stems,
        ProcessingTime = time
    };
}
