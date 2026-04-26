using System.Reflection;
using System.Runtime.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Service;

/// <summary>
/// Generates EnumSchema from C# enum types annotated with [Meta&lt;SchemaType&gt;]
/// </summary>
internal sealed class EnumGenerator : INodeSchemaGenerator
{
    /// <inheritdoc />
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, string?> typeResolver)
    {
        if (!type.IsEnum) yield break;

        // Build node schema (NodeSchema.Create applies node-level meta-properties and uses XML doc as default display)
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_ENUM, @namespace, name, type);
        
        // Determine enum value type: Flags or String
        EnumValueType valueType = type.GetCustomAttribute<FlagsAttribute>() != null
            ? EnumValueType.Flags
            : EnumValueType.String;

        // Build enum values from public static fields
        EnumValueSchema[] values = 
            // from record property
            type.GetMetaProperty<Record>()?.Value?.GetRecordedValues()
           .Where(v => v.HasValue)
           .Select(v =>
           {
               string value = v.GetValue<string>()!.ToCamelCase();
               EnumValueSchema valueSchema = new () { Value = value };
               valueSchema.SetProperty<Display, LocaleString>($"{schema.FullName}.{value.ToLowerInvariant()}");
               return valueSchema;
            }).ToArray() 
       
       // From enum definition
       ?? type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f =>
        {
            EnumValueSchema valueSchema = new ()
            {
                Value = valueType switch
                {
                    EnumValueType.String => (f.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? f.Name).ToCamelCase(),
                    _ => $"{f.GetValue(null)}"
                },
            };
            valueSchema.SetProperty<Display, LocaleString>(type.GetSummaryFromXmlDoc(f) ?? $"{schema.FullName}.{f.Name.ToLowerInvariant()}");
            
            // properties
            foreach (IProperty prop in f.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_ENUM_VALUE))
                valueSchema.SetProperty(prop);
            return valueSchema;
        }).ToArray();

        // Set the EnumProperty with the value type and values
        schema.SetProperty<EnumProperty, EnumSchema>(new EnumSchema
        {
            Type = valueType,
            Values = values,
        });

        yield return schema;
    }
}