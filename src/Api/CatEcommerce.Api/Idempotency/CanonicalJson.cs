using System.Text.Json;

namespace CatEcommerce.Api.Idempotency;

/// <summary>
/// HO-ORD-001 §8.13 step 2 -- the canonical form of a JSON request body: property names sorted ordinally at every level, no insignificant
/// whitespace, so two bodies that differ only in key order or spacing hash equal (a client retry that re-serialises must replay, not be
/// <c>422</c>). Array order is significant and kept. A body that is not valid JSON is hashed as the raw bytes.
/// </summary>
public static class CanonicalJson
{
    public static byte[] Canonicalise(byte[] body)
    {
        if (body.Length == 0) return body;

        try
        {
            using var document = JsonDocument.Parse(body);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
                Write(writer, document.RootElement);

            return stream.ToArray();
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
