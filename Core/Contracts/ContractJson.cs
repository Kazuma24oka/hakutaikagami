using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HakutaiKagami.Core.Contracts;

public sealed class EntityIdConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? new(reader.GetString()!) : throw new JsonException("ID must be a string.");
    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        if (!EntityId.IsValid(value.Value)) throw new JsonException("Invalid ID.");
        writer.WriteStringValue(value.Value);
    }
}
public sealed class Int64StringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
    {
        if (r.TokenType != JsonTokenType.String || !long.TryParse(r.GetString(), NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var n) || n.ToString(CultureInfo.InvariantCulture) != r.GetString())
            throw new JsonException("Canonical Int64 string required.");
        return n;
    }
    public override void Write(Utf8JsonWriter w, long n, JsonSerializerOptions o) => w.WriteStringValue(n.ToString(CultureInfo.InvariantCulture));
}
public sealed class UInt64StringConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
    {
        if (r.TokenType != JsonTokenType.String || !ulong.TryParse(r.GetString(), NumberStyles.None,
            CultureInfo.InvariantCulture, out var n) || n.ToString(CultureInfo.InvariantCulture) != r.GetString())
            throw new JsonException("Canonical UInt64 string required.");
        return n;
    }
    public override void Write(Utf8JsonWriter w, ulong n, JsonSerializerOptions o) => w.WriteStringValue(n.ToString(CultureInfo.InvariantCulture));
}

public static class ContractJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(t =>
        {
            if (t.Kind == JsonTypeInfoKind.Object)
                foreach (var p in t.Properties.Where(p => p.Get != null)) p.IsRequired = !(t.Type == typeof(ContractWorld) && (p.Name == nameof(ContractWorld.Branches) || p.Name == nameof(ContractWorld.Couriers)));
        });
        return new() { TypeInfoResolver = resolver, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 64, Converters = { new EntityIdConverter(), new Int64StringConverter(),
                new UInt64StringConverter(), new JsonStringEnumConverter(allowIntegerValues: false) } };
    }
    public static byte[] Write<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static T Read<T>(ReadOnlySpan<byte> bytes)
    {
        try
        {
            // Reject invalid UTF-8 and duplicate properties before materializing any DTO.
            var text = StrictUtf8.GetString(bytes);
            using var doc = JsonDocument.Parse(text, new() { MaxDepth = 64 });
            CheckKeys(doc.RootElement);
            return JsonSerializer.Deserialize<T>(text, Options) ?? throw new JsonException("Null document.");
        }
        catch (Exception e) when (e is JsonException or ArgumentException or DecoderFallbackException or NotSupportedException)
        { throw new InvalidDataException("Invalid common-contract JSON.", e); }
    }
    private static void CheckKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new JsonException("Duplicate property.");
                CheckKeys(p.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var child in node.EnumerateArray()) CheckKeys(child);
    }
    public static T Copy<T>(T value) => Read<T>(Write(value));
    public static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    public static string CanonicalHash<T>(T value)
    {
        // Arrays that are declared sets are canonicalized by identity; ordered histories remain ordered.
        object? normalized=value switch{
            DefinitionPack d=>d with{
                Characters=d.Characters.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                Organizations=d.Organizations.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                Sites=d.Sites.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                AuthorityBases=d.AuthorityBases.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                Armies=d.Armies.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray()},
            PendingEvent e=>e with{DependsOn=e.DependsOn.OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray()},
            _=>value};
        using var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(normalized,normalized?.GetType()??typeof(T),Options));
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        CanonicalWrite(writer, doc.RootElement); writer.Flush(); return Hash(stream.ToArray());
    }
    private static void CanonicalWrite(Utf8JsonWriter w, JsonElement n)
    {
        if (n.ValueKind == JsonValueKind.Object)
        {
            w.WriteStartObject(); foreach (var p in n.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { w.WritePropertyName(p.Name); CanonicalWrite(w, p.Value); } w.WriteEndObject();
        }
        else if (n.ValueKind == JsonValueKind.Array)
        { w.WriteStartArray(); foreach (var x in n.EnumerateArray()) CanonicalWrite(w, x); w.WriteEndArray(); }
        else n.WriteTo(w);
    }
}
