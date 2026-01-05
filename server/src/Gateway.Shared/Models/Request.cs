namespace Gateway.Shared.Models;

public class Request
{
    public long Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string? Ip { get; set; }
    public string? DeviceId { get; set; }
    public string? UserAgent { get; set; }
    public string? Body { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
