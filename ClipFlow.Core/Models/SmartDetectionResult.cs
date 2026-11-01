namespace ClipFlow.Core.Models;

public class SmartDetectionResult
{
    public SmartType DetectedType { get; set; } = SmartType.None;
    public string DisplayText { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? ColorHex { get; set; }
    public string? ColorRgb { get; set; }
    public string? CodeLanguage { get; set; }
    public int LineCount { get; set; }
    public int WordCount { get; set; }
    public int CharCount { get; set; }
    public string? FilePath { get; set; }
    public bool FileExists { get; set; }
    public string? FileExtension { get; set; }
    public string? FileSizeFormatted { get; set; }
}
