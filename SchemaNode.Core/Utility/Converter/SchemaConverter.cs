using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Schema;

namespace SchemaNode.Utility;

internal sealed class SchemaConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(ExtensibleSchema).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        Type converterType =
            typeof(SchemaConverter<>).MakeGenericType(typeToConvert);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class SchemaConverter<TSchema>
    : JsonConverter<TSchema>
    where TSchema : ExtensibleSchema
{
    private static readonly PropertyInfo[] Properties =
        typeof(TSchema)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static p =>
                p.CanRead &&
                p.Name != nameof(ExtensibleSchema.Extensions) &&
                p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .ToArray();

    public override TSchema Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        JsonObject root =
            JsonNode.Parse(ref reader)?.AsObject()
            ?? throw new JsonException();

        TSchema schema =
            Activator.CreateInstance<TSchema>();

        JsonObject extensions = [];

        foreach ((string key, JsonNode? value) in root)
        {
            PropertyInfo? property =
                Properties.FirstOrDefault(p =>
                    string.Equals(
                        GetJsonName(p, options),
                        key,
                        StringComparison.OrdinalIgnoreCase));

            if (property == null)
            {
                extensions[key] = value?.DeepClone();
                continue;
            }

            object? propertyValue =
                value?.Deserialize(
                    property.PropertyType,
                    options);

            property.SetValue(schema, propertyValue);
        }

        if (extensions.Count > 0)
        {
            schema.Extensions = extensions;
        }

        return schema;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TSchema value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (PropertyInfo property in Properties)
        {
            object? propertyValue =
                property.GetValue(value);

            if (propertyValue == null &&
                options.DefaultIgnoreCondition ==
                JsonIgnoreCondition.WhenWritingNull)
            {
                continue;
            }

            writer.WritePropertyName(
                GetJsonName(property, options));

            JsonSerializer.Serialize(
                writer,
                propertyValue,
                property.PropertyType,
                options);
        }

        if (value.Extensions != null)
        {
            foreach ((string key, JsonNode? node) in value.Extensions)
            {
                writer.WritePropertyName(key);

                if (node == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    node.WriteTo(writer, options);
                }
            }
        }

        writer.WriteEndObject();
    }

    private static string GetJsonName(
        PropertyInfo property,
        JsonSerializerOptions options)
    {
        JsonPropertyNameAttribute? attr =
            property.GetCustomAttribute<JsonPropertyNameAttribute>();

        if (attr != null)
        {
            return attr.Name;
        }

        return options.PropertyNamingPolicy?.ConvertName(property.Name)
               ?? property.Name;
    }
}