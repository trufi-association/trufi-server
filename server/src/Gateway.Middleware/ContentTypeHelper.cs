namespace Gateway.Middleware;

public static class PayloadHelper
{
    // Límite prudente: 1MB
    public const int MaxPayloadSize = 1_048_576; // 1 MB

    /// <summary>
    /// Detecta si el content-type es binario (imágenes, videos, PDFs, etc.)
    /// que no debería ser guardado como texto en PostgreSQL.
    /// </summary>
    public static bool IsBinaryContent(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        // Extraer el tipo base (sin charset, boundary, etc)
        var baseType = contentType.Split(';')[0].Trim();

        // Tipos binarios comunes que causan problemas con PostgreSQL UTF-8
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
    /// Detecta si el content-type es texto/JSON que puede ser guardado en PostgreSQL.
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
