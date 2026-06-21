using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Utility;

public class UniversalFlexibleEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return FlagsEnums.GetOrAdd(typeToConvert, t =>
        {
            var isFlags = t.IsDefined(typeof(FlagsAttribute), false);
            if (isFlags)
            {
                var converterType = typeof(FlexibleEnumConverter<>).MakeGenericType(t);
                return (JsonConverter)Activator.CreateInstance(converterType)!;
            }
            else
            {
                return NormalEnumConverter.CreateConverter(t, options);
            }
        });
    }

    private static readonly JsonStringEnumConverter NormalEnumConverter = new (JsonNamingPolicy.CamelCase);
    private static readonly ConcurrentDictionary<Type, JsonConverter> FlagsEnums = [];
}
