namespace Gateway.Middleware;

/// <summary>
/// Forwards every write to the client's stream unchanged and keeps a copy of
/// the first bytes for the analytics log, up to a cap. Past the cap only the
/// count goes on, and a response the log will not store (decided by
/// <c>shouldCapture</c> on the first write, when the headers are known) is
/// never copied at all: a 7 MB wasm or a 5 MB main.dart.js costs the proxy no
/// buffering, and the client gets its bytes as they are produced instead of
/// after the whole response.
/// </summary>
public sealed class CapturingBodyStream : Stream
{
    private readonly Stream _inner;
    private readonly int _capacity;
    private readonly Func<bool> _shouldCapture;
    private MemoryStream? _captured;
    private bool _decided;

    public CapturingBodyStream(Stream inner, int capacity, Func<bool> shouldCapture)
    {
        _inner = inner;
        _capacity = capacity;
        _shouldCapture = shouldCapture;
    }

    /// <summary>Bytes the client was sent, captured or not.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>Whether the response is being kept for the log.</summary>
    public bool IsCapturing => _decided ? _captured != null : _shouldCapture();

    /// <summary>More bytes were written than the cap keeps.</summary>
    public bool Overflowed => BytesWritten > _capacity;

    /// <summary>The captured prefix (the whole body unless <see cref="Overflowed"/>).</summary>
    public ReadOnlyMemory<byte> Captured =>
        _captured == null ? ReadOnlyMemory<byte>.Empty : new(_captured.GetBuffer(), 0, (int)_captured.Length);

    /// <summary>A read-only stream over <see cref="Captured"/>, without copying it.</summary>
    public Stream OpenCaptured() =>
        _captured == null
            ? Stream.Null
            : new MemoryStream(_captured.GetBuffer(), 0, (int)_captured.Length, writable: false);

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        Keep(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        Keep(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        Keep(buffer.AsSpan(offset, count));
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken);
        Keep(buffer.Span);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    private void Keep(ReadOnlySpan<byte> bytes)
    {
        BytesWritten += bytes.Length;
        if (!_decided)
        {
            _decided = true;
            if (_shouldCapture())
                _captured = new MemoryStream();
        }
        var captured = _captured;
        if (captured == null)
            return;
        var room = _capacity - (int)captured.Length;
        if (room <= 0)
            return;
        captured.Write(bytes[..Math.Min(room, bytes.Length)]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captured?.Dispose();
            _captured = null;
        }
        base.Dispose(disposing);
    }
}
