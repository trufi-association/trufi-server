namespace Gateway.Middleware;

public static class PayloadHelper
{
    // Límite prudente: 1MB
    public const int MaxPayloadSize = 1_048_576; // 1 MB

    public static string CreateLargePayloadPlaceholder(string? contentType, long contentLength)
    {
        var type = contentType?.Split(';')[0].Trim() ?? "unknown type";
        var sizeStr = FormatBytes(contentLength);
        return $"[LARGE PAYLOAD: {type}, {sizeStr}]";
    }

    public static bool ExceedsMaxSize(long? contentLength)
    {
        return contentLength.HasValue && contentLength.Value > MaxPayloadSize;
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
