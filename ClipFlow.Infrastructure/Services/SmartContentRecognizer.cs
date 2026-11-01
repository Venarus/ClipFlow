using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;
using ClipFlow.Core.Utilities;

namespace ClipFlow.Infrastructure.Services;

public partial class SmartContentRecognizer : ISmartContentRecognizer
{
    // Regex patterns compiled for performance
    private static readonly Regex HexColorRegex = new(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);
    private static readonly Regex RgbColorRegex = new(@"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*([\d\.]+))?\s*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmailRegex = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^(\+?\d{1,3}[-.\s]?)?(\(?\d{2,4}\)?[-.\s]?)?\d{3,4}[-.\s]?\d{3,4}$", RegexOptions.Compiled);
    private static readonly Regex WindowsPathRegex = new(@"^([a-zA-Z]:\\|\\\\)[^\r\n]+$", RegexOptions.Compiled);

    public SmartDetectionResult Recognize(string? text, ContentType baseType, List<string>? filePaths = null)
    {
        var result = new SmartDetectionResult();

        if (filePaths != null && filePaths.Count > 0)
        {
            var first = filePaths[0];
            result.DetectedType = SmartType.FilePath;
            result.FilePath = first;
            result.DisplayText = filePaths.Count == 1 ? first : $"{filePaths.Count} files selected";
            result.FileExists = File.Exists(first) || Directory.Exists(first);
            if (File.Exists(first))
            {
                var fi = new FileInfo(first);
                result.FileExtension = fi.Extension.ToLowerInvariant();
                result.FileSizeFormatted = HashUtilities.FormatFileSize(fi.Length);
            }
            return result;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            result.DisplayText = baseType.ToString();
            return result;
        }

        var trimmed = text.Trim();

        // Calculate text statistics
        result.CharCount = text.Length;
        result.LineCount = text.Split('\n').Length;
        result.WordCount = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        // 1. Color check (Hex)
        var hexMatch = HexColorRegex.Match(trimmed);
        if (hexMatch.Success)
        {
            var hex = NormalizeHexColor(trimmed);
            if (hex != null)
            {
                result.DetectedType = SmartType.HexColor;
                result.ColorHex = hex;
                result.ColorRgb = HexToRgbString(hex);
                result.DisplayText = $"HEX Color: {hex}";
                return result;
            }
        }

        // 2. Color check (RGB/RGBA)
        var rgbMatch = RgbColorRegex.Match(trimmed);
        if (rgbMatch.Success)
        {
            int r = int.Parse(rgbMatch.Groups[1].Value);
            int g = int.Parse(rgbMatch.Groups[2].Value);
            int b = int.Parse(rgbMatch.Groups[3].Value);
            if (r <= 255 && g <= 255 && b <= 255)
            {
                var hex = $"#{r:X2}{g:X2}{b:X2}";
                result.DetectedType = SmartType.RgbColor;
                result.ColorHex = hex;
                result.ColorRgb = trimmed;
                result.DisplayText = $"RGB Color: {trimmed}";
                return result;
            }
        }

        // 3. URL check
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp))
        {
            result.DetectedType = SmartType.Url;
            result.Domain = uri.Host;
            result.DisplayText = uri.Host;
            return result;
        }

        // 4. Email check
        if (EmailRegex.IsMatch(trimmed))
        {
            result.DetectedType = SmartType.Email;
            result.DisplayText = trimmed;
            return result;
        }

        // 5. File Path check (single line)
        if (WindowsPathRegex.IsMatch(trimmed) && trimmed.Length < 260)
        {
            bool exists = false;
            try
            {
                exists = File.Exists(trimmed) || Directory.Exists(trimmed);
            }
            catch
            {
                // Path format errors
            }

            if (exists)
            {
                result.DetectedType = SmartType.FilePath;
                result.FilePath = trimmed;
                result.FileExists = true;
                if (File.Exists(trimmed))
                {
                    try
                    {
                        var fi = new FileInfo(trimmed);
                        result.FileExtension = fi.Extension.ToLowerInvariant();
                        result.FileSizeFormatted = HashUtilities.FormatFileSize(fi.Length);
                    }
                    catch { }
                }
                result.DisplayText = Path.GetFileName(trimmed);
                return result;
            }
        }

        // 6. Phone number check (if concise)
        if (trimmed.Length >= 7 && trimmed.Length <= 25 && PhoneRegex.IsMatch(trimmed))
        {
            int digitCount = trimmed.Count(char.IsDigit);
            if (digitCount >= 7 && digitCount <= 15)
            {
                result.DetectedType = SmartType.Phone;
                result.DisplayText = trimmed;
                return result;
            }
        }

        // 7. Date check
        if (trimmed.Length >= 6 && trimmed.Length <= 35)
        {
            if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dt) ||
                DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                // Only consider it a date if it's not a plain small number or isolated year
                if (trimmed.Contains('/') || trimmed.Contains('-') || trimmed.Contains('.') || trimmed.Contains(' ') || trimmed.Contains(':'))
                {
                    result.DetectedType = SmartType.Date;
                    result.DisplayText = dt.ToString("F", CultureInfo.CurrentCulture);
                    return result;
                }
            }
        }

        // 8. Code snippet check
        var codeLang = DetectCodeLanguage(trimmed);
        if (codeLang != null)
        {
            result.DetectedType = SmartType.Code;
            result.CodeLanguage = codeLang;
            result.DisplayText = $"{codeLang} Code ({result.LineCount} lines)";
            return result;
        }

        // Default text
        result.DetectedType = SmartType.None;
        result.DisplayText = trimmed.Length > 80 ? trimmed.Substring(0, 80) + "..." : trimmed;
        return result;
    }

    private static string? NormalizeHexColor(string hex)
    {
        hex = hex.Trim().ToUpperInvariant();
        if (!hex.StartsWith("#")) return null;

        var raw = hex.Substring(1);
        if (raw.Length == 3) // #RGB -> #RRGGBB
        {
            return $"#{raw[0]}{raw[0]}{raw[1]}{raw[1]}{raw[2]}{raw[2]}";
        }
        if (raw.Length == 4) // #RGBA -> #RRGGBBAA
        {
            return $"#{raw[0]}{raw[0]}{raw[1]}{raw[1]}{raw[2]}{raw[2]}{raw[3]}{raw[3]}";
        }
        if (raw.Length == 6 || raw.Length == 8)
        {
            return hex;
        }
        return null;
    }

    private static string HexToRgbString(string hex)
    {
        try
        {
            var raw = hex.TrimStart('#');
            if (raw.Length == 6)
            {
                int r = Convert.ToInt32(raw.Substring(0, 2), 16);
                int g = Convert.ToInt32(raw.Substring(2, 2), 16);
                int b = Convert.ToInt32(raw.Substring(4, 2), 16);
                return $"rgb({r}, {g}, {b})";
            }
            if (raw.Length == 8)
            {
                int r = Convert.ToInt32(raw.Substring(0, 2), 16);
                int g = Convert.ToInt32(raw.Substring(2, 2), 16);
                int b = Convert.ToInt32(raw.Substring(4, 2), 16);
                int a = Convert.ToInt32(raw.Substring(6, 2), 16);
                return $"rgba({r}, {g}, {b}, {(a / 255.0):0.##})";
            }
        }
        catch { }
        return hex;
    }

    private static string? DetectCodeLanguage(string text)
    {
        if (text.Length < 10) return null;

        // JSON check
        if ((text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]")))
        {
            if (text.Contains("\":") || text.Contains("\",") || text.Contains("null") || text.Contains("true") || text.Contains("false"))
            {
                return "JSON";
            }
        }

        // HTML / XML check
        if ((text.StartsWith("<") && text.EndsWith(">")) || 
            (text.Contains("<!DOCTYPE") || text.Contains("<html") || text.Contains("<div") || text.Contains("<script") || text.Contains("</")))
        {
            return "HTML/XML";
        }

        // SQL check
        var upper = text.ToUpperInvariant();
        if ((upper.Contains("SELECT ") && upper.Contains(" FROM ")) ||
            (upper.Contains("INSERT INTO ") && upper.Contains(" VALUES")) ||
            (upper.Contains("UPDATE ") && upper.Contains(" SET ")) ||
            (upper.Contains("CREATE TABLE ")) ||
            (upper.Contains("DELETE FROM ")))
        {
            return "SQL";
        }

        // C# check
        if (text.Contains("namespace ") || 
            text.Contains("using System;") || 
            text.Contains("public class ") || 
            text.Contains("private void ") || 
            text.Contains("Task<") || 
            text.Contains("Console.WriteLine"))
        {
            return "C#";
        }

        // Python check
        if (text.Contains("def ") || 
            text.Contains("import ") && (text.Contains("from ") || text.Contains("as ")) || 
            text.Contains("if __name__ == '__main__':") ||
            (text.Contains("print(") && text.Contains(":\n")))
        {
            return "Python";
        }

        // JavaScript / TypeScript check
        if (text.Contains("const ") || 
            text.Contains("let ") || 
            text.Contains("function(") || 
            text.Contains("=> {") || 
            text.Contains("export default") || 
            text.Contains("console.log("))
        {
            return "JavaScript";
        }

        // CSS check
        if (text.Contains("{") && text.Contains("}") && (text.Contains("margin:") || text.Contains("padding:") || text.Contains("display:") || text.Contains("color:")))
        {
            return "CSS";
        }

        // Generic code indicator (semicolons + brackets + multiple lines)
        if (text.Contains("{") && text.Contains("}") && text.Contains(";") && text.Contains("\n"))
        {
            return "Code";
        }

        return null;
    }
}
