using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Collection Aps
/// </summary>
[Schema(NS_SYSTEM_COLLECTION)]
public static class SystemCollection
{
    /// <summary>
    /// Gets the array length
    /// </summary>
    [Schema]
    public static long length([Schema(NS_SYSTEM_ARRAY)] object array)
    {
        if (array is string str) return str.Length;
        if (array is JsonArray jsonArray) return jsonArray.Count;
        if (array is Array arr) return arr.LongLength;
        if (array is ArrayTypeNode node) return node.Count;
        if (array is ICollection collection) return collection.Count;
        return 0;
    }
    
    /// <summary>
    /// Whether the list contains the item
    /// </summary>
    [Schema]
    [Logic(LogicType.Contains, true)]
    public static bool contains<T>(ArrayTypeNode array, T value) where T: IComparable
    {
        foreach (var item in array)
        {
            if (EqualityComparer<T>.Default.Equals(item.ToValue<T>(), value)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the list not contains the item
    /// </summary>
    [Schema]
    [Logic(LogicType.NotContains, true)]
    public static bool notcontains<T>(ArrayTypeNode array, T value) where T: IComparable
    {
        return !contains(array, value);
    }

    /// <summary>
    /// Gets the field value from the object
    /// </summary>
    [Schema]
    public static async Task<T?> getfield<T>(SchemaContext context, StructTypeNode obj, string field, T? @default)
    {
        AnySchemaNode? result = await GetFieldNode(context, obj, field.Split('.', StringSplitOptions.RemoveEmptyEntries));
        return result is { IsEmpty: false } ? (T?)result.ToTypeValue(typeof(T)) : (@default ?? default);
    }

    /// <summary>
    /// Gets fields from the objects in the array to a new array
    /// </summary>
    [Schema]
    public static async Task<ArrayTypeNode> getfields(SchemaContext context, ArrayTypeNode array, string field)
    {
        if (array.ElementType is not StructType @struct) throw new InvalidOperationException("The array type is invalid");
        
        var f = @struct.Fields.FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"The field {field} not found in the struct {@struct.Name}");
        if (f.SchemeType == null) throw new InvalidOperationException($"The field {field} type is null in the struct {@struct.Name}");
        AnySchemaType arrayNode = SchemaContext.GetArraySchemaType(f.SchemeType)
                                  ?? throw new InvalidOperationException($"The field {field} type {f.Type} has no array type");

        ArrayTypeNode resultType = new (arrayNode);
        string[] paths = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (AnySchemaNode item in array)
        {
            AnySchemaNode? fieldNode = await GetFieldNode(context, item, paths);
            if (fieldNode != null) resultType.Add(fieldNode);
        }
        return resultType;
    }
    
    /// <summary>
    /// order by the given field
    /// </summary>
    [Schema]
    public static ArrayTypeNode orderby(ArrayTypeNode obj, string field, bool descending)
    {
        var list = new List<AnySchemaNode>(obj);
        list.Sort((a, b) =>
        {
            if (a is not StructTypeNode sa || b is not StructTypeNode sb) return 0;
            var fa = sa.GetField(field);
            var fb = sb.GetField(field);
            if ((fa == null || fa.IsEmpty) && (fb == null || fb.IsEmpty)) return 0;
            if (fa == null || fa.IsEmpty) return descending ? 1 : -1;
            if (fb == null || fb.IsEmpty) return descending ? -1 : 1;
            if (fa.Value is IComparable ca && fb.Value is IComparable cb)
                return descending ? cb.CompareTo(ca) : ca.CompareTo(cb);
            return 0;
        });
        return new ArrayTypeNode(obj.SchemaType, list);
    }
    
    /// <summary>
    /// Skip the given count items
    /// </summary>
    [Schema]
    public static ArrayTypeNode skip(ArrayTypeNode obj, int count)
    {
        if (count <= 0) return obj;
        return count > obj.Count 
            ? new ArrayTypeNode(obj.SchemaType) 
            : new ArrayTypeNode(obj.SchemaType, obj.Skip(count));
    }

    /// <summary>
    /// Take the given count items
    /// </summary>
    [Schema]
    public static ArrayTypeNode take(ArrayTypeNode obj, int count)
    {
        if (count >= obj.Count) return obj;
        return count <= 0 
            ? new ArrayTypeNode(obj.SchemaType)
            : new ArrayTypeNode(obj.SchemaType, obj.Take(count));
    }
    
    /// <summary>
    /// Gets the field node from object
    /// </summary>
    static async Task<AnySchemaNode?> GetFieldNode(SchemaContext context, AnySchemaNode? obj, string[] paths)
    {
        if (paths.Length == 0) return obj;
        if (obj is not StructTypeNode s) return null;
        
        StructType structType = (s.SchemaType as StructType)! ;
        StructFieldSchema? fldConfig = structType.GetField(paths[0]);
        AnySchemaNode? field = s.GetField(paths[0]);
        if (field == null) return null;
        if (fldConfig?.DisplayOnly != true)
            return paths.Length == 1 ? field : await GetFieldNode(context, field, paths.Skip(1).ToArray());
        
        // Calc the display only field
        StructRelationSchema? relation = structType.Relations?.FirstOrDefault(r =>
            r.Field.Equals(paths[0], StringComparison.OrdinalIgnoreCase) &&
            r.Property.Equals(PROPERTY_DEFAULT, StringComparison.OrdinalIgnoreCase));
        if (relation == null) return null;
        
        JsonArray args = [];
        foreach (var arg in relation.Args)
            args.Add(!string.IsNullOrWhiteSpace(arg.Name) ? s.GetValueByPaths(arg.Name)?.ToJson() : arg.Value?.DeepClone());
        field.Value = await context.CallFunctionAsync(relation.Func, args, fldConfig.Type);
        return paths.Length == 1 ? field : await GetFieldNode(context, field, paths.Skip(1).ToArray());
    }
}