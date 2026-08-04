using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using ValueType = System.ValueType;

namespace SchemaNode.Function.Reflect;

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