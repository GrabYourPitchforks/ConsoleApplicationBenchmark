using BenchmarkDotNet.Attributes;
using System.IO;
using System.Linq;
using System.Text;

namespace ConsoleAppBenchmark
{
    public class TwitterJsonRunner
    {
        private byte[] _twitterJsonBlob;
        private byte[] _asciiBlob;

        [GlobalSetup]
        public void Setup()
        {
            _twitterJsonBlob = File.ReadAllBytes(@"C:\Users\levib\Desktop\twitter.json");
            _asciiBlob = Enumerable.Repeat((byte)'x', _twitterJsonBlob.Length).ToArray();
        }

        [Benchmark]
        public int ValidateAscii()
        {
            return Encoding.UTF8.GetCharCount(_asciiBlob);
        }

        [Benchmark]
        public int ValidateTwitterJson()
        {
            return Encoding.UTF8.GetCharCount(_twitterJsonBlob);
        }
    }
}
