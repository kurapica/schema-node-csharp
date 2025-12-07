using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Components;

/// <summary>
/// The function node expression tree visitor
/// support (arg: struct, ...) => bool
/// Generate the struct access where clause
/// </summary>
public static class RowAccessExpTreeVisitor
{
    /// <summary>
    /// Visit the function type, the table must be the first parameter
    /// </summary>
    public static async Task<AccessExpNode> Visit(SchemaContext context, FunctionType func)
    {
        if (func.AccessExpNode != null) return func.AccessExpNode;
        
        // verify the function
        var _ = func.GetSchemaFuncInfo(context) ?? throw new Exception($"Function {func.Name} can't be complied");
        
        if (func.Args.Length < 1 || func.Args[0].TypeNode is not StructType structType)
            throw new NotSupportedException("The struct type must be the first parameter");

        if (func.ReturnNode is not ScalarType { IsBool: true })
            throw new NotSupportedException("The function return type must be bool");
        
        // scan the exp trees
        FunctionNodeExpression last = func.ExpTrees.LastOrDefault() as FunctionNodeExpression
            ?? throw new NotSupportedException("The function exp tree is invalid");
        
        // init the exp map
        Dictionary<FunctionNodeExpTree, AccessExpNode> expMap = [];
        expMap[func.Args[0]] = new StructAccessExpNode(structType);
        for (int i = 1; i < func.Args.Length; i++)
            expMap[func.Args[i]] = new ArgNode(func.Args[i].TypeNode, i - 1);
        
        // visit the exp tree
        AccessExpNode accessExp =  await VisitExp(context, last, expMap);
        func.AccessExpNode = accessExp;
        return accessExp;
    }
    
    /// <summary>
    /// Convert the exp tree to SQL
    /// </summary>
    public static string ToSql(this AccessExpNode accessExp, ISqlProvider sqlProvider, string prefix = "", params object[] args)
        => ToSql(sqlProvider, accessExp, prefix, args);

