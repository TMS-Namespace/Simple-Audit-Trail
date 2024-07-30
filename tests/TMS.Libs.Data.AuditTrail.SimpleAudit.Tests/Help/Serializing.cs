using System.Text.Json;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help;

internal static class Serializing
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task<string> SerializeAsync<T>(T obj, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, obj, Options, cancellationToken);

        stream.Position = 0;
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static async Task<T?> DeserializeAsync<T>(string json, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);

        await writer.WriteAsync(json);
        await writer.FlushAsync(cancellationToken);
        stream.Position = 0;

        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
    }
}