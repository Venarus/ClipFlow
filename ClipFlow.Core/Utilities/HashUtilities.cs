using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ClipFlow.Core.Utilities;

public static class HashUtilities
{
    public static string ComputeSha256(string content)
    {
        if (content == null) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeSha256(byte[] data)
    {
        if (data == null || data.Length == 0) return string.Empty;
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
