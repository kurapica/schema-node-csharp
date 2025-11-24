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
/// only support (arg: struct) => bool
/// Generate the struct access where clause
/// </summary>
public static class RowAccessExpTreeVisitor
{
    /// <summary>
    /// Visit the function type
    /// </summary>
    public static async Task<ExpNode> Visit(SchemaContext context, FunctionType func, JsonObject? filter = null)
    {
        SchemaFuncInfo funcInfo = func.GetSchemaFuncInfo() ?? throw new Exception($"Function {func.Name} can't be complied");
        
        if (func.Args.Length != 1)
            throw new NotSupportedException("Only support one parameter function");

        if (func.Args[0].TypeNode is not StructType structType)
            throw new NotSupportedException("Only support struct type parameter");

        if (func.ReturnNode is not ScalarType { IsBool: true })
            throw new NotSupportedException("The function return type must be bool");

        // scan the exp trees
        FunctionNodeExpression last = func.ExpTrees.LastOrDefault() as FunctionNodeExpression
            ?? throw new NotSupportedException("The function exp tree is invalid");
        
        // visit the exp tree
        ExpNode exp =  await VisitExp(context, last, 
            new Dictionary<FunctionNodeExpTree, ExpNode>
                { { func.Args[0], new StructExpNode((func.Args[0].TypeNode as StructType)!) } } );

        // combine the filter
        if ((filter != null && !filter.IsEmpty()))
        {
            foreach((string key, JsonNode? value) in filter)
            {
                if (value == null || value.IsEmpty()) continue;
                StructFieldConfig? field = structType.Fields.FirstOrDefault(f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (field == null || field.TypeNode is not ScalarType) continue;

                BinaryExpNode? fieldExp = exp.FindFieldAccessOper(key);

                // additional filter
                if (fieldExp == null || fieldExp.Type != BinaryExpType.Equal)
                {
                    if (value is JsonArray arr)
                    {
                        exp = new BinaryExpNode(
                            BinaryExpType.AndAlso,
                            exp,
                            new BinaryExpNode(
                                BinaryExpType.Contains,
                                new ValueExpNode(new ArrayTypeNode(field.TypeNode!, arr)),
                                new FieldAccessExpNode(key)
                            )
                        );
                    }
                    else if (value is JsonValue val)
                    {
                        exp = new BinaryExpNode(
                            BinaryExpType.AndAlso,
                            exp,
                            new BinaryExpNode(
                                BinaryExpType.Equal,
                                new FieldAccessExpNode(key),
                                new ValueExpNode(field.TypeNode!.CreateNode(val))
                            )
                        );
                    }
                }

                // write back to the filter
                else
                {
                    filter[key] = fieldExp.Right is ValueExpNode valNode
                        ? JsonValue.Create(valNode.Value)
                        : null;
                }
            }
        }
        return exp;
    }
        
    /// <summary>
    /// Visit the exp tree
    /// </summary>
    static async Task<ExpNode> VisitExp(SchemaContext context, FunctionNodeExpTree? expTree, Dictionary<FunctionNodeExpTree, ExpNode> expMap)
    {
        // cache
        if (expTree != null && expMap.TryGetValue(expTree, out ExpNode? result)) return result;
        
        // const value
        if (expTree is ConstantExpNode constNode)
        {
            result = new ValueExpNode(constNode.Value is AnySchemaNode node ? node : constNode.TypeNode?.CreateNode(constNode.Value));
            expMap.Add(constNode, result);
            return result;
        }
        
        // exp only
        if (expTree is not FunctionNodeExpression exp)
            throw new NotSupportedException("The function exp tree is invalid");

        // visit leaf nodes
        ExpNode[] leafNodes = new ExpNode[exp.LeafNodes.Length];
        for (int i = 0; i < exp.LeafNodes.Length; i++)
            leafNodes[i] = await VisitExp(context, exp.LeafNodes[i], expMap);

        // all loaded, calc directly
        if (leafNodes.All(l => l is ValueExpNode))
        {
            result = await VisitExp(context, exp, leafNodes);
            expMap.Add(exp, result);
            return result;
        }
        
        // check by function
        switch (exp.Func)
        {
            // a & b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.andalso)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.AndAlso,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }

            // v in [min, max] | (min, max) | [min, max) | (min, max]
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}":
            {
                ExpNode valueNode = leafNodes[0];
                ExpNode minNode = leafNodes[1];
                ExpNode maxNode = leafNodes[2];
                ExpNode? includeMinNode = exp.LeafNodes.Length > 3 ? leafNodes[3] : null;
                ExpNode? includeMaxNode = exp.LeafNodes.Length > 4 ? leafNodes[4] : null;

                if (minNode is not ValueExpNode
                    || maxNode is not ValueExpNode
                    || includeMinNode != null && includeMinNode is not ValueExpNode
                    || includeMaxNode != null && includeMaxNode is not ValueExpNode)
                    throw new NotSupportedException("Can't figure out the between value");

                result = new BinaryExpNode (
                    BinaryExpType.AndAlso,
                    new BinaryExpNode(
                        includeMinNode is ValueExpNode incMin && (incMin.Value?.ToValue<bool>() ?? false)
                            ? BinaryExpType.GreaterEqual
                            : BinaryExpType.GreaterThan,
                        valueNode,
                        minNode
                    ),
                    new BinaryExpNode(
                        includeMaxNode is ValueExpNode incMax && (incMax.Value?.ToValue<bool>() ?? false)
                            ? BinaryExpType.LessEqual
                            : BinaryExpType.LessThan,
                        valueNode,
                        maxNode
                    )   
                );
                break;
            }
            
            // a == b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.equal)}":
            {
                var left = await VisitExp(context, exp.LeafNodes[0], expMap);
                var right = await VisitExp(context, exp.LeafNodes[1], expMap);
                if (right is FieldAccessExpNode) (left, right) = (right, left);
                result = new BinaryExpNode(BinaryExpType.Equal, left, right);
                break;
            }
            
            // a >= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greateequal)}":
            {
                var left = await VisitExp(context, exp.LeafNodes[0], expMap);
                var right = await VisitExp(context, exp.LeafNodes[1], expMap);
                result = left is FieldAccessExpNode 
                    ? new BinaryExpNode(BinaryExpType.GreaterEqual, left, right) 
                    : new BinaryExpNode(BinaryExpType.LessEqual, right, left);
                break;
            }
            
            // a > b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greatethan)}":
            {
                var left = await VisitExp(context, exp.LeafNodes[0], expMap);
                var right = await VisitExp(context, exp.LeafNodes[1], expMap);
                result = left is FieldAccessExpNode 
                    ? new BinaryExpNode(BinaryExpType.GreaterThan, left, right) 
                    : new BinaryExpNode(BinaryExpType.LessThan, right, left);
                break;
            }
            
            // a <= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessequal)}":
            {
                var left = await VisitExp(context, exp.LeafNodes[0], expMap);
                var right = await VisitExp(context, exp.LeafNodes[1], expMap);
                result = left is FieldAccessExpNode 
                    ? new BinaryExpNode(BinaryExpType.LessEqual, left, right) 
                    : new BinaryExpNode(BinaryExpType.GreaterEqual, right, left);
                break;
            }
            
            // a < b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessthan)}":
            {
                var left = await VisitExp(context, exp.LeafNodes[0], expMap);
                var right = await VisitExp(context, exp.LeafNodes[1], expMap);
                result = left is FieldAccessExpNode 
                    ? new BinaryExpNode(BinaryExpType.LessThan, left, right) 
                    : new BinaryExpNode(BinaryExpType.GreaterThan, right, left);
                break;
            }
            
            // a != b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notequal)}":
            {
                var left = await VisitExp(context, exp.LeafNodes[0], expMap);
                var right = await VisitExp(context, exp.LeafNodes[1], expMap);
                if (right is FieldAccessExpNode) (left, right) = (right, left);
                result = new BinaryExpNode(BinaryExpType.NotEqual, left, right);
                break;
            }
            
            // a | b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.orelse)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.OrElse,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
            
            // !a
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}":
            {
                // try to convert the exp to [not] part
                var notExp = await VisitExp(context, exp.LeafNodes[0], expMap);
                if (notExp is not BinaryExpNode binaryNotExp) throw new NotSupportedException("The system.logic.not expression not supported");

                switch (binaryNotExp.Type)
                {
                    case BinaryExpType.Equal:
                        result = new BinaryExpNode(BinaryExpType.NotEqual, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.NotEqual:
                        result = new BinaryExpNode(BinaryExpType.Equal, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.GreaterThan:
                        result = new BinaryExpNode(BinaryExpType.LessEqual, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.GreaterEqual:
                        result = new BinaryExpNode(BinaryExpType.LessThan, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.LessThan:
                        result = new BinaryExpNode(BinaryExpType.GreaterEqual, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.LessEqual:
                        result = new BinaryExpNode(BinaryExpType.GreaterThan, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.Contains:
                        result = new BinaryExpNode(BinaryExpType.NotContains, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    case BinaryExpType.NotContains:
                        result = new BinaryExpNode(BinaryExpType.Contains, binaryNotExp.Left, binaryNotExp.Right);
                        break;
                    default:
                        throw new NotSupportedException("The system.logic.not expression not supported");
                }
                break;
            }
            
            // a[b]
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.contains)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.Contains,
                    await VisitExp(context, exp.LeafNodes[0], expMap) as ValueExpNode 
                           ?? throw new NotSupportedException($"The list of ${exp.Name} can't be resolved"),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
                        
            // a.b
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
            {
                if (leafNodes[0] is StructExpNode structNode 
                    && leafNodes[1] is ValueExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName))
                {
                    result = new FieldAccessExpNode(fieldName);
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
                if (leafNodes[0] is FieldAccessExpNode
                    && leafNodes[1] is ValueExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } prefix
                    && !string.IsNullOrEmpty(prefix))
                {
                    result = new BinaryExpNode(
                        BinaryExpType.StartsWith,
                        leafNodes[0],
                        leafNodes[1]
                    );
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
                if (leafNodes[0] is FieldAccessExpNode
                    && leafNodes[1] is ValueExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } suffix
                    && !string.IsNullOrEmpty(suffix))
                {
                    result = new BinaryExpNode(
                        BinaryExpType.EndsWith,
                        leafNodes[0],
                        leafNodes[1]
                    );
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
                if (leafNodes[0] is FieldAccessExpNode
                    && leafNodes[1] is ValueExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } substr
                    && !string.IsNullOrEmpty(substr))
                {
                    result = new BinaryExpNode(
                        BinaryExpType.ContainsStr,
                        leafNodes[0],
                        leafNodes[1]
                    );
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
    
    /// <summary>
    /// Calculate the expression with args to value exp node
    /// </summary>
    static async Task<ValueExpNode> VisitExp(SchemaContext context, FunctionNodeExpression exp, params ExpNode?[] args)
    {
        return args.Any(a => a is not ValueExpNode) 
            ? throw new NotSupportedException("Only support value exp node") 
            : new ValueExpNode(await context.CallFunctionAsync(exp.FuncNode!, 
                args.Select(a => (a as ValueExpNode)?.Value).ToArray()));
    }

    /// <summary>
    /// Gets the expression node by field name
    /// </summary>
    static BinaryExpNode? FindFieldAccessOper(this ExpNode exp, string fieldName)
    {
        if (exp is BinaryExpNode binaryExpNode)
        {
            if (binaryExpNode.Left is FieldAccessExpNode leftField && leftField.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase) 
                || binaryExpNode.Right is FieldAccessExpNode rightField && rightField.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                return binaryExpNode;
            
            return FindFieldAccessOper(binaryExpNode.Left, fieldName) ?? FindFieldAccessOper(binaryExpNode.Right, fieldName);
        }
        return null;
    }

    /// <summary>
    /// Convert the exp tree to SQL
    /// </summary>
    public static string ToSql(this ExpNode exp, ISqlProvider sqlProvider, string prefix = "")
    {
        switch(exp)
        {
            case FieldAccessExpNode access:
                return $"{prefix}{sqlProvider.QuoteField(access.FieldName)}";
            case BinaryExpNode binary:
                switch (binary.Type)
                {
                    case BinaryExpType.AndAlso:
                    case BinaryExpType.OrElse:
                    case BinaryExpType.Equal:
                    case BinaryExpType.NotEqual:
                    case BinaryExpType.GreaterThan:
                    case BinaryExpType.GreaterEqual:
                    case BinaryExpType.LessThan:
                    case BinaryExpType.LessEqual:
                        return sqlProvider.Binary(binary.Type, 
                            ToSql(binary.Left, sqlProvider, prefix), 
                            ToSql(binary.Right, sqlProvider, prefix));
                    case BinaryExpType.Contains:
                        return sqlProvider.In(
                            ToSql(binary.Right, sqlProvider, prefix),
                            ((binary.Left as ValueExpNode)!.Value as ArrayTypeNode)!);
                    case BinaryExpType.NotContains:
                        return sqlProvider.NotIn(
                            ToSql(binary.Right, sqlProvider, prefix),
                            ((binary.Left as ValueExpNode)!.Value as ArrayTypeNode)!);
                    case BinaryExpType.StartsWith:
                        return sqlProvider.LikeStartsWith(
                            ToSql(binary.Left, sqlProvider, prefix),
                            (binary.Right as ValueExpNode)?.Value?.ToValue<string>() 
                                ?? throw new NotSupportedException("The startsWith right value must be string"));
                    case BinaryExpType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(binary.Left, sqlProvider, prefix),
                            (binary.Right as ValueExpNode)?.Value?.ToValue<string>() 
                                ?? throw new NotSupportedException("The endsWith right value must be string"));
                    case BinaryExpType.ContainsStr:
                        return sqlProvider.LikeContains(
                            ToSql(binary.Left, sqlProvider, prefix),
                            (binary.Right as ValueExpNode)?.Value?.ToValue<string>() 
                                ?? throw new NotSupportedException("The contains right value must be string"));
                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            case ValueExpNode value:
                return sqlProvider.Literal(value.Value);
        }

        throw new NotSupportedException("The expression type not supported");
    }
}

#region Exp access type

/// <summary>
/// The exp access type
/// </summary>
public enum BinaryExpType
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
public abstract record ExpNode;

/// <summary>
/// The struct argument node
/// </summary>
public record StructExpNode(StructType StructType) : ExpNode;

/// <summary>
/// Teh struct field access node
/// </summary>
public record FieldAccessExpNode(string FieldName) : ExpNode;

/// <summary>
/// The binary expression node
/// </summary>
public record BinaryExpNode(BinaryExpType Type, ExpNode Left, ExpNode Right) : ExpNode;

/// <summary>
/// The value expression node
/// </summary>
public record ValueExpNode(AnySchemaNode? Value) : ExpNode;

#endregion