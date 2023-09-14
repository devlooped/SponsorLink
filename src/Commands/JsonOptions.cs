using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Devlooped.Sponsors;

static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } =
#if NET6_0_OR_GREATER
        new(JsonSerializerDefaults.Web)
#else
        new()
#endif
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
#if NET6_0_OR_GREATER
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
#endif
            WriteIndented = true,
            Converters =
        {
            new JsonStringEnumConverter(allowIntegerValues: false),
#if NET6_0_OR_GREATER
            new DateOnlyJsonConverter()
#endif
        }
        };

#if NET6_0_OR_GREATER
    public class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => DateOnly.Parse(reader.GetString()?[..10] ?? "", CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }
#endif
}
