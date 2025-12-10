using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Enum;
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
    public static async Task<AccessExpNode> Visit(this SchemaContext context, FunctionType func)
    {
        if (func.AccessExpNode != null) return func.AccessExpNode;
        
        // verify the function
        _ = func.GetSchemaFuncInfo(context) ?? throw new Exception($"Function {func.Name} can't be complied");
        
        StructType structType = func.Args.ElementAtOrDefault(0)?.TypeNode as StructType ?? throw new NotSupportedException("The function struct type parameter not valid");

        if (func.ReturnNode is not ScalarType { IsBool: true })
            throw new NotSupportedException("The function return type must be bool");
        
        // scan the exp trees
        FunctionNodeExpression last = func.ExpTrees.LastOrDefault() as FunctionNodeExpression
            ?? throw new NotSupportedException(NotValid);
        
        // init the exp map
        Dictionary<FunctionNodeExpTree, AccessExpNode> expMap = [];
        expMap[func.Args[0]] = new StructAccessExpNode(structType);
        for (int i = 1; i < func.Args.Length; i++)
            // for custom func, arg must have type, no system func will be used for visit
            expMap[func.Args[i]] = new ArgNode(func.Args[i].TypeNode ?? throw new NotSupportedException($"The function {func.Name} can't be used as row access func"), i - 1);
        
        // visit the exp tree
        AccessExpNode accessExp =  await VisitExp(context, last, expMap);
        func.AccessExpNode = accessExp;
        return accessExp;
    }

    /// <summary>
    /// Clone the access exp and replace the arg nodes with values
    /// </summary>
    public static AccessExpNode Expand(this AccessExpNode accessExp, params object[] args)
    {
        if (args.Length == 0) return accessExp; // no args to replace, use original
        return accessExp switch
        {
            FieldAccessAccessExpNode access => new FieldAccessAccessExpNode(access.Struct, access.FieldName),
            BinaryAccessExpNode binary => new BinaryAccessExpNode(binary.Type, binary.Left.Expand(args), binary.Right.Expand(args)),
            ValueAccessExpNode value => value,
            ArgNode arg => args.Length > arg.Index
                ? new ValueAccessExpNode(arg.Type, arg.Type.CreateNode(args[arg.Index]))
                : throw new NotSupportedException("The argument index is out of range"),
            _ => accessExp
        };
    }

    public static AccessExpNode And(this AccessExpNode left, AccessExpNode right)
        => new BinaryAccessExpNode(BinaryAccessExpType.AndAlso, left, right);
    
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
                            new ValueAccessExpNode(field.TypeNode.GetArrayType(), new ArrayTypeNode(field.TypeNode!, arr)),
                            new FieldAccessAccessExpNode(structAccess, key))),
                    JsonValue val => new BinaryAccessExpNode(BinaryAccessExpType.AndAlso, accessExp,
                        new BinaryAccessExpNode(BinaryAccessExpType.Equal, new FieldAccessAccessExpNode(structAccess, key),
                            new ValueAccessExpNode(field.TypeNode, field.TypeNode!.CreateNode(val)))),
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
    internal static async Task<AccessExpNode> VisitExp(this SchemaContext context, FunctionNodeExpTree? expTree, Dictionary<FunctionNodeExpTree, AccessExpNode> expMap, bool skipType = false)
    {
        // cache
        if (expTree != null && expMap.TryGetValue(expTree, out AccessExpNode? result)) return result;
        
        // const value
        if (expTree is ConstantExpNode constNode)
        {
            result = new ValueAccessExpNode(constNode.TypeNode, constNode.Value ?? constNode.TypeNode?.CreateNode(constNode.Value));
            expMap.Add(constNode, result);
            return result;
        }
        
        // exp only
        if (expTree is not FunctionNodeExpression exp)
            throw new NotSupportedException(NotValid);

        // visit leaf nodes
        AccessExpNode[] leafNodes = new AccessExpNode[exp.LeafNodes.Length];
        for (int i = 0; i < exp.LeafNodes.Length; i++)
            leafNodes[i] = await VisitExp(context, exp.LeafNodes[i], expMap);

        // all loaded, calc directly
        if (leafNodes.All(l => l is ValueAccessExpNode))
        {
            AnySchemaNode? res = await context.CallFunctionAsync(exp.FuncNode!,
                leafNodes.Select(a => (a as ValueAccessExpNode)?.Value).ToArray());
            result = new ValueAccessExpNode(res?.Type ?? exp.FuncNode!.ReturnNode, res);
            expMap.Add(exp, result);
            return result;
        }
        
        // check call type
        if (!skipType && exp.Type != ExpressionType.Call) throw new NotSupportedException(NotValid);
        
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

                result = new BinaryAccessExpNode(
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
                if (notExp is not BinaryAccessExpNode binaryNotExp)
                    throw new NotSupportedException("The system.logic.not expression not supported");

                result = binaryNotExp.Type switch
                {
                    BinaryAccessExpType.Equal => new BinaryAccessExpNode(BinaryAccessExpType.NotEqual,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.NotEqual => new BinaryAccessExpNode(BinaryAccessExpType.Equal,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.GreaterThan => new BinaryAccessExpNode(BinaryAccessExpType.LessEqual,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.GreaterEqual => new BinaryAccessExpNode(BinaryAccessExpType.LessThan,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.LessThan => new BinaryAccessExpNode(BinaryAccessExpType.GreaterEqual,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.LessEqual => new BinaryAccessExpNode(BinaryAccessExpType.GreaterThan,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.Contains => new BinaryAccessExpNode(BinaryAccessExpType.NotContains,
                        binaryNotExp.Left, binaryNotExp.Right),
                    BinaryAccessExpType.NotContains => new BinaryAccessExpNode(BinaryAccessExpType.Contains,
                        binaryNotExp.Left, binaryNotExp.Right),
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
                    result = new BinaryAccessExpNode(BinaryAccessExpType.Equal,
                        new FieldAccessAccessExpNode(structNode, fieldName), valNode);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }

                break;
            }

            // a[b] != c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotequal)}":
            {
                AccessExpNode valNode = leafNodes[2];
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && valNode is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.NotEqual,
                        new FieldAccessAccessExpNode(structNode, fieldName), valNode);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }

                break;
            }

            // a[b] >= c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldgreateequal)}":
            {
                AccessExpNode valNode = leafNodes[2];
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && valNode is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.GreaterEqual,
                        new FieldAccessAccessExpNode(structNode, fieldName), valNode);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }

                break;
            }

            // a[b] > c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldgreatethan)}":
            {
                AccessExpNode valNode = leafNodes[2];
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && valNode is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.GreaterThan,
                        new FieldAccessAccessExpNode(structNode, fieldName), valNode);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }

                break;
            }

            // a[b] <= c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldlessequal)}":
            {
                AccessExpNode valNode = leafNodes[2];
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && valNode is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.LessEqual,
                        new FieldAccessAccessExpNode(structNode, fieldName), valNode);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }

                break;
            }

            // a[b] < c
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldlessthan)}":
            {
                AccessExpNode valNode = leafNodes[2];
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && valNode is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.LessThan,
                        new FieldAccessAccessExpNode(structNode, fieldName), valNode);
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

            // a[b].startswith(c)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldstartswith)}":
            {
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && leafNodes[2] is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.StartsWith,
                        new FieldAccessAccessExpNode(structNode, fieldName), leafNodes[2]);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }

                break;
            }

            // a[b].endswith(c)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldendswith)}":
            {
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && leafNodes[2] is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.EndsWith,
                        new FieldAccessAccessExpNode(structNode, fieldName), leafNodes[2]);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }

            // a[b].contains(c)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldcontains)}":
            {
                if (leafNodes[0] is StructAccessExpNode structNode
                    && leafNodes[1] is ValueAccessExpNode { Value: ScalarTypeNode { IsEmpty: false } scalarNode }
                    && scalarNode.ToValue<string>() is { } fieldName
                    && !string.IsNullOrEmpty(fieldName)
                    && structNode.StructType.Fields.Any(f => f.Name == fieldName)
                    && leafNodes[2] is ValueAccessExpNode or ArgNode)
                {
                    result = new BinaryAccessExpNode(BinaryAccessExpType.Contains,
                        new FieldAccessAccessExpNode(structNode, fieldName), leafNodes[2]);
                }
                else
                {
                    throw new NotSupportedException($"The field name of ${exp.Name} can't be resolved");
                }
                break;
            }

        // complex func check
            default:
            {
                var info = exp.FuncNode?.GetSchemaFuncInfo(context);
                
                // only support non-system function
                if (info == null || (info.Sign & FUNC_SIGN_IMMUTABLE) > 0)
                    throw new NotSupportedException($"The expression not supported: {exp.Name} ({exp.Func})");
            
                FunctionType func = info.FunctionNode!;
                if (leafNodes.ElementAtOrDefault(0) is not StructAccessExpNode structAccess 
                    || func.ReturnNode is not ScalarType { IsBool: true }) 
                    throw new NotSupportedException(NotValid);

                // scan the exp trees
                FunctionNodeExpression last = func.ExpTrees.LastOrDefault() as FunctionNodeExpression
                                              ?? throw new NotSupportedException(NotValid);
    
                // bind the args to exp map
                expMap[func.Args[0]] = structAccess;
                for (int i = 1; i < func.Args.Length; i++)
                    expMap[func.Args[i]] = await VisitExp(context, exp.LeafNodes[i], expMap) ?? throw new NotSupportedException(NotValid);
    
                // visit the exp tree
                result = await VisitExp(context, last, expMap);
                break;
            }
        }
        
        // cache the result
        expMap.Add(exp, result ?? throw new NotSupportedException(NotValid));
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

    private const string NotValid = "The function exp tree not valid for row access";
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
public record ValueAccessExpNode(AnySchemeType? Type, AnySchemaNode? Value) : AccessExpNode;

/// <summary>
/// The argument node
/// </summary>
public record ArgNode(AnySchemeType Type, int Index) : AccessExpNode;

#endregion