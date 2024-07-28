using System.Text.Json;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help
{
    internal static class Serializing
    {
        public static async Task<string> SerializeAsync<T>(T obj, CancellationToken cancellationToken)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, obj, options, cancellationToken);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public static async Task<T?> DeserializeAsync<T>(string json, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            await writer.FlushAsync();
            stream.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
        }
    }
}
