namespace StemSplitter.Models;

/// <summary>
/// Represents progress information during stem separation.
/// </summary>
public class StemProgress
{
    public StemProgressType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OverallPercent { get; set; }
    public string? CurrentStem { get; set; }
    public int StemPercent { get; set; }
    public int StemIndex { get; set; }
    public int TotalStems { get; set; }

    public static StemProgress Info(string message) => new()
    {
        Type = StemProgressType.Info,
        Message = message
    };

    public static StemProgress Overall(int percent, string message) => new()
    {
        Type = StemProgressType.OverallProgress,
        OverallPercent = percent,
        Message = message
    };

    public static StemProgress Stem(string stemName, int stemIndex, int totalStems, int percent) => new()
    {
        Type = StemProgressType.StemProgress,
        CurrentStem = stemName,
        StemIndex = stemIndex,
        TotalStems = totalStems,
        StemPercent = percent,
        Message = $"Processing {stemName}..."
    };

    public static StemProgress StemComplete(string stemName, int stemIndex, int totalStems) => new()
    {
        Type = StemProgressType.StemComplete,
        CurrentStem = stemName,
        StemIndex = stemIndex,
        TotalStems = totalStems,
        StemPercent = 100,
        Message = $"Completed: {stemName}"
    };
}

public enum StemProgressType
{
    Info,
    OverallProgress,
    StemProgress,
    StemComplete
}