    /// <summary>
    /// Combine the access exp with the filter
    /// </summary>
    public static AccessExpNode Combine(this AccessExpNode accessExp, JsonObject? filter = null)
    {
        if (filter == null || filter.IsEmpty()) return accessExp;
        
        StructAccessExpNode structAccess = GetStructAccessExpNode(accessExp) ?? throw new NotSupportedException("The access expression tree is invalid");
        foreach((string key, JsonNode? value) in filter)
        {
            if (value == null || value.IsEmpty()) continue;
            StructFieldConfig? field = structAccess.StructType.Fields.FirstOrDefault(f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (field is not { TypeNode: ScalarType }) continue;

            BinaryAccessExpNode? fieldExp = accessExp.FindFieldAccessOper(key);

            // additional filter
            if (fieldExp is not { Type: BinaryAccessExpType.Equal })
            {
                accessExp = value switch
                {
                    JsonArray arr => new BinaryAccessExpNode(BinaryAccessExpType.AndAlso, accessExp,
                        new BinaryAccessExpNode(BinaryAccessExpType.Contains,
                            new ValueAccessExpNode(new ArrayTypeNode(field.TypeNode!, arr)),
                            new FieldAccessAccessExpNode(structAccess, key))),
                    JsonValue val => new BinaryAccessExpNode(BinaryAccessExpType.AndAlso, accessExp,
                        new BinaryAccessExpNode(BinaryAccessExpType.Equal, new FieldAccessAccessExpNode(structAccess, key),
                            new ValueAccessExpNode(field.TypeNode!.CreateNode(val)))),
                    _ => accessExp
                };
            }

            // write back to the filter
            else
            {
                filter[key] = fieldExp.Right is ValueAccessExpNode valNode
                    ? JsonValue.Create(valNode.Value)
                    : null;
            }
        }

        return accessExp;
    }
    
    /// <summary>
    /// Visit the exp tree
    /// </summary>
    static async Task<AccessExpNode> VisitExp(SchemaContext context, FunctionNodeExpTree? expTree, Dictionary<FunctionNodeExpTree, AccessExpNode> expMap)
    {
        // cache
        if (expTree != null && expMap.TryGetValue(expTree, out AccessExpNode? result)) return result;
        
        // const value
        if (expTree is ConstantExpNode constNode)
        {
            result = new ValueAccessExpNode(constNode.Value as AnySchemaNode ?? constNode.TypeNode?.CreateNode(constNode.Value));
            expMap.Add(constNode, result);
            return result;
        }
        
        // exp only
        if (expTree is not FunctionNodeExpression exp)
            throw new NotSupportedException("The function exp tree is invalid");

        // visit leaf nodes
        AccessExpNode[] leafNodes = new AccessExpNode[exp.LeafNodes.Length];
        for (int i = 0; i < exp.LeafNodes.Length; i++)
            leafNodes[i] = await VisitExp(context, exp.LeafNodes[i], expMap);

        // all loaded, calc directly
        if (leafNodes.All(l => l is ValueAccessExpNode))
        {
            result = new ValueAccessExpNode(await context.CallFunctionAsync(exp.FuncNode!, 
                    leafNodes.Select(a => (a as ValueAccessExpNode)?.Value).ToArray()));
            expMap.Add(exp, result);
            return result;
        }
        
        // check by function
        switch (exp.Func)
        {
            // a & b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.andalso)}":
            {
                result = new BinaryAccessExpNode(BinaryAccessExpType.AndAlso, leafNodes[0], leafNodes[1]);
                break;
            }

            // v in [min, max] | (min, max) | [min, max) | (min, max]
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}":
            {
                AccessExpNode valueNode = leafNodes[0];
                AccessExpNode minNode = leafNodes[1];
                AccessExpNode maxNode = leafNodes[2];
                AccessExpNode? includeMinNode = leafNodes.ElementAtOrDefault(3);
                AccessExpNode? includeMaxNode = leafNodes.ElementAtOrDefault(4);

                if (minNode is not ValueAccessExpNode or ArgNode
                    || maxNode is not ValueAccessExpNode or ArgNode
                    || includeMinNode != null && includeMinNode is not ValueAccessExpNode
                    || includeMaxNode != null && includeMaxNode is not ValueAccessExpNode)
                    throw new NotSupportedException("Can't figure out the between value");

                result = new BinaryAccessExpNode (
                    BinaryAccessExpType.AndAlso,
                    new BinaryAccessExpNode(
                        includeMinNode is ValueAccessExpNode incMin && (incMin.Value?.ToValue<bool>() ?? false)
                            ? BinaryAccessExpType.GreaterEqual
                            : BinaryAccessExpType.GreaterThan,
                        valueNode,
                        minNode
                    ),
                    new BinaryAccessExpNode(
                        includeMaxNode is ValueAccessExpNode incMax && (incMax.Value?.ToValue<bool>() ?? false)
                            ? BinaryAccessExpType.LessEqual
                            : BinaryAccessExpType.LessThan,
                        valueNode,
                        maxNode
                    )   
                );
                break;
            }
            
            // a == b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.equal)}":
            {
                AccessExpNode left = leafNodes[0];
                AccessExpNode right = leafNodes[1];
                if (right is FieldAccessAccessExpNode) (left, right) = (right, left);
                result = new BinaryAccessExpNode(BinaryAccessExpType.Equal, left, right);
                break;
            }
            
            // a >= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greateequal)}":
            {
                AccessExpNode left = leafNodes[0];
                AccessExpNode right = leafNodes[1];
                result = left is FieldAccessAccessExpNode 
                    ? new BinaryAccessExpNode(BinaryAccessExpType.GreaterEqual, left, right) 
                    : new BinaryAccessExpNode(BinaryAccessExpType.LessEqual, right, left);
                break;
            }
            
            // a > b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greatethan)}":
            {
                AccessExpNode left = leafNodes[0];
                AccessExpNode right = leafNodes[1];
                result = left is FieldAccessAccessExpNode 
                    ? new BinaryAccessExpNode(BinaryAccessExpType.GreaterThan, left, right) 
                    : new BinaryAccessExpNode(BinaryAccessExpType.LessThan, right, left);
                break;
            }
            
            // a <= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessequal)}":
            {
                AccessExpNode left = leafNodes[0];
                AccessExpNode right = leafNodes[1];
                result = left is FieldAccessAccessExpNode 
                    ? new BinaryAccessExpNode(BinaryAccessExpType.LessEqual, left, right) 
                    : new BinaryAccessExpNode(BinaryAccessExpType.GreaterEqual, right, left);
                break;
            }
            
            // a < b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessthan)}":
            {
                AccessExpNode left = leafNodes[0];
                AccessExpNode right = leafNodes[1];
                result = left is FieldAccessAccessExpNode 
                    ? new BinaryAccessExpNode(BinaryAccessExpType.LessThan, left, right) 
                    : new BinaryAccessExpNode(BinaryAccessExpType.GreaterThan, right, left);
                break;
            }
            
            // a != b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notequal)}":
            {
                AccessExpNode left = leafNodes[0];
                AccessExpNode right = leafNodes[1];
                if (right is FieldAccessAccessExpNode) (left, right) = (right, left);
                result = new BinaryAccessExpNode(BinaryAccessExpType.NotEqual, left, right);
                break;
            }
            
            // a | b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.orelse)}":
            {
                result = new BinaryAccessExpNode(BinaryAccessExpType.OrElse, leafNodes[0], leafNodes[1]);
                break;
            }
            
            // !a
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}":
            {
                // try to convert the exp to [not] part
                var notExp = leafNodes[0];
                if (notExp is not BinaryAccessExpNode binaryNotExp) throw new NotSupportedException("The system.logic.not expression not supported");

                result = binaryNotExp.Type switch
                {
                    BinaryAccessExpType.Equal => new BinaryAccessExpNode(BinaryAccessExpType.NotEqual, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.NotEqual => new BinaryAccessExpNode(BinaryAccessExpType.Equal, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.GreaterThan => new BinaryAccessExpNode(BinaryAccessExpType.LessEqual, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.GreaterEqual => new BinaryAccessExpNode(BinaryAccessExpType.LessThan, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.LessThan => new BinaryAccessExpNode(BinaryAccessExpType.GreaterEqual, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.LessEqual => new BinaryAccessExpNode(BinaryAccessExpType.GreaterThan, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.Contains => new BinaryAccessExpNode(BinaryAccessExpType.NotContains, binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.NotContains => new BinaryAccessExpNode(BinaryAccessExpType.Contains, binaryNotExp.Left, binaryNotExp.Right),
                    _ => throw new NotSupportedException("The system.logic.not expression not supported")
                };
                break;
            }
            
            // a.includes(b)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.contains)}":
            {
                AccessExpNode leftNode = leafNodes[0];
                AccessExpNode rightNode = leafNodes[1];
                if (leftNode is not ValueAccessExpNode or ArgNode)
                    throw new NotSupportedException($"The list of ${exp.Name} can't be resolved");
                result = new BinaryAccessExpNode(BinaryAccessExpType.Contains, leftNode, rightNode);
                break;
            }
            
            // a.b
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
            {
                if (leafNodes[0] is StructAccessExpNode structNode 
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName))
                {
                    result = new FieldAccessAccessExpNode(structNode, fieldName);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }
            
            // a[b] = c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldequal)}":
            {
                AccessExpNode valNode = leafNodes[2];
                if (leafNodes[0] is StructAccessExpNode structNode 
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && valNode is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.Equal, new FieldAccessAccessExpNode(structNode, fieldName), valNode);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }
            
            // a.startsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.startswith)}":
            {
                if (leafNodes[0] is FieldAccessAccessExpNode && leafNodes[1] is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.StartsWith, leafNodes[0], leafNodes[1]);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }

            // a.endsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.endswith)}":
            {
                if (leafNodes[0] is FieldAccessAccessExpNode && leafNodes[1] is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.EndsWith, leafNodes[0], leafNodes[1]);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }

            // a.contains(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.contains)}":
            {
                if (leafNodes[0] is FieldAccessAccessExpNode && leafNodes[1] is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.ContainsStr, leafNodes[0], leafNodes[1]);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }
            
            default:
                throw new NotSupportedException($"The expression not supported: {exp.Name} ({exp.Func})");
        }
        
        // cache the result
        expMap.Add(exp, result ?? throw new NotSupportedException("The function exp tree is invalid"));
        return result;
    }
    
    static StructAccessExpNode? GetStructAccessExpNode(this AccessExpNode accessExp)
    {
        return accessExp switch
        {
            StructAccessExpNode structNode => structNode,
            FieldAccessAccessExpNode accessNode => accessNode.Struct,
            BinaryAccessExpNode binaryNode => GetStructAccessExpNode(binaryNode.Left) ?? GetStructAccessExpNode(binaryNode.Right),
            _ => null
        };
    }
    
    // To sql
    static string ToSql(ISqlProvider sqlProvider, AccessExpNode accessExp, string prefix, object[] args)
    {
        switch(accessExp)
        {
            case FieldAccessAccessExpNode access:
                return $"{prefix}{sqlProvider.QuoteField(access.FieldName)}";
            case BinaryAccessExpNode binary:
                switch (binary.Type)
                {
                    case BinaryAccessExpType.AndAlso:
                    case BinaryAccessExpType.OrElse:
                    case BinaryAccessExpType.Equal:
                    case BinaryAccessExpType.NotEqual:
                    case BinaryAccessExpType.GreaterThan:
                    case BinaryAccessExpType.GreaterEqual:
                    case BinaryAccessExpType.LessThan:
                    case BinaryAccessExpType.LessEqual:
                        return sqlProvider.Binary(binary.Type, 
                            ToSql(sqlProvider, binary.Left, prefix, args), 
                            ToSql(sqlProvider, binary.Right, prefix, args));
                    case BinaryAccessExpType.Contains:
                        return sqlProvider.In(
                            ToSql(sqlProvider, binary.Right,prefix, args),
                            ((binary.Left as ValueAccessExpNode)!.Value as ArrayTypeNode)!);
                    case BinaryAccessExpType.NotContains:
                        return sqlProvider.NotIn(
                            ToSql(sqlProvider, binary.Right, prefix, args),
                            ((binary.Left as ValueAccessExpNode)!.Value as ArrayTypeNode)!);
                    case BinaryAccessExpType.StartsWith:
                        return sqlProvider.LikeStartsWith(
                            ToSql(sqlProvider, binary.Left, prefix, args),
                            (binary.Right as ValueAccessExpNode)?.Value?.ToValue<string>() 
                                ?? throw new NotSupportedException("The startsWith right value must be string"));
                    case BinaryAccessExpType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(sqlProvider, binary.Left, prefix, args),
                            (binary.Right as ValueAccessExpNode)?.Value?.ToValue<string>() 
                                ?? throw new NotSupportedException("The endsWith right value must be string"));
                    case BinaryAccessExpType.ContainsStr:
                        return sqlProvider.LikeContains(
                            ToSql(sqlProvider, binary.Left, prefix, args),
                            (binary.Right as ValueAccessExpNode)?.Value?.ToValue<string>() 
                                ?? throw new NotSupportedException("The contains right value must be string"));
                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            case ValueAccessExpNode value:
                return sqlProvider.Literal(value.Value);
            case ArgNode arg:
                return sqlProvider.Literal(args.ElementAtOrDefault(arg.Index) ?? throw new NotSupportedException($"The argument {arg.Index + 1} not provided"));
        }

        throw new NotSupportedException("The expression type not supported");
    }
    
    // Gets the expression node by field name
    static BinaryAccessExpNode? FindFieldAccessOper(this AccessExpNode accessExp, string fieldName)
    {
        if (accessExp is BinaryAccessExpNode binaryExpNode)
        {
            if (binaryExpNode.Left is FieldAccessAccessExpNode leftField && leftField.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase) 
                || binaryExpNode.Right is FieldAccessAccessExpNode rightField && rightField.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                return binaryExpNode;
            
            return FindFieldAccessOper(binaryExpNode.Left, fieldName) ?? FindFieldAccessOper(binaryExpNode.Right, fieldName);
        }
        return null;
    }
}

#region Exp access type

/// <summary>
/// The exp access type
/// </summary>
public enum BinaryAccessExpType
{
    AndAlso,
    OrElse,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterEqual,
    LessThan,
    LessEqual,

    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    ContainsStr,
}

/// <summary>
/// The exp node
/// </summary>
public abstract record AccessExpNode;

/// <summary>
/// The struct argument node
/// </summary>
public record StructAccessExpNode(StructType StructType) : AccessExpNode;

/// <summary>
/// Teh struct field access node
/// </summary>
public record FieldAccessAccessExpNode(StructAccessExpNode Struct, string FieldName) : AccessExpNode;

/// <summary>
/// The binary expression node
/// </summary>
public record BinaryAccessExpNode(BinaryAccessExpType Type, AccessExpNode Left, AccessExpNode Right) : AccessExpNode;

/// <summary>
/// The value expression node
/// </summary>
public record ValueAccessExpNode(AnySchemaNode? Value) : AccessExpNode;

/// <summary>
/// The argument node
/// </summary>
public record ArgNode(AnySchemeType? Type, int Index) : AccessExpNode;

#endregion