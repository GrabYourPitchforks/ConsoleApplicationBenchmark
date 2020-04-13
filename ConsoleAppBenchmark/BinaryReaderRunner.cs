using BenchmarkDotNet.Attributes;
using System.IO;
using System.Text;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    public class BinaryReaderRunner
    {
        private MemoryStream _stream;

        [Params(0, 32, 128, 4096, 32 * 1024, 128 * 1024, 16 * 1024 * 1024)]
        public int StringLength;

        [GlobalSetup]
        public void Setup()
        {
            string s = new string('x', StringLength);

            _stream = new MemoryStream();
            var writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(new string('a', StringLength));
            writer.Write(new string('b', StringLength));
            writer.Write(new string('c', StringLength));
            writer.Write(new string('d', StringLength));
            writer.Write(new string('e', StringLength));
            writer.Dispose();
        }

        [Benchmark]
        public string ReadString()
        {
            MemoryStream stream = _stream;
            stream.Position = 0;

            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            reader.ReadString();
            reader.ReadString();
            reader.ReadString();
            reader.ReadString();
            return reader.ReadString();
        }
    }
}
