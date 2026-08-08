using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Schema.ArrayType;
using ValueType = SchemaNode.Schema.ValueType;

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
    
    /// <summary>
    /// Gets the arguments of the function schema
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context, 
        FuncArg[] args, FuncExp[] exps,
        string? path = null, string? root = null)
    {
        
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && !path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        path ??= root;

        List<Entry<string>> first = [];
        Runtime.ValueType? valueType = null;
        Entry<string>? curr = null;
        foreach (FuncArg arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg.Name) || string.IsNullOrWhiteSpace(arg.Type)) continue;
            Runtime.ValueType? fieldType = await context.GetNodeTypeAsync<Runtime.ValueType>(arg.Type);
            if (fieldType == null) continue;
            var entry = new Entry<string> { Value = arg.Name, HasChildren = !fieldType.HasAccessEntries };
            entry.SetProperty<Display, LocaleString>(arg.GetProperty<Display>()?.Value ?? arg.Name);
            first.Add(entry);
            if (curr == null && !string.IsNullOrWhiteSpace(path) &&
                (path.Equals(arg.Name, StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith($"{arg.Name}.", StringComparison.OrdinalIgnoreCase)))
            {
                valueType = fieldType;
                curr = entry;
            }
        }

        // expressions
        foreach (FuncExp exp in exps)
        {
            if (string.IsNullOrWhiteSpace(exp.Name) || string.IsNullOrWhiteSpace(exp.Return)) continue;
            Runtime.ValueType? fieldType = await context.GetNodeTypeAsync<Runtime.ValueType>(exp.Return);
            if (fieldType == null) continue;
            var entry = new Entry<string> { Value = exp.Name, HasChildren = fieldType.HasAccessEntries };
            entry.SetProperty<Display, LocaleString>(exp.Name);
            first.Add(entry);
            
            if (curr == null && !string.IsNullOrWhiteSpace(path) && (path.Equals(exp.Name, StringComparison.OrdinalIgnoreCase) || 
                                                     path.StartsWith($"{exp.Name}.", StringComparison.OrdinalIgnoreCase)))
            {
                valueType = fieldType;
                curr = entry;
            }
        }
        
        // build the access entries
        List<EntryAccess<string>> result = [new (){ Children = first.ToArray() }];
        
        while (valueType != null)
        {
            var accessEntry = new EntryAccess<string>();
            Entry<string>[] accesses = valueType.GetAccessEntries().ToArray();
            if (curr != null)
            {
                accessEntry.Entry = new Entry<string> { Value = curr.Value, HasChildren = accesses.Length > 0 };
                accessEntry.Entry.SetProperty<Display, LocaleString>(curr.GetProperty<Display>()?.Value ?? curr.Value);
            }
            accessEntry.Children = accesses;
            
            // check next part
            Runtime.ValueType? next = null;
            foreach (var a in accesses)
            {
                string n = a.Value;
                if (curr != null) a.Value = $"{curr.Value}.{n}";
                if (!string.IsNullOrWhiteSpace(path) && (path.Equals(a.Value, StringComparison.OrdinalIgnoreCase) || 
                                                         path.StartsWith($"{a.Value}.", StringComparison.OrdinalIgnoreCase)))
                {
                    next = valueType.GetAccessValueType(n);
                    curr = a;
                }
            }
            result.Add(accessEntry);
            valueType = next;
        } 

        // cut
        if (!string.IsNullOrWhiteSpace(root))
            result = result.SkipWhile(r => (r.Entry?.Value.Length ?? 0) < root.Length).ToList();
        return result;
    }

    /// <summary>
    /// Gets the access value type
    /// </summary>
    public static async Task<string?> getaccessvaluetype(SchemaContext context, FuncArg[] args, FuncExp[] exps, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string[] paths = path.Split('.', 2,  StringSplitOptions.RemoveEmptyEntries);
        var type = args.FirstOrDefault(f => f.Name.Equals(paths[0], StringComparison.OrdinalIgnoreCase))?.Type
            ?? exps.FirstOrDefault(e => e.Name.Equals(paths[0], StringComparison.OrdinalIgnoreCase))?.Return;
        Runtime.ValueType? valueType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
        return paths.Length > 1 ? valueType?.GetAccessValueType(paths[1])?.Name : valueType?.Name;
    }

    
    /// <summary>
    /// Gets the expression types for the given exp return type
    /// </summary>
    public static async Task<List<ExpType>> getexptypes(SchemaContext context, [Meta<SchemaType>(typeof(ValueType))] string type)
    {
        var returnType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
        if (returnType == null) return [];
        if (returnType is Runtime.ArrayType)
        {
            return [ExpType.Call, ExpType.Filter, ExpType.Map];
        }
        else if (returnType is Runtime.BoolType)
        {
            return [ExpType.Call, ExpType.All, ExpType.Any];
        }
        else if (returnType is Runtime.IntType)
        {
            return [ExpType.Call, ExpType.Count, ExpType.Reduce];
        }
        else if (returnType is Runtime.DecimalType)
        {
            return [ExpType.Call, ExpType.Reduce];
        }
        return [ExpType.Call, ExpType.First, ExpType.Last, ExpType.Reduce];
    }

    /// <summary>
    /// Gets the expected function return type for the given exp return type
    /// </summary>
    public static async Task<string?> getexpectreturn(SchemaContext context, [Meta<SchemaType>(typeof(ValueType))] string type, ExpType expType)
    {
        var valueType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
        if (valueType == null) return null;
        return expType switch
        {
            ExpType.Call => valueType.Name,
            ExpType.Map => valueType is Runtime.ArrayType arr ? arr.Element?.Name : valueType.Name,
            ExpType.Reduce => valueType.Name,
            ExpType.First => NS_SYSTEM_BOOL,
            ExpType.Last => NS_SYSTEM_BOOL,
            ExpType.Filter => NS_SYSTEM_BOOL,
            ExpType.Count => NS_SYSTEM_BOOL,
            ExpType.All => NS_SYSTEM_BOOL,
            ExpType.Any => NS_SYSTEM_BOOL,
            _ => null,
        };
    }
}