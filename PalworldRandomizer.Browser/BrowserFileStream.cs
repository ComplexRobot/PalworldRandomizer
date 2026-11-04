using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using BrowserFile = SpawnDev.SpawnJS.JSObjects.File;

namespace PalworldRandomizer.Browser;

/// <summary>Measured source reads; a shared block cache is not a bound on the parser's whole heap.</summary>
internal sealed class BrowserReadStats {
    public long Bytes { get; set; }
    public int Reads { get; set; }
    public int MaxRequest { get; set; }
    public bool Closed { get; set; }
}

/// <summary>Bounded public SpawnJS File slices shared by independent CUE cursors.</summary>
internal sealed class BrowserFileSource : IDisposable {
    internal const int _blockSize = 256 * 1024;
    private readonly BrowserFile _file;
    private readonly SpawnJSObjectReference _reader;
    private readonly byte[] _cache = new byte[_blockSize];
    private long _start = -1;
    private int _valid;
    private int _references;
    internal long Length { get; }
    internal BrowserReadStats Stats { get; } = new();

    internal BrowserFileSource(BrowserFile file) {
        Length = file.Size;
        if (Length is < 0 or > 9007199254740991) {
            throw new IOException("Unsafe browser file length.");
        }

        _file = file.JSRefCopy<BrowserFile>();
        _reader = SpawnJSRuntime.Instance.New("FileReaderSync");
    }

    internal void Retain() {
        ObjectDisposedException.ThrowIf(Stats.Closed, this);
        _references++;
    }

    internal void Release() {
        if (--_references == 0) {
            Dispose();
        }
    }

    internal int ReadAt(long position, Span<byte> target) {
        ObjectDisposedException.ThrowIf(Stats.Closed, this);
        int done = 0;
        while (done < target.Length && position < Length) {
            if (position < _start || position >= _start + _valid) {
                _start = position / _blockSize * _blockSize;
                int requested = (int)Math.Min(_blockSize, Length - _start);
                using var slice = _file.Slice(_start, _start + requested);
                using var buffer = _reader.Call<Blob, ArrayBuffer>("readAsArrayBuffer", slice);
                using var bytes = new Uint8Array(buffer);
                using var heap = HeapView.Create(_cache.AsMemory(0, requested));
                using var view = heap.View;
                view.Set(bytes);
                _valid = checked((int)buffer.ByteLength);
                Stats.Bytes += _valid;
                Stats.Reads++;
                Stats.MaxRequest = Math.Max(Stats.MaxRequest, requested);
                if (_valid != requested) {
                    throw new EndOfStreamException("Local file changed or produced a short read.");
                }
            }

            int within = checked((int)(position - _start));
            int count = Math.Min(target.Length - done, _valid - within);
            _cache.AsSpan(within, count).CopyTo(target[done..]);
            done += count;
            position += count;
        }

        return done;
    }

    public void Dispose() {
        if (!Stats.Closed) {
            Stats.Closed = true;
            _reader.Dispose();
            _file.Dispose();
        }
    }
}

/// <summary>A read-only stream with independently moving clones and positional source reads.</summary>
internal sealed class BrowserFileStream : Stream, ICloneable {
    private readonly BrowserFileSource _source;
    private long _position;
    private bool _disposed;
    internal BrowserReadStats Stats => _source.Stats;
    internal BrowserFileStream(BrowserFile file) : this(new BrowserFileSource(file), 0) { }
    private BrowserFileStream(BrowserFileSource source, long position) {
        _source = source;
        _position = position;
        _source.Retain();
    }

    private void Check() => ObjectDisposedException.ThrowIf(_disposed, this);
    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length { get { Check(); return _source.Length; } }
    public override long Position {
        get { Check(); return _position; }
        set {
            Check();
            if (value is < 0 or > 9007199254740991) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer) {
        Check();
        int read = _source.ReadAt(_position, buffer);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) {
        Check();
        long start = origin switch {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => _position,
            SeekOrigin.End => Length,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        long next = checked(start + offset);
        if (next is < 0 or > 9007199254740991) {
            throw new IOException("Seek outside browser-safe positions.");
        }

        return Position = next;
    }

    public object Clone() { Check(); return new BrowserFileStream(_source, _position); }
    protected override void Dispose(bool disposing) {
        if (disposing && !_disposed) {
            _disposed = true;
            _source.Release();
        }

        base.Dispose(disposing);
    }

    public override void Flush() => Check();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}