using BenchmarkDotNet.Attributes;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    public class JsonRunner
    {
        private MemoryStream _ms;
        private const string lipsum = "Le Lorem Ipsum est simplement du faux texte employé dans la composition et la mise en page avant impression. Le Lorem Ipsum est le faux texte standard de l'imprimerie depuis les années 1500, quand un imprimeur anonyme assembla ensemble des morceaux de texte pour réaliser un livre spécimen de polices de texte. Il n'a pas fait que survivre cinq siècles, mais s'est aussi adapté à la bureautique informatique, sans que son contenu n'en soit modifié. Il a été popularisé dans les années 1960 grâce à la vente de feuilles Letraset contenant des passages du Lorem Ipsum, et, plus récemment, par son inclusion dans des applications de mise en page de texte, comme Aldus PageMaker.";
        private byte[] _bytes;
        private Utf8JsonWriter _jsonWriter;
        private JsonEncodedText _propertyName;

        [GlobalSetup]
        public void Setup()
        {
            _bytes = Encoding.UTF8.GetBytes(lipsum);
            _ms = new MemoryStream();

            // use fastest possible encoder
            _jsonWriter = new Utf8JsonWriter(_ms, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            _propertyName = JsonEncodedText.Encode("PropName", JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

            _jsonWriter.WriteStartObject();
        }

        [Benchmark]
        public object WriteJsonFromUTF8_Bytes()
        {
            _jsonWriter.WriteString(_propertyName, _bytes);
            _jsonWriter.Flush();
            _ms.Position = 0;
            return _ms;
        }

        [Benchmark]
        public object WriteJsonFromUTF8_Utf8String()
        {
            byte[] cloned = _bytes.AsSpan().ToArray();
            _ = Encoding.UTF8.GetCharCount(cloned);
            _jsonWriter.WriteString(_propertyName, cloned);
            _jsonWriter.Flush();
            _ms.Position = 0;
            return _ms;
        }

        [Benchmark]
        public object WriteJsonFromUTF16()
        {
            _jsonWriter.WriteString(_propertyName, Encoding.UTF8.GetString(_bytes));
            _jsonWriter.Flush();
            _ms.Position = 0;
            return _ms;
        }
    }
}
