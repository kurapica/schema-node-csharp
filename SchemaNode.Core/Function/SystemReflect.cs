using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using PropertyType = SchemaNode.Runtime.PropertyType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;
using ValueType = SchemaNode.Schema.ValueType;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The reflection helpers for the system schema, used for validating the schema nodes and functions in the system schema
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT)]
public static class SystemReflect
{
    /// <summary>
    /// Gets the full names and labels of the schema nodes under the namespace with the given name
    /// </summary>
    public static async Task<Entry<string>[]> gettypes(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.NamespaceType))] string? name = null)
    {
        var ns = await context.GetNodeTypeAsync<Runtime.NamespaceType>(name ?? string.Empty);
        if (ns == null) return [];
        return ns.GetNodeSchemas().Select(s => new Entry<string> { Value = s.FullName, Label = s.GetProperty<Display>()?.Value, HasChildren = s.Kind == SCHEMA_KIND_NAMESPACE }).ToArray();
    }

    /// <summary>
    /// Gets the property value type
    /// </summary>
    public static async Task<string?> getproptype(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.PropertyType))] string name)
    {
        PropertyType? prop = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<PropertyType>(name) : null;
        return prop?.ValueType?.Name;
    }

    /// <summary>
    /// Gets the sub entries of the value type
    /// </summary>
    public static async Task<Entry<string>[]> getsubentries(SchemaContext context,
        [Meta<SchemaType>(typeof(ValueType))] string name,
        string? path = null)
    {
        var valueType = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<Runtime.ValueType>(name) : null;
        if (valueType == null) return [];
        
        if (!string.IsNullOrWhiteSpace(path))
            valueType = valueType.GetAccessValueType(path);
        return valueType?.GetSubEntries().ToArray() ?? [];
    }

    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is the same as the given kind
    /// </summary>
    public static async Task<bool> isschemakind(SchemaContext context, 
        [Meta<SchemaType>(typeof(AnyType))] string name, 
        [Meta<SchemaType>(typeof(SchemaKind))] string kind,
        bool matchArrayElement = false)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        if (nodeType == null) return false;
        if (nodeType.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)) return true;
        return matchArrayElement && nodeType is ArrayType arr && arr.Element?.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is a value schema kind
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static async Task<bool> isvaluekind(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType != null && 
            typeof(ValueSchemaKind).GetRecordedValues().Any(v => v.GetValue<string>()!.Equals(nodeType.Kind, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is a value schema kind and not array schema kind
    /// </summary>
    public static async Task<bool> isarrayele(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType != null && 
            !nodeType.Kind.Equals(SCHEMA_KIND_ARRAY, StringComparison.OrdinalIgnoreCase) &&
            typeof(ValueSchemaKind).GetRecordedValues().Any(v => v.GetValue<string>()!.Equals(nodeType.Kind, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// The reflection helpers for the schema functions
    /// </summary>
    [Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_FUNC)]
    public static class Function
    {
        /// <summary>
        /// Checks if the function type's return type match the given type
        /// </summary>
        public static async Task<bool> withreturn(SchemaContext context,
            [Meta<SchemaType>(typeof(FuncType))] string func, 
            [Meta<SchemaType>(typeof(ValueType))] string type,
            bool matchArrayElement = false)
        {
            var nodeType = !string.IsNullOrWhiteSpace(func) ? await context.GetNodeTypeAsync<FunctionType>(func) : null;
            var returnType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
            return nodeType?.Return != null && returnType != null && (nodeType.Return.IsAssignableTo(returnType) || matchArrayElement && returnType is Runtime.ArrayType { Element: not null } arr && nodeType.Return.IsAssignableTo(arr.Element));
        }

        /// <summary>
        /// Checks if the function type's argument match the given types
        /// </summary>
        public static async Task<bool> withargs(SchemaContext context,
            [Meta<SchemaType>(typeof(AnyType))] string func,
            [Meta<SchemaType>(typeof(ValueType))] params string[] args)
        {
            var funcType = !string.IsNullOrWhiteSpace(func) ? await context.GetNodeTypeAsync<FunctionType>(func) : null;
            if (funcType == null || args.Length != funcType.Args.Length) return false;
            for (int i = 0; i < args.Length; i++)
            {
                var argType = !string.IsNullOrWhiteSpace(args[i]) ? await context.GetNodeTypeAsync<Runtime.ValueType>(args[i]) : null;
                if (argType == null) return false;
                if (funcType.Args[i].ValueType == null || !funcType.Args[i].ValueType!.IsAssignableTo(argType)) return false;
            }
            return true;
        }
    }
}