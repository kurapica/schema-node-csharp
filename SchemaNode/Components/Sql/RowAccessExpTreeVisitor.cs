using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
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
    public static async Task<ExpNode> Visit(SchemaContext context, FunctionType func)
    {
        if (func.Args.Length != 1)
            throw new NotSupportedException("Only support one parameter function");

        if (func.Args[0].TypeNode is not StructType)
            throw new NotSupportedException("Only support struct type parameter");

        if (func.ReturnNode is not ScalarType { IsBool: true })
            throw new NotSupportedException("The function return type must be bool");

        // scan the exp trees
        FunctionNodeExpression last = func.ExpTrees.LastOrDefault() as FunctionNodeExpression
            ?? throw new NotSupportedException("The function exp tree is invalid");
        
        // visit the exp tree
        return await VisitExp(context, last, 
            new Dictionary<FunctionNodeExpTree, ExpNode>
                { { func.Args[0], new StructExpNode((func.Args[0].TypeNode as StructType)!) } } );
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
            result = new ValueExpNode(constNode.TypeNode?.CreateNode(constNode.Value));
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
                result = new BinaryExpNode(
                    BinaryExpType.Equal,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
            
            // a >= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greateequal)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.GreaterEqual,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
            
            // a > b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greatethan)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.GreaterThan,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
            
            // a <= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessequal)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.LessEqual,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
            
            // a < b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessthan)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.LessThan,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
                break;
            }
            
            // a != b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notequal)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.NotEqual,
                    await VisitExp(context, exp.LeafNodes[0], expMap),
                    await VisitExp(context, exp.LeafNodes[1], expMap)
                );
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
                result = new UnaryExpNode(
                    UnaryExpType.Not,
                    await VisitExp(context, exp.LeafNodes[0], expMap)
                );
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
            
            // !a[b]
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.notcontains)}":
            {
                result = new BinaryExpNode(
                    BinaryExpType.NotContains,
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
            
            // a.b == c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldequal)}":
            {
                if (leafNodes[0] is StructExpNode structNode 
                    && leafNodes[1] is ValueExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode } 
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && leafNodes is [_, _, ValueExpNode { Value: ScalarTypeNode }])
                {
                    result = new BinaryExpNode(
                        BinaryExpType.Equal,
                        new FieldAccessExpNode(fieldName),
                        leafNodes[2]
                    );
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }

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

public enum UnaryExpType
{
    Not,
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
/// Teh unary expression node
/// </summary>
public record UnaryExpNode(UnaryExpType Type, ExpNode Operand) : ExpNode;

/// <summary>
/// The value expression node
/// </summary>
public record ValueExpNode(AnySchemaNode? Value) : ExpNode;

#endregion