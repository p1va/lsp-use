namespace LspUse.LanguageServerClient.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Ensures that <see cref="Uri"/> instances are serialized using their
/// <see cref="Uri.AbsoluteUri"/> representation so that a value such as
/// "/tmp/foo.cs" is emitted as "file:///tmp/foo.cs" in JSON payloads, which is
/// what the Language Server Protocol expects.
/// </summary>
public sealed class AbsoluteUriJsonConverter : JsonConverter<Uri>
{
    public override Uri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        var uriString = reader.GetString() ?? string.Empty;
        return new Uri(uriString, UriKind.RelativeOrAbsolute);
    }

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var uriString = value.IsAbsoluteUri
            ? value.AbsoluteUri
            : new UriBuilder(Uri.UriSchemeFile, host: string.Empty, port: -1, pathValue: value.ToString()).Uri.AbsoluteUri;
        writer.WriteStringValue(uriString);
    }
}
