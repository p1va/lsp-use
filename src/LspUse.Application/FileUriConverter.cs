using System.Text.Json;
using System.Text.Json.Serialization;

namespace LspUse.Application;

public class FileUriConverter : JsonConverter<Uri>
{
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value == null ? throw new JsonException("URI cannot be null") : new Uri(value);
    }

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        // If it's a file URI, write just the local path; otherwise write the full URI
        if (value.Scheme == "file")
        {
            writer.WriteStringValue(value.LocalPath);
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
