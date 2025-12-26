using Shared.Models;

namespace Analytics.Tests.TestFixtures;

public static class TestDataBuilder
{
    public static Request CreateRequest(
        string method = "GET",
        string uri = "/api/test",
        string host = "example.com",
        string? ip = "192.168.1.1",
        string? deviceId = "device-123",
        string? userAgent = "TestAgent/1.0",
        string? body = null,
        DateTime? receivedAt = null)
    {
        return new Request
        {
            Method = method,
            Uri = uri,
            Host = host,
            Ip = ip,
            DeviceId = deviceId,
            UserAgent = userAgent,
            Body = body,
            ReceivedAt = receivedAt ?? DateTime.UtcNow
        };
    }

    public static List<Request> CreateRequests(int count, string host = "example.com")
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateRequest(
                uri: $"/api/endpoint{i}",
                host: host,
                deviceId: $"device-{i}",
                receivedAt: DateTime.UtcNow.AddHours(-i)))
            .ToList();
    }

    public static List<Request> CreateRequestsForMultipleHosts(int countPerHost, params string[] hosts)
    {
        var requests = new List<Request>();
        foreach (var host in hosts)
        {
            requests.AddRange(CreateRequests(countPerHost, host));
        }
        return requests;
    }
}
