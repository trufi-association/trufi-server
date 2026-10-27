using Xunit;

namespace Gateway.Middleware.Tests;

/// <summary>
/// The tee behind the analytics log: every byte reaches the client, the log
/// gets a copy only when the first write says so and only up to the cap.
/// </summary>
public class CapturingBodyStreamTests
{
    private static byte[] Bytes(int count, byte seed = 7) =>
        Enumerable.Range(0, count).Select(i => (byte)((i * seed + 13) & 0xff)).ToArray();

    [Fact]
    public async Task Forwards_every_byte_and_captures_a_body_within_the_cap()
    {
        var wire = new MemoryStream();
        using var tee = new CapturingBodyStream(wire, capacity: 100, shouldCapture: () => true);
        var body = Bytes(60);

        await tee.WriteAsync(body.AsMemory(0, 25));
        tee.Write(body, 25, 20);
        tee.Write(body.AsSpan(45, 15));

        Assert.Equal(body, wire.ToArray());
        Assert.Equal(60, tee.BytesWritten);
        Assert.False(tee.Overflowed);
        Assert.True(tee.IsCapturing);
        Assert.Equal(body, tee.Captured.ToArray());
        using var captured = tee.OpenCaptured();
        var copy = new MemoryStream();
        await captured.CopyToAsync(copy);
        Assert.Equal(body, copy.ToArray());
    }

    [Fact]
    public async Task Past_the_cap_only_the_count_goes_on_but_the_client_still_gets_everything()
    {
        var wire = new MemoryStream();
        using var tee = new CapturingBodyStream(wire, capacity: 100, shouldCapture: () => true);
        var body = Bytes(250);

        await tee.WriteAsync(body.AsMemory(0, 90));
        await tee.WriteAsync(body.AsMemory(90, 160));

        Assert.Equal(body, wire.ToArray());
        Assert.Equal(250, tee.BytesWritten);
        Assert.True(tee.Overflowed);
        Assert.Equal(100, tee.Captured.Length);
        Assert.Equal(body.Take(100), tee.Captured.ToArray());
    }

    [Fact]
    public async Task A_body_the_log_will_not_keep_is_forwarded_but_never_copied()
    {
        var wire = new MemoryStream();
        var asked = 0;
        using var tee = new CapturingBodyStream(wire, capacity: 1024, shouldCapture: () => { asked++; return false; });
        var body = Bytes(500);

        await tee.WriteAsync(body.AsMemory(0, 200));
        await tee.WriteAsync(body.AsMemory(200, 300));

        Assert.Equal(body, wire.ToArray());
        Assert.Equal(500, tee.BytesWritten);
        Assert.False(tee.IsCapturing);
        Assert.True(tee.Captured.IsEmpty);
        Assert.Same(Stream.Null, tee.OpenCaptured());
        Assert.Equal(1, asked); // decided once, on the first write, when the headers are known
    }

    [Fact]
    public void Nothing_written_means_nothing_captured_and_no_decision_taken()
    {
        var asked = 0;
        using var tee = new CapturingBodyStream(new MemoryStream(), capacity: 10, shouldCapture: () => { asked++; return true; });

        Assert.Equal(0, tee.BytesWritten);
        Assert.False(tee.Overflowed);
        Assert.True(tee.Captured.IsEmpty);
        Assert.Equal(0, asked);
        Assert.True(tee.IsCapturing); // before any write it consults the predicate
        Assert.Equal(1, asked);
        Assert.False(tee.CanRead);
        Assert.False(tee.CanSeek);
        Assert.True(tee.CanWrite);
    }
}
