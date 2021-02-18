using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    public class BinaryWriterRunner
    {
        private BinaryWriter _bw;

        [GlobalSetup]
        public void Setup()
        {
            _bw = new BinaryWriter(new NullWriteStream());
        }

        [Benchmark]
        public BinaryWriter DefaultCtor() => new BinaryWriter(Stream.Null);

        [Benchmark]
        public void WriteUInt32()
        {
            _bw.Write((uint)0xdeadbeef);
        }

        [Benchmark]
        public void WriteUInt64()
        {
            _bw.Write((ulong)0xdeadbeef_aabbccdd);
        }
    }

    [MemoryDiagnoser]
    public class BinaryWriterRunner_Extended
    {
        private string _input;
        private char[] _inputAsChars;
        private readonly BinaryWriter _bw;

        [Params(4, 16, 512, 8 * 1024, 16 * 1024, 128 * 1024, 1024 * 1024)]
        public int StringLengthInChars;

        public BinaryWriterRunner_Extended()
        {
            _bw = new BinaryWriter(new NullWriteStream());
        }

        [GlobalSetup]
        public void Setup()
        {
            _input = new string('x', StringLengthInChars);
            _inputAsChars = _input.ToCharArray();
        }

        [Benchmark]
        public void WriteCharArray()
        {
            _bw.Write(_inputAsChars);
        }

        [Benchmark]
        public void WriteString()
        {
            _bw.Write(_input);
        }
    }

    internal class NullWriteStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) { }

        public override void Write(ReadOnlySpan<byte> buffer) { }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void WriteByte(byte value) { }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
