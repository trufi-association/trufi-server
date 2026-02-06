namespace Gateway.Middleware;

public static class PayloadHelper
{
    // Reasonable limit: 1MB
    public const int MaxPayloadSize = 1_048_576; // 1 MB

    /// <summary>
    /// Detects if the content-type is binary (images, videos, PDFs, etc.)
    /// that should not be saved as text in PostgreSQL.
    /// </summary>
    public static bool IsBinaryContent(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        // Extract base type (without charset, boundary, etc)
        var baseType = contentType.Split(';')[0].Trim();

        // Common binary types that cause problems with PostgreSQL UTF-8
        return baseType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
               baseType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
               baseType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               baseType.StartsWith("application/zip", StringComparison.OrdinalIgnoreCase) ||
               baseType.StartsWith("application/x-", StringComparison.OrdinalIgnoreCase) || // .exe, .tar, etc
               baseType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects if the content-type is text/JSON that can be safely saved in PostgreSQL.
    /// </summary>
    public static bool IsTextContent(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        var baseType = contentType.Split(';')[0].Trim();

        return baseType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
               baseType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    public static string CreateLargePayloadPlaceholder(string? contentType, long contentLength)
    {
        var type = contentType?.Split(';')[0].Trim() ?? "unknown type";
        var sizeStr = FormatBytes(contentLength);
        return $"[LARGE PAYLOAD: {type}, {sizeStr}]";
    }

    public static string CreateBinaryPayloadPlaceholder(string? contentType, long contentLength)
    {
        var type = contentType?.Split(';')[0].Trim() ?? "unknown type";
        var sizeStr = FormatBytes(contentLength);
        return $"[BINARY CONTENT: {type}, {sizeStr}]";
    }

    public static bool ExceedsMaxSize(long? contentLength)
    {
        return contentLength.HasValue && contentLength.Value > MaxPayloadSize;
    }

    /// <summary>
    /// Sanitizes a string by removing null characters (0x00) that cause errors in PostgreSQL.
    /// </summary>
    public static string? SanitizeString(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove all null characters (0x00) that cause UTF-8 encoding errors in PostgreSQL
        return input.Replace("\0", string.Empty);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
