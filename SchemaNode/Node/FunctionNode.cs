using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using static SchemaNode.Utility.Constant;
using System.Text.RegularExpressions;
using SchemaNode.Attribute;
using SchemaNode.Utility;
using static SchemaNode.Utility.Schema;
using ExpressionType = SchemaNode.Enum.ExpressionType;
// ReSharper disable UnusedMember.Local

namespace SchemaNode.Node;

/// <summary>
/// The in-memory function schema representation
/// </summary>
public class FunctionNode: AnySchemaNode
{
    #region Data
    
    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FunctionNodeArgument[] Args { get; set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FunctionNodeExpression[] Exps { get; set; } = [];

    /// <summary>
    /// The basic type of generic types, provided to T(single generic type),
    /// T1, T2(for multi generic type)
    /// </summary>
    public AnySchemaNode?[] Generic { get; set; } = [];

    /// <summary>
    /// Call server if server provided
    /// </summary>
    public bool? Server  { get; set; }

    /// <summary>
    /// The client should not cache the result
    /// </summary>
    public bool? Nocache  { get; set; }
    
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Func;
    
    /// <summary>
    /// Whether the function will be used to construct the object
    /// </summary>
    public bool IsStructConstructor { get; set; }

    /// <summary>
    /// Whether the function is remote call only
    /// </summary>
    public bool IsRemoteCall { get; set; }

    /// <summary>
    /// Whether the function require call server
    /// </summary>
    public bool RequireRemoteCall { get; set; }

    /// <summary>
    /// Whether the function is defined as system, direct call
    /// </summary>
    public bool IsSystemCall { get; set; }
    
    /// <summary>
    /// The function info
    /// </summary>
    internal SchemaFuncInfo? FuncInfo { get; set; }
    
    #endregion
    
    #region Ref
    
    /// <summary>
    /// The return type node
    /// </summary>
    public AnySchemaNode? ReturnNode { get; set; }

    /// <summary>
    /// The root expression trees
    /// </summary>
    public List<FunctionNodeExpTree> ExpTrees { get; private set; } = [];
    
    #endregion
    
    #region Methods

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        FunctionSchema? func = schema.Func;
        
        // Data
        Return = func?.Return ?? string.Empty;
        Args = func?.Args.Select(a => (FunctionNodeArgument)a).ToArray() ?? [];
        Exps = func?.Exps.Select(e => (FunctionNodeExpression)e).ToArray() ?? [];
        Generic = func?.Generic != null ? new AnySchemaNode?[func.Generic.Length] : [];
        Server = func?.Server;
        Nocache = func?.Nocache;

        // Status
        if (func == null)
        {
            Status = SchemaNodeStatus.NoDefinition;
            return;
        }
        
        // Generic check
        if (Generic.Length > 0)
        {
            for(int i = 0; i < Generic.Length; i++)
            {
                string name = func.Generic![i];
                if (!string.IsNullOrWhiteSpace(name) && !Regex.IsMatch(name, @"^[tT]\d*$"))
                    Generic[i] = await context.GetSchemaNodeAsync(name);
            }
        }

        // Check if server or direct call
        IsRemoteCall = (LoadState & SchemaLoadState.Remote) > 0;
        IsSystemCall = (LoadState & SchemaLoadState.System) > 0;
        RequireRemoteCall = IsRemoteCall;

        // Gets return type
        if (string.IsNullOrWhiteSpace(Return))
        {
            Status = SchemaNodeStatus.FunctionWrongReturnType;
        }
        else if (Regex.IsMatch(Return, @"^[tT]\d*$"))
        {
            // Only system function can be generic
            if (!IsSystemCall)
            {
                Status = SchemaNodeStatus.FunctionWrongReturnType;
            }
            else
            {
                // Generic type
                int index = Return.Length > 1 && int.TryParse(Return[1..], out int i) ? i : 1;
                ResizeGeneric(index);
                ReturnNode = new GenericTypeNode
                {
                    GenericIndex = index,
                    BaseNode = Generic[index - 1]
                };
            }
        }
        else
        {
            ReturnNode = await context.GetSchemaNodeAsync(Return);
            if (ReturnNode == null || !ReturnNode.IsValueType) Status = SchemaNodeStatus.FunctionWrongReturnType;
        }

        // Generate the exp trees
        if (Status == SchemaNodeStatus.Ready)
        {
            (List<FunctionNodeExpTree> trees, string? error) = await BuildExpTrees(context);
            if (error != null)
                Status = Status == SchemaNodeStatus.Ready ? SchemaNodeStatus.FunctionExpsHasCompileError : Status;
            else
                ExpTrees = trees;

            // Check if client only
            if (Exps is { Length: > 0 })
            {
                foreach (FunctionNodeExpression exp in Exps.Where(e => e.FuncNode != null))
                {
                    if (exp.FuncNode!.IsSystemCall)
                        IsSystemCall = true;
                    if (exp.FuncNode.RequireRemoteCall)
                        RequireRemoteCall = true;
                }
            }

            // Check if used to generate the object
            IsStructConstructor = trees.LastOrDefault() is StructResultExpNode;
        }

        // Add usages
        if (Status == SchemaNodeStatus.Ready)
        {
            ReturnNode?.AddRef(this);
            foreach (FunctionNodeArgument arg in Args)
            {
                arg.TypeNode?.AddRef(this);
            }

            foreach (FunctionNodeExpression exp in Exps)
            {
                exp.TypeNode?.AddRef(this);
                exp.FuncNode?.AddRef(this);
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        ReturnNode?.RemoveRef(this);
        ReturnNode = null;
        foreach (FunctionNodeArgument arg in Args)
        {
            arg.TypeNode?.RemoveRef(this);
            arg.TypeNode = null;
        }

        foreach (FunctionNodeExpression exp in Exps)
        {
            exp.TypeNode?.RemoveRef(this);
            exp.TypeNode = null;
            exp.FuncNode?.RemoveRef(this);
            exp.FuncNode = null;
        }
        Args = [];
        Exps = [];
        ExpTrees = [];

        // Clear function info to be re-compiled
        ClearFunctionInfo();
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemaNode other) => false;

    /// <inheritdoc />
    public override ArrayNode? GetArrayNode(bool exactly = false) => null;
    
    // Clear the function info to be re-complied
    void ClearFunctionInfo()
    {
        if (FuncInfo != null && (FuncInfo.Sign & FUNC_SIGN_IMMUTABLE) > 0) return; // Immutable, no need to clear

        FuncInfo = null;
        if (UsedBy == null || UsedBy.IsEmpty) return;
        foreach ((AnySchemaNode other, _) in UsedBy)
        {
            if (other is FunctionNode func)
                func.ClearFunctionInfo();
        }
    }

    #endregion

    #region Exp Tree

    /// <summary>
    /// Build the expression tree based on the arguments and expressions
    /// </summary>
    async Task<(List<FunctionNodeExpTree>, string? error)> BuildExpTrees(SchemaContext context)
    {
        List<FunctionNodeExpTree> trees = new();
        Dictionary<string, FunctionNodeExpTree> treeMap = new();
        
        // Validate the arguments and reset the states
        foreach(FunctionNodeArgument arg in Args)
        {
            arg.Used = 0;
            arg.Status = SchemaNodeStatus.Ready;
            
            // Check argument name
            if (string.IsNullOrWhiteSpace(arg.Name))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentNoName;
                Status = SchemaNodeStatus.FunctionArgumentNoName;
                return (trees, TYPE_FUNC_ARG_NAME_REQUIRED);
            }
            else if (treeMap.ContainsKey(arg.Name))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentDuplicateName;
                Status = SchemaNodeStatus.FunctionArgumentDuplicateName;
                return (trees, TYPE_FUNC_ARG_NAME_DUPLICATE);
            }

            // Check argument type
            if (string.IsNullOrWhiteSpace(arg.Type))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentNoType;
                Status = SchemaNodeStatus.FunctionArgumentNoType;
                return (trees, TYPE_FUNC_ARG_NO_TYPE);
            }
            else if (Regex.IsMatch(arg.Type, @"^[tT]\d*$"))
            {
                // Only system defined can have generic types
                if (!IsSystemCall)
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentNoType;
                    Status = SchemaNodeStatus.FunctionArgumentNoType;
                    return (trees, TYPE_FUNC_ARG_NO_TYPE);
                }
                else
                {
                    int index = Return.Length > 1 && int.TryParse(Return[1..], out int idx) ? idx : 1;
                    ResizeGeneric(index);
                    arg.TypeNode = new GenericTypeNode
                    {
                        GenericIndex = index,
                        BaseNode = Generic[index - 1]
                    };
                }
            }
            else
            {
                AnySchemaNode? node = await context.GetSchemaNodeAsync(arg.Type);
                if (node == null || !node.IsValueType)
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    return (trees, TYPE_FUNC_ARG_TYPE_NOT_VALID);
                }
                arg.TypeNode = node;
                arg.Type = node.Name; // fix the name
            }
            
            // Add to the tree
            treeMap[arg.Name] = arg;
        }

        // The system provide expression has no expression body
        if (IsSystemCall) return (trees, null);
        if (Exps.Length == 0)
        {
            Status = SchemaNodeStatus.FunctionNoExps;
            return (trees, TYPE_FUNC_NEED_EXPS);
        }

        // Validate the expressions and build the tree
        foreach (FunctionNodeExpression exp in Exps)
        {            
            int arrayArg = -1; // the array argument index
            AnySchemaNode? arrayRequireEle = null;
            bool isMapReduce = exp.Type != ExpressionType.Call;

            // reset
            exp.Used = 0;
            exp.Status = SchemaNodeStatus.Ready;

            // Check exp name
            if (string.IsNullOrWhiteSpace(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpNoName;
                Status = SchemaNodeStatus.FunctionExpNoName;
                return (trees, TYPE_FUNC_EXP_NAME_REQUIRED);
            }
            else if (treeMap.ContainsKey(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpDuplicateName;
                Status = SchemaNodeStatus.FunctionExpDuplicateName;
                return (trees, TYPE_FUNC_EXP_NAME_CONFLICT_ARG);
            }

            // Check function
            if (string.IsNullOrWhiteSpace(exp.Func))
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                Status = SchemaNodeStatus.FunctionExpWrongFunc;
                return (trees, TYPE_FUNC_EXP_CALL_FUNC_REQUIRED);
            }
            if (await context.GetSchemaNodeAsync(exp.Func) is not FunctionNode funcNode)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                Status = SchemaNodeStatus.FunctionExpWrongFunc;
                return (trees, TYPE_FUNC_EXP_CALL_FUNC_NOT_EXIST);
            }
            
            // check with call type
            switch (exp.Type)
            {
                // Check reduce function
                case ExpressionType.Reduce when funcNode.Args.Length is 0 or > 2:
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncForReduce;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncForReduce;
                    return (trees, TYPE_FUNC_CANT_USE_AS_REDUCE);

                // Check first function
                case ExpressionType.First when funcNode.ReturnNode is not ScalarNode { IsBool: true }:
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncForFirst;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncForFirst;
                    return (trees, TYPE_FUNC_CANT_USE_AS_FIRST);

                // Check last function
                case ExpressionType.Last when funcNode.ReturnNode is not ScalarNode { IsBool: true }:
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncForLast;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncForLast;
                    return (trees, TYPE_FUNC_CANT_USE_AS_LAST);

                // Check filter function
                case ExpressionType.Filter when funcNode.ReturnNode is not ScalarNode { IsBool: true }:
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncForFilter;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncForFilter;
                    return (trees, TYPE_FUNC_CANT_USE_AS_FILTER);
            }
            exp.FuncNode = funcNode;

            // no further check if func not valid
            if (funcNode.Status != SchemaNodeStatus.Ready)
            {
                exp.Status = SchemaNodeStatus.FunctionExpInValidFunc;
                Status = SchemaNodeStatus.FunctionExpInValidFunc;
                return (trees, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
            }

            // Gets the function info of the function node, only need static method info for generic types
            AnySchemaNode?[] genericTypes = funcNode.Generic.ToArray();
            
            // Gets the return type
            if (string.IsNullOrWhiteSpace(exp.Return))
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                Status = SchemaNodeStatus.FunctionWrongReturnType;
                return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
            }
            else
            {
                AnySchemaNode? node = await context.GetSchemaNodeAsync(exp.Return);
                if (node is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                    Status = SchemaNodeStatus.FunctionWrongReturnType;
                    return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                }
                exp.TypeNode = node;

                // validate the exp type & return type & func type
                bool skipMatch = false;
                if (exp.Type is ExpressionType.Map or ExpressionType.Filter)
                {
                    if (node is not ArrayNode arr || arr.ElementNode is null)
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        Status = SchemaNodeStatus.FunctionWrongReturnType;
                        return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    node = arr.ElementNode;
                    if (exp.Type is ExpressionType.Filter)
                    {
                        arrayRequireEle = node;
                        skipMatch = true;
                    }
                }
                else if (exp.Type is ExpressionType.First or ExpressionType.Last)
                {
                    if (node is ArrayNode)
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        Status = SchemaNodeStatus.FunctionWrongReturnType;
                        return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }

                    arrayRequireEle = node;
                    skipMatch = true;
                }

                if (!skipMatch)
                {
                    if (funcNode.ReturnNode is GenericTypeNode generic)
                    {
                        genericTypes[generic.GenericIndex - 1] = node;
                    }
                    else if (!(funcNode.ReturnNode!.CanBeUseAs(node) || 
                               exp.Type == ExpressionType.Map 
                               && funcNode.ReturnNode is ArrayNode { ElementNode: not null } arr
                               && arr.ElementNode.CanBeUseAs(node)))
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        Status = SchemaNodeStatus.FunctionWrongReturnType;
                        return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                }
            }

            // exp leaf
            exp.LeafNodes = new FunctionNodeExpTree[funcNode.Args.Length];

            // Check function call arguments and gets the return type
            // Also bind the type node to the expression call arguments, before compile
            if (funcNode.Args is { Length: > 0 })
            {
                // fill all exp args for checking
                if (exp.Args.Length < funcNode.Args.Length)
                    exp.Args = exp.Args.Concat(Enumerable.Range(0, funcNode.Args.Length - exp.Args.Length)
                        .Select(_ => new FunctionCallArgument())).ToArray();
                
                // check exp use variables first, they provide type infos
                for (int i = 0; i < funcNode.Args.Length; i++)
                {
                    FunctionCallArgument callArg = exp.Args[i];
                    if (string.IsNullOrWhiteSpace(callArg.Name)) continue;

                    FunctionNodeArgument funcArg = funcNode.Args[i];
                    AnySchemaNode? funcArgType = funcArg.TypeNode;
                    if (funcArgType is GenericTypeNode gn) funcArgType = genericTypes[gn.GenericIndex - 1];
                    
                    // Gets the arg/exp
                    if (treeMap.TryGetValue(callArg.Name, out FunctionNodeExpTree? value))
                    {
                        AnySchemaNode? argTypeNode;
                        switch (value)
                        {
                            case FunctionNodeArgument rArg:
                                exp.LeafNodes[i] = rArg; // Add to leaf
                                argTypeNode = rArg.TypeNode;
                                if (argTypeNode is GenericTypeNode generic)
                                    argTypeNode = genericTypes[generic.GenericIndex - 1];
                                if (argTypeNode == null)
                                {
                                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                    Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                    return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST); // For safe
                                }
                                break;
                            
                            case FunctionNodeExpression rExp:
                                exp.LeafNodes[i] = rExp; // Add to leaf
                                argTypeNode = rExp.TypeNode; // always exist
                                break;
                            
                            default:
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST); // For safe
                        }
                        
                        // Check if used as array type
                        if (isMapReduce && arrayArg < 0 && 
                            argTypeNode is ArrayNode { ElementNode: not null } array && 
                            funcArgType is not ArrayNode && 
                            (funcArgType is null || array.ElementNode.CanBeUseAs(funcArgType)) &&
                            (arrayRequireEle is null || array.ElementNode.CanBeUseAs(arrayRequireEle)))
                        {
                            arrayArg = i;
                            argTypeNode = array.ElementNode;
                        }
                        
                        // Match the type
                        if (funcArgType != null)
                        {
                            if (argTypeNode == null || !argTypeNode.CanBeUseAs(funcArgType))
                            {
                                // Error
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                return (trees, TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL);
                            }
                        }
                        else if (funcArg.TypeNode is GenericTypeNode g)
                        {
                            genericTypes[g.GenericIndex - 1] = argTypeNode;
                        }
                        
                        // save the type
                        callArg.TypeNode = argTypeNode;
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST);
                    }
                }
                
                // Check constant value arguments
                for (int i = 0; i < funcNode.Args.Length; i++)
                {
                    FunctionCallArgument callArg = exp.Args[i];
                    if (!string.IsNullOrWhiteSpace(callArg.Name)) continue;

                    FunctionNodeArgument funcArg = funcNode.Args[i];
                    AnySchemaNode? funcArgType = funcArg.TypeNode;
                    if (funcArgType is GenericTypeNode g)
                    {
                        funcArgType = genericTypes[g.GenericIndex - 1];
                    }

                    // for safe, couldn't be
                    if (funcArgType == null)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        return (trees, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
                    }
                    
                    // save type
                    callArg.TypeNode = funcArgType;
                    
                    // Check nullable
                    if (callArg.Value == null || callArg.Value.IsEmpty())
                    {
                        if ((funcNode.Args[i].Nullable ?? true) || (exp.Type == ExpressionType.Reduce && i == 1)) // Nullable or Reduce
                        {
                            exp.LeafNodes[i] = new ConstantExpNode
                            {
                                Value = null
                            };
                        }
                        else
                        {
                            exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            return (trees, TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH);
                        }
                    }
                    else
                    {
                        // Check the value
                        (object? r, JsonNode? e) = await funcArgType.ValidateValueAsync(context, callArg.Value);
                        if (e != null && !e.IsEmpty())
                        {
                            exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            return (trees, TYPE_FUNC_EXP_CALL_CONSTANT_NOT_VALID);
                        }
                        
                        callArg.Value = funcArgType.Type is SchemaType.Array or SchemaType.Struct ? r as JsonNode : JsonValue.Create(r);

                        // Add to leaf
                        exp.LeafNodes[i] = new ConstantExpNode
                        {
                            Value = r
                        };
                    }
                }
            }

            // Check array argument
            if (isMapReduce && arrayArg < 0)
            {
                exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                Status = SchemaNodeStatus.FunctionArgumentWrongType;
                return (trees, TYPE_FUNC_EXP_CALL_NO_ARRAY);
            }
            exp.ArrayIndex = isMapReduce ? arrayArg : null;

            // Mark leaf nodes used count++
            foreach (FunctionNodeExpTree? leaf in exp.LeafNodes)
            {
                if (leaf is null)
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    return (trees, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                leaf.Used++;
            }

            // Add to the tree
            treeMap[exp.Name] = exp;
        }

        // Validate the function return
        StructResultExpNode? structResultNode = null;
        if (!Exps.Last().TypeNode!.CanBeUseAs(ReturnNode!))
        {
            if (ReturnNode is StructNode { Fields: { Length: > 0 } } @struct)
            {
                // Check the struct fields
                List<FunctionNodeExpTree> leafNodes = []; // leaf nodes
                foreach (StructFieldConfig field in @struct.Fields.Where(f => !(f.DisplayOnly ?? false)))
                {
                    if (treeMap.TryGetValue(field.Name, out FunctionNodeExpTree? leaf))
                    {
                        if (leaf.TypeNode is null || !leaf.TypeNode.CanBeUseAs(field.TypeNode!))
                        {
                            Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                            return (trees, TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID);
                        }
                        leafNodes.Add(leaf);
                    }
                    else if (field.Require ?? false)
                    {
                        Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                        return (trees, TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID);
                    }
                }

                // Mark all leaf nodes as require
                leafNodes.ForEach(l => l.Used = 0);

                // Add struct result as the last exp tree
                structResultNode = new StructResultExpNode
                {
                    TypeNode = @struct,
                    LeafNodes = leafNodes.ToArray()
                };
            }
            else
            {
                return (trees, TYPE_FUNC_RETURN_NOT_VALID);
            }
        }

        // Reduce the trees, only the result and non-one-time-used exp will be added to the tree
        trees = Exps.Where(e => e.Used is > 1 or 0)
            .Select(p => (FunctionNodeExpTree)p).ToList();
        if (structResultNode != null) trees.Add(structResultNode);
        return (trees, null);
    }

    #endregion

    #region Complie

    #region Register System Functions

    /// <summary>
    /// Register all schema function and its namespace
    /// </summary>
    public static NodeSchema? GenerateSystemFunction(MethodInfo method, string? ns = null)
    {
        if (!method.IsStatic) return null;
        SchemaFuncAttribute? funcAttr = method.GetCustomAttribute<SchemaFuncAttribute>();
        if (funcAttr == null) return null;

        int sign = FUNC_SIGN_IMMUTABLE; // The system method won't be changed and already compiled
        if (method.IsGenericMethodDefinition) sign |= FUNC_SIGN_GENERIC;
        
        // Generate the arguments and result type
        ParameterInfo[] parameters = method.GetParameters();
        SchemaParamTypeInfo[] genInfos = method.GetGenericArguments().Select(g => g.GetSchemaTypeInfo(true)!).ToArray(); // The generic type infos

        // The schema context must be the first if used
        if (parameters.Length > 0 && (parameters[0].ParameterType == typeof(SchemaContext) || 
                                      parameters[0].ParameterType.IsSubclassOf(typeof(SchemaContext))))
        {
            sign |= FUNC_SIGN_CONTEXT;
            parameters = parameters.Skip(1).ToArray();
        }

        // Generate func schema
        var name = $"{(string.IsNullOrEmpty(ns) ? "" : $"{ns}.")}{(funcAttr.Type ?? method.Name).ToLowerInvariant()}";
        NodeSchema funcSchema = new NodeSchema
        {
            Name = name,
            Type = SchemaType.Func,
            Display = funcAttr.Display ?? name,
            Func = new FunctionSchema
            {
                Return = string.Empty,
                Args = new FunctionArgumentInfo[parameters.Length],
                Exps = [],
                Generic = genInfos.Select(g => g is { AnyArray: false, Number: true } 
                    ? NS_SYSTEM_NUMBER : "").ToArray(),
            }
        };

        // Return type
        SchemaParamTypeInfo? retInfo = method.ReturnType.GetSchemaTypeInfo(true);
        if (retInfo == null) return null;
        if (retInfo.Task) sign |= FUNC_SIGN_ASYNC;
        if (retInfo.Nullable) sign |= FUNC_SIGN_NULLABLE_RET;

        if (retInfo.Generic != null)
        {
            // IList<T>, use system.array instead
            if (retInfo.AnyArray)
            {
                funcSchema.Func.Return = NS_SYSTEM_ARRAY;
            }
            else
            {
                // single
                int gIdx = Array.FindIndex(genInfos, g => g.Generic == retInfo.Generic);
                if (gIdx >= 0)
                    funcSchema.Func.Return = genInfos.Length > 1 ? $"T{gIdx + 1}" : "T";
                else
                    return null;
            }
        }
        else if (string.IsNullOrEmpty(retInfo.SchemaType))
        {
            return null;
        }
        else
        {
            funcSchema.Func.Return = retInfo.SchemaType;
        }
        
        // Parameter types
        SchemaParamTypeInfo?[] paramInfos = parameters.Select(p => p.ParameterType.GetSchemaTypeInfo(true)).ToArray();
        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo p = parameters[i];
            SchemaParamTypeInfo? pt = paramInfos[i];
            if (pt == null) return null;
            
            FunctionArgumentInfo arg = new ()
            {
                Name = p.Name ?? $"arg{i}",
                Nullable = pt.Nullable || p.HasDefaultValue
            };
            funcSchema.Func.Args[i] = arg;

            // Check dynamic type
            if (pt.Generic != null)
            {
                if (pt.AnyArray)
                {
                    arg.Type = NS_SYSTEM_ARRAY;
                }
                else
                {
                    int gIdx = Array.FindIndex(genInfos, (g) => g.Generic == pt.Generic);
                    if (gIdx >= 0)
                    {
                        // generic type
                        arg.Type = genInfos.Length > 1 ? $"T{gIdx + 1}" : "T";
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(pt.SchemaType))
            {
                // normally if arg is Object, use func arg attr to specific the schema type
                if (p.GetCustomAttribute<SchemaFuncArgAttribute>() != null)
                {
                    pt.SchemaType = p.GetCustomAttribute<SchemaFuncArgAttribute>()!.Type;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                arg.Type = pt.SchemaType;
            }
        }

        // Save the method info to cache
        StaticMethodMap.TryAdd(funcSchema.Name, new SchemaFuncInfo
        {
            Name = funcSchema.Name,
            Method = method,
            Sign = sign,
            Generics = genInfos,
            Args = paramInfos!,
            Return = retInfo
        });

        return funcSchema;
    }

    #endregion

    #region Compile Custom Functions

    /// <summary>
    /// Compile a custom function node to dynamic method
    /// </summary>
    SchemaFuncInfo CompileFunction()
    {
        // Only full-filled function can be complied
        if (Status != SchemaNodeStatus.Ready)
            throw new Exception($"The {Name} can't be compiled because of {Status}");

        SchemaFuncInfo funcInfo = new SchemaFuncInfo
        {
            Name = Name,
            Sign = FUNC_SIGN_CONTEXT, // always use context for dynamic func
            FunctionNode = this,
            Args = Args.Select(a =>
            {
                var info = a.TypeNode!.GetSchemaTypeInfo()!;
                if (a.Nullable ?? false) info.Kind |= ParameterTypeKind.Nullable;
                return info;
            }).ToArray(),
            Return = ReturnNode!.GetSchemaTypeInfo()!
        };

        // Remote call, no dynamic function required
        if (IsRemoteCall)
        {
            funcInfo.Sign |= FUNC_SIGN_CONTEXT | FUNC_SIGN_REMOTE_CALL | FUNC_SIGN_ASYNC;
            return funcInfo;
        }

        // Compile the dynamic function node
        try
        {
            // Prepare
            Dictionary<string, Expression> paramExpMap = new();
            Dictionary<string, ParameterExpression> variableExpMap = new();
            var paramExps = new ParameterExpression[Args.Length + 1];

            // Build the parameters, no generic type for custom methods
            // Always add SchemaContext as the first parameters for inner call
            paramExps[0] = Expression.Parameter(typeof(SchemaContext)); 
            for (int i = 0; i < Args.Length; i++)
            {
                FunctionNodeArgument arg = Args[i];
                ParameterExpression paramExp = Expression.Parameter(arg.TypeNode?.ToCSharpType(arg.Nullable) 
                    ?? throw new Exception($"The { Name } can't be compiled - expression compile failed"));
                paramExps[i + 1] = paramExp;
                paramExpMap[arg.Name] = paramExp;
            }

            // Expression Tree -> Function Body
            List<Expression> expBlocks = new();
            int expCount = 0;
            foreach (FunctionNodeExpTree exp in ExpTrees)
            {
                Expression result = CompileFunctionNodeExpression(paramExps[0], paramExpMap, variableExpMap, expBlocks, exp);
                if (result == null) throw new Exception($"The {Name} can't be compiled - expression compile failed");

                // exp = result
                string expName = exp is FunctionNodeExpression nodeExp ? nodeExp.Name : $"_expResult{++expCount}";
                ParameterExpression expRes = Expression.Parameter(result.Type);
                variableExpMap.Add(expName, expRes);
                expBlocks.Add(Expression.Assign(expRes, result));
            }

            // Conversion last type
            // Gets the function and expression
            Type lastType = ReturnNode?.ToCSharpType() ?? throw new Exception($"The {Name} can't be compiled - return type not valid");
            Expression lastExp = expBlocks.Last();
            if (lastType != lastExp.Type)
            {
                string expName = "_final";
                ParameterExpression expRes = Expression.Parameter(lastType);
                variableExpMap.Add(expName, expRes);
                expBlocks.Add(Expression.Assign(expRes, ConvertExp(lastType, lastExp)));
            }

            // Build block
            BlockExpression blockExpr =
                Expression.Block(
                    variableExpMap.Values.ToArray(),
                    expBlocks
                );

            // Build the dynamic method
            Delegate dynamicMethod = CompileMethod(lastType, paramExps, blockExpr);

            // Build the function info
            funcInfo.Method = dynamicMethod.Method;
            funcInfo.DynamicMethod = dynamicMethod;
            return funcInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    // Compile the method to delegate
    static Delegate CompileMethod(Type retType, IReadOnlyList<ParameterExpression> paramExps, BlockExpression blockExpr)
    {
        Type[] funcTypes = new Type[paramExps.Count + 1];
        for (int i = 0; i < paramExps.Count; i++)
        {
            funcTypes[i] = paramExps[i].Type;
        }
        funcTypes[paramExps.Count] = retType;
        Type lambdaType = funcTypes.Length switch
        {
            2 => typeof(Func<,>).MakeGenericType(funcTypes),
            3 => typeof(Func<,,>).MakeGenericType(funcTypes),
            4 => typeof(Func<,,,>).MakeGenericType(funcTypes),
            5 => typeof(Func<,,,,>).MakeGenericType(funcTypes),
            6 => typeof(Func<,,,,,>).MakeGenericType(funcTypes),
            7 => typeof(Func<,,,,,,>).MakeGenericType(funcTypes),
            8 => typeof(Func<,,,,,,,>).MakeGenericType(funcTypes),
            9 => typeof(Func<,,,,,,,,>).MakeGenericType(funcTypes),
            10 => typeof(Func<,,,,,,,,,>).MakeGenericType(funcTypes),
            11 => typeof(Func<,,,,,,,,,,>).MakeGenericType(funcTypes),
            12 => typeof(Func<,,,,,,,,,,,>).MakeGenericType(funcTypes),
            13 => typeof(Func<,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            14 => typeof(Func<,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            15 => typeof(Func<,,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            16 => typeof(Func<,,,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            17 => typeof(Func<,,,,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            _ => throw new ArgumentOutOfRangeException()
        };
        return (Delegate)typeof(FunctionNode)
            .GetMethod(nameof(ComplieDynamicMethod), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(lambdaType)
            .Invoke(null, [blockExpr, paramExps])!;
    }
    
    /// <summary>
    /// Compile a function node expression to expression
    /// </summary>
    static Expression CompileFunctionNodeExpression(Expression contextExp, IReadOnlyDictionary<string, Expression> paramMap, Dictionary<string, ParameterExpression> expMap, List<Expression> blocks, FunctionNodeExpTree expTree)
    {
        switch (expTree)
        {
            case FunctionNodeExpression exp:
            {
                SchemaFuncInfo callFuncInfo = exp.FuncNode?.GetSchemaFuncInfo()
                    ?? throw new Exception($"The expression {exp.Name} can't be compiled - return type not supported");
                int useContext = (callFuncInfo.Sign & FUNC_SIGN_CONTEXT) == FUNC_SIGN_CONTEXT ? 1 : 0;

                // Prepare the call arguments
                int arrayIndex = -1;
                Expression[] callArgs = new Expression[exp.LeafNodes.Length + useContext];
                Type[] callArgTypes = new Type[callArgs.Length];
                if (useContext > 0)
                {
                    callArgs[0] = contextExp;
                    callArgTypes[0] = typeof(SchemaContext);
                }

                // Add leaf nodes
                for (int i = 0; i < exp.LeafNodes.Length; i++)
                {
                    // Gets the type
                    Type callType = exp.Args[i].TypeNode?.ToCSharpType(exp.FuncNode!.Args[i].Nullable)
                        ?? throw new Exception($"The expression {exp.Name}'s {i} argument type not valid.");

                    // Build the call arguments
                    switch (exp.LeafNodes[i])
                    {
                        case ConstantExpNode constExp:
                            if (constExp.Value == null && !callType.IsNullable())
                            {
                                // For reduce
                                callArgs[i + useContext] = Expression.Default(callType);
                            }
                            else
                            {
                                object? value = constExp.Value;
                                if (value != null)
                                    value = callType.GetNotNullType().TryConvert(value);
                                callArgs[i + useContext] = Expression.Constant(value, callType);
                            }
                            break;
                        
                        case FunctionNodeArgument argExp:
                            callArgs[i + useContext] = paramMap[argExp.Name];
                            break;
                        
                        case FunctionNodeExpression otherExp:
                            if (otherExp.Used == 1)
                            {
                                // Embed the other expression
                                callArgs[i + useContext] = CompileFunctionNodeExpression(contextExp, paramMap, expMap, blocks, otherExp);
                            }
                            else
                            {
                                // Use variable expression
                                callArgs[i + useContext] = expMap[otherExp.Name];
                            }
                            break;
                    }

                    // Convert the call type to argument type
                    if (exp.ArrayIndex != i) // !exp.LeafNodes[i].UseAsArray)
                    {
                        // Add Conversion
                        callArgs[i + useContext] = ConvertExp(callType, callArgs[i + useContext]);
                        callArgTypes[i + useContext] = callArgs[i + useContext].Type;
                    }
                    else
                    {
                        arrayIndex = i + useContext;
                        callArgTypes[i + useContext] = exp.LeafNodes[i]?.TypeNode is ArrayNode a ? (a.ElementNode?.ToCSharpType() ?? callType) : callType;
                    }
                }

                // Call the functions
                MethodInfo callMethod = callFuncInfo.Method!;
                Type expReturnType = exp.TypeNode?.ToCSharpType((callFuncInfo.Sign & FUNC_SIGN_NULLABLE_RET) > 0) 
                                     ?? throw new Exception($"The expression {exp.Name}'s type not valid");
                Type epxReturnElement = (exp.Type is ExpressionType.Map or ExpressionType.Filter) && exp.TypeNode is ArrayNode arr
                    ? (arr.ElementNode?.ToCSharpType() ?? throw new Exception($"The expression {exp.Name}'s type not valid"))
                    : expReturnType;

                // Make generic method for system defined methods
                if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE && (callFuncInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
                {
                    // Generate the generic method
                    Type?[] genTypes = callFuncInfo.Generics.Select(p =>
                    {
                        SchemaParamTypeInfo? info = null;
                        Type? type = null;
                        if (callFuncInfo.Return.Generic == p.Generic)
                        {
                            info = callFuncInfo.Return;
                            type = epxReturnElement?.GetNotNullType();
                        }
                        else
                        {
                            for (int j = 0; j < callFuncInfo.Args.Length; j++)
                            {
                                SchemaParamTypeInfo sinfo = callFuncInfo.Args[j];
                                if (sinfo.Generic == p.Generic)
                                {
                                    info = sinfo;
                                    type = callArgTypes[j + useContext].GetNotNullType();
                                    break;
                                }
                            }
                        }

                        if (info == null || type == null) return null;
                        if (info.Array && type.IsSZArray) return type.GetElementType();
                        if ((info.List || info.Enumerable) && type.GetGenericArguments() is { Length: > 0} args) return args[0];
                        return type;
                    }).ToArray();
                    if (genTypes.Length == 0 || genTypes.Any(g => g == null))
                        throw new Exception($"The expression {exp.Name}'s generic type not valid");
                    string genSign = string.Join('|', genTypes.Select(p => (Nullable.GetUnderlyingType(p) ?? p).FullName));
                    callMethod = callFuncInfo.GenericMethods.GetOrAdd(genSign, _ => callFuncInfo.Method!.MakeGenericMethod(genTypes!));
                }
                
                // Use remote call
                else if ((callFuncInfo.Sign & FUNC_SIGN_REMOTE_CALL) == FUNC_SIGN_REMOTE_CALL)
                {
                    string?[] genTypes = callFuncInfo.Generics.Select(p =>
                    {
                        SchemaParamTypeInfo? info = null;
                        Type? type = null;
                        if (callFuncInfo.Return.Generic == p.Generic)
                        {
                            info = callFuncInfo.Return;
                            type = expReturnType.GetNotNullType();
                        }
                        else
                        {
                            for (int j = 0; j < callFuncInfo.Args.Length; j++)
                            {
                                SchemaParamTypeInfo sinfo = callFuncInfo.Args[j];
                                if (sinfo.Generic == p.Generic)
                                {
                                    info = sinfo;
                                    type = callArgTypes[j + useContext].GetNotNullType();
                                    break;
                                }
                            }
                        }

                        if (info == null || type == null) return null;                        
                        if (info.Array && type.IsSZArray) return type.GetElementType()?.GetSchemaType();
                        if ((info.List || info.Enumerable) && type.GetGenericArguments() is { Length: > 0} args) return args[0].GetSchemaType();
                        return type.GetSchemaType();
                    }).ToArray();
                    
                    // Generate the call
                    var convCallArgs = new Expression[callArgs.Length + 2];
                    convCallArgs[0] = callArgs[0];
                    convCallArgs[1] = Expression.Constant(callFuncInfo.Name);
                    convCallArgs[2] = Expression.Constant(genTypes);
                    for (int i = 1; i < callArgs.Length; i++)
                        convCallArgs[i + 2] = callArgs[i];
                    callArgs = convCallArgs;
                    if (arrayIndex >= 0) arrayIndex += 2;
                    int count = callArgs.Length - 3;
                    callMethod = typeof(FunctionNode).GetMethod($"CallRemoteFunction{count}", BindingFlags.Static | BindingFlags.NonPublic)!;

                    // Make generic type
                    if (count > 0)callMethod = callMethod.MakeGenericMethod(callArgs.Skip(3).Select(e => e.Type).Prepend(epxReturnElement).ToArray());
                }

                // Generate call body
                if (exp.Type == ExpressionType.Call)
                    return GenMethodCallExp(callFuncInfo, callMethod, callArgs);

                // Generate the lambda for collection operations
                Type callMethodReturn = callMethod.ReturnType;
                if (callMethodReturn.IsSubclassOfGenericType(typeof(Task<>)))
                    callMethodReturn = callMethodReturn.GetGenericArguments()[0];

                // Parameters
                ParameterExpression[] innerParams = callArgs.Select(p => Expression.Parameter(p.Type)).ToArray();
                Expression[] innerCallArgs = innerParams.Select(p => (Expression)p).ToArray();
                Expression jarray = innerCallArgs[arrayIndex];
                ParameterExpression resExp = Expression.Parameter(exp.Type switch
                {
                    ExpressionType.Map => expReturnType.IsArrayType() ? expReturnType : typeof(JsonArray),
                    ExpressionType.Reduce => callMethodReturn,
                    ExpressionType.First => callArgTypes[arrayIndex],
                    ExpressionType.Last => callArgTypes[arrayIndex],
                    ExpressionType.Filter => expReturnType.IsArrayType() ? expReturnType : typeof(JsonArray),
                    _ => throw new ArgumentOutOfRangeException()
                });
                ParameterExpression start = Expression.Parameter(typeof(int), "_start");
                ParameterExpression stop = Expression.Parameter(typeof(int), "_stop");
                LabelTarget forLabel = Expression.Label(typeof(int));
                //ParameterExpression array = Expression.Parameter(jarray.Type.IsArrayType() ? jarray.Type : typeof(JsonNode[]), "_array");
                ParameterExpression final = Expression.Parameter(resExp.Type, "_final");
                    
                // Convert the call argument
                if (jarray.Type.IsSZArray)
                {
                    // array[start++]
                    innerCallArgs[arrayIndex] = Expression.ArrayIndex(jarray, exp.Type == ExpressionType.Last ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start));
                }
                else
                {
                    // array.get_item(start++)
                    innerCallArgs[arrayIndex] = Expression.MakeIndex(jarray, jarray.Type.GetProperty("Item", new[] { typeof(int) })!, new[] { exp.Type == ExpressionType.Last ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start) });
                }

                // Conversion
                Type ctype = callMethod.GetParameters()[arrayIndex].ParameterType;
                innerCallArgs[arrayIndex] = ConvertExp(ctype, innerCallArgs[arrayIndex]);
                callArgTypes[arrayIndex] = innerCallArgs[arrayIndex].Type;

                // Generate call body
                Delegate innerCall;
                Expression arrayLen = jarray.Type.IsSZArray ? Expression.ArrayLength(jarray) : Expression.Property(jarray, "Count");

                switch (exp.Type)
                {
                    // Map the element
                    case ExpressionType.Map:
                        {
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                new[] { resExp, start, stop, final },
                                Expression.Assign(resExp, Expression.New(resExp.Type)),
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        resExp.Type.IsArrayType() 
                                            ? callMethodReturn.IsArrayType()
                                                ? Expression.Call(resExp, resExp.Type.GetMethod("AddRange", new[] { typeof(IEnumerable<>).MakeGenericType(expReturnType) })!, GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs))
                                                : Expression.Call(resExp, resExp.Type.GetMethod("Add")!, GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs))
                                            : callMethodReturn == typeof(JsonArray)
                                                ? Expression.Call(resExp, typeof(Extension).GetMethod(nameof(Extension.AddRange))!, GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs))
                                                : Expression.Call(resExp, typeof(JsonArray).GetMethod(nameof(JsonArray.Add), new[] { typeof(object) })!, Expression.Convert(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs), typeof(object))),
                                        Expression.Break(forLabel, stop)
                                    ),
                                    forLabel
                                ),
                                Expression.Assign(final, resExp)
                            ));
                            break;
                        }

                    // Consume the elements
                    case ExpressionType.Reduce:
                        {
                            // Buld the init
                            int sumIndex = useContext > 0 ? (arrayIndex == 1 ? 2 : 1) : (arrayIndex == 1 ? 0 : 1);

                            // init ??= array.Length > 0 ? array[start++] : default;
                            Expression init = innerCallArgs[sumIndex] ?? Expression.Condition(
                                Expression.GreaterThan(arrayLen, Expression.Constant(0)),
                                innerCallArgs[arrayIndex],
                                callMethodReturn == typeof(JsonObject) ? Expression.New(typeof(JsonObject)) : callMethodReturn == typeof(JsonArray) ? Expression.New(typeof(JsonArray)) : Expression.Default(callMethodReturn)
                            );

                            // Replace the sum exp
                            innerCallArgs[sumIndex] = resExp;

                            // Complie
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                new[] { resExp, start, stop, final },
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Assign(resExp, init),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        Expression.Assign(resExp, GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs)),
                                        Expression.Break(forLabel, stop)
                                    ),
                                    forLabel
                                ),
                                Expression.Assign(final, resExp)
                            ));
                            break;
                        }

                    // First match
                    case ExpressionType.First:
                        {
                            // Replace the call args
                            Expression temp = innerCallArgs[arrayIndex];
                            innerCallArgs[arrayIndex] = resExp;

                            // New init parameter
                            ParameterExpression init = Expression.Parameter(resExp.Type, "_init");

                            // Complie
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                new[] { resExp, start, stop, init, final },
                                 Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Assign(init, resExp.Type == typeof(JsonObject) ? Expression.New(typeof(JsonObject)) : resExp.Type == typeof(JsonArray) ? Expression.New(typeof(JsonArray)) : Expression.Default(callArgTypes[arrayIndex])),
                                Expression.Assign(resExp, init),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        Expression.Block(new List<Expression>()
                                        {
                                            Expression.Assign(resExp, temp),
                                            Expression.IfThenElse(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs), Expression.Break(forLabel, stop), Expression.Assign(resExp, init))
                                        }),
                                        Expression.Break(forLabel, stop)
                                    ),
                                    forLabel
                                ),
                                Expression.Assign(final, resExp)
                            ));
                            break;
                        }

                    // Last match
                    case ExpressionType.Last:
                        {
                            // Replace the call args
                            Expression temp = innerCallArgs[arrayIndex];
                            innerCallArgs[arrayIndex] = resExp;

                            // New init parameter
                            ParameterExpression init = Expression.Parameter(resExp.Type, "_init");

                            // Complie
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                new[] { resExp, start, stop, init, final },
                                Expression.Assign(stop, Expression.Constant(0, typeof(int))),
                                Expression.Assign(start, arrayLen),
                                Expression.Assign(init, resExp.Type == typeof(JsonObject) ? Expression.New(typeof(JsonObject)) : resExp.Type == typeof(JsonArray) ? Expression.New(typeof(JsonArray)) : Expression.Default(callArgTypes[arrayIndex])),
                                Expression.Assign(resExp, init),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.GreaterThan(start, stop),
                                        Expression.Block(new List<Expression>()
                                        {
                                            Expression.Assign(resExp, temp),
                                            Expression.IfThenElse(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs), Expression.Break(forLabel, stop), Expression.Assign(resExp, init))
                                        }),
                                        Expression.Break(forLabel, stop)
                                    ),
                                    forLabel
                                ),
                                Expression.Assign(final, resExp)
                            ));
                            break;
                        }

                    // Filter the elements
                    case ExpressionType.Filter:
                        {
                            Expression temp = innerCallArgs[arrayIndex];
                            ParameterExpression curr = Expression.Parameter(temp.Type, "_curr");
                            innerCallArgs[arrayIndex] = curr;

                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                new[] { resExp, start, stop, final, curr },
                                Expression.Assign(resExp, Expression.New(resExp.Type)),
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        Expression.Block(new List<Expression>()
                                        {
                                            Expression.Assign(curr, temp),
                                            Expression.IfThen(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs),
                                                resExp.Type.IsArrayType()
                                                    ? Expression.Call(resExp, resExp.Type.GetMethod("Add")!, curr)
                                                    : Expression.Call(resExp, typeof(JsonArray).GetMethod(nameof(JsonArray.Add), new[] { typeof(object) })!, Expression.Convert(curr, typeof(object)))
                                            )
                                        }),
                                        Expression.Break(forLabel, stop)
                                    ),
                                    forLabel
                                ),
                                Expression.Assign(final, resExp)
                            ));
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Gets the call
                return GenDynamicCallExp(resExp.Type, innerCall, callArgs);
            }
            // Generate the result
            case StructResultExpNode strt:
            {
                // Only one struct result can exist
                ParameterExpression resultVar = Expression.Parameter(typeof(JsonObject));
                expMap.Add("_retobject", resultVar);
                blocks.Add(Expression.Assign(resultVar, Expression.New(typeof(JsonObject).GetConstructor(System.Type.EmptyTypes)!)));
                MethodInfo objectAdd = typeof(JsonObject).GetMethod(nameof(JsonObject.Add), new[] { typeof(string), typeof(JsonNode) })!;

                // Build the JsonObject result
                foreach (FunctionNodeExpTree? leafNode in strt.LeafNodes)
                {
                    string name;
                    Expression memberExp;
                    switch (leafNode)
                    {
                        case FunctionNodeArgument leafArg:
                            name = leafArg.Name;
                            memberExp = paramMap[name];
                            break;
                        case FunctionNodeExpression leafExp:
                            name = leafExp.Name;
                            memberExp = expMap[name];
                            break;
                        default:
                            continue; // won't hit
                    }

                    // Gets the struct member
                    var fld = ((StructNode?)strt.TypeNode)?.Fields?.FirstOrDefault(p => p.Name == name);
                    if (fld == null) continue;
                    Type fldType = fld.TypeNode!.ToCSharpType(!fld.Require);
                    memberExp = ConvertExp(fldType, memberExp);

                    // Build the exp
                    if (memberExp.Type.IsNullable())
                    {
                        blocks.Add(Expression.IfThen(
                            Expression.Property(memberExp, memberExp.Type.GetProperty("HasValue")!),
                            Expression.Call(resultVar, objectAdd, new Expression[] { Expression.Constant(name, typeof(string)), Expression.New(typeof(JsonValue).GetConstructor(new[] { memberExp.Type.GetNotNullType() })!, Expression.Property(memberExp, memberExp.Type.GetProperty("Value")!)) })
                        ));
                    }
                    else if (memberExp.Type == typeof(JsonArray) || memberExp.Type == typeof(JsonObject))
                    {
                        blocks.Add(Expression.IfThen(
                            Expression.NotEqual(memberExp, Expression.Constant(null)),
                            // ReSharper disable once RedundantExplicitArrayCreation
                            Expression.Call(resultVar, objectAdd, new Expression[] { Expression.Constant(name, typeof(string)), memberExp })
                        ));
                    }
                    else
                    {
                        blocks.Add(Expression.Call(resultVar, objectAdd, new Expression[] { Expression.Constant(name, typeof(string)), Expression.New(typeof(JsonValue).GetConstructor(new[] { memberExp.Type.GetNotNullType() })!, memberExp) }));
                    }
                }
                return resultVar;
            }
        }
        // won't hit
        return Expression.Empty();
    }

    // Convert expression
    static Expression ConvertExp(Type ctype, Expression exp)
    {
        if (ctype == exp.Type) return exp;
        if (ctype.IsAssignableFrom(exp.Type)) return Expression.Convert(exp, ctype);

        Expression notNullExp = exp.Type.IsNullable() ? Expression.Call(exp, exp.Type.GetMethod("GetValueOrDefault", System.Type.EmptyTypes)!) : exp;
        Expression? resExp = null;
        Type rctype = ctype.GetNotNullType();

        switch (System.Type.GetTypeCode(rctype))
        {
            case TypeCode.Boolean:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(bool)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToBoolean), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Char:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(char)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToChar), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.SByte:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(sbyte)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSByte), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Byte:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(byte)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToByte), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Int16:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(Int16)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt16), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.UInt16:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(UInt16)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt16), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Int32:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(Int32)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt32), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.UInt32:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(UInt32)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt32), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Int64:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(Int64)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt64), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.UInt64:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(UInt64)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt64), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Single:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(Single)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSingle), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Double:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(double)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDouble), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.Decimal:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(decimal)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDecimal), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.DateTime:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(DateTime)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDateTime), [notNullExp.Type])!, notNullExp);
                break;
            case TypeCode.String:
                resExp = notNullExp.Type == typeof(JsonValue)
                    ? Expression.Call(notNullExp, typeof(JsonValue).GetMethod(nameof(JsonValue.GetValue), System.Type.EmptyTypes)!.MakeGenericMethod(typeof(string)))
                    : Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToString), [notNullExp.Type])!, notNullExp);
                break;
        }

        // for complex types
        if (resExp == null)
        {
            if ((rctype == typeof(JsonObject) || rctype == typeof(JsonArray)) && notNullExp.Type == typeof(JsonNode))
            {
                resExp = Expression.Convert(notNullExp, rctype);
            }
            else
            {
                MethodInfo method = typeof(Extension).GetMethod(nameof(Extension.TryConvert), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
                resExp = Expression.Convert(Expression.Call(null, method, Expression.Constant(ctype), notNullExp), ctype);
            }
        }

        // nullable result
        return rctype == ctype
            ? resExp
            : Expression.Condition(Expression.NotEqual(exp, Expression.Constant(null, exp.Type)), resExp, Expression.Constant(null, ctype));
    }

    // Gen method call
    static Expression GenMethodCallExp(SchemaFuncInfo callFuncInfo, MethodInfo callMethod, Expression[] callArgs)
    {
        // Call the method
        if ((callFuncInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC)
        {
            // Gets the task result
            MethodCallExpression callExp = Expression.Call(null, callMethod, callArgs);
            callExp = Expression.Call(callExp, callExp.Type.GetMethod(nameof(Task.GetAwaiter), System.Type.EmptyTypes)!);
            return Expression.Call(callExp, callExp.Type.GetMethod(nameof(TaskAwaiter.GetResult), System.Type.EmptyTypes)!);
        }
        else if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            return callFuncInfo.Name switch
            {
                // system.arth
                $"{NS_SYSTEM_MATH}.e" => Expression.Constant(Math.E),
                $"{NS_SYSTEM_MATH}.pi" => Expression.Constant(Math.PI),
                $"{NS_SYSTEM_MATH}.add" => Expression.Add(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.subtract" => Expression.Subtract(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.multiply" => Expression.Multiply(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.divide" => Expression.Divide(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.modulo" => Expression.Modulo(callArgs[0], callArgs[1]),
                
                // system.conv
                $"{NS_SYSTEM_CONV}.assign" => callArgs[0],
                $"{NS_SYSTEM_CONV}.null" => Expression.Constant(null, callMethod.ReturnType.GetNullableType()),

                // system.logic
                $"{NS_SYSTEM_LOGIC}.isnull" => Expression.Call(null, callMethod, Expression.Convert(callArgs[0], typeof(object))),
                $"{NS_SYSTEM_LOGIC}.cond" => Expression.Condition(callArgs[0], callArgs[1], callArgs[2]),
                $"{NS_SYSTEM_LOGIC}.lessthan" => Expression.LessThan(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.lessequal" => Expression.LessThanOrEqual(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.equal" => Expression.Equal(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.notequal" => Expression.NotEqual(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.greatethan" => Expression.GreaterThan(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.greateequal" => Expression.GreaterThanOrEqual(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.andalso" => Expression.AndAlso(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.orelse" => Expression.OrElse(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.not" => Expression.Not(callArgs[0]),
                $"{NS_SYSTEM_LOGIC}.between" => Expression.AndAlso( // (value, min, max, includeMin, includeMax)
                    Expression.Condition(callArgs[3], Expression.GreaterThanOrEqual(callArgs[0], callArgs[1]), Expression.GreaterThan(callArgs[0], callArgs[1])),
                    Expression.Condition(callArgs[4], Expression.LessThanOrEqual(callArgs[0], callArgs[2]), Expression.LessThan(callArgs[0], callArgs[2]))
                    ),

                // default
                _ => Expression.Call(null, callMethod, callArgs)
            };
        }
        else
        {
            return GenDynamicCallExp(callMethod.ReturnType, callFuncInfo.DynamicMethod!, callArgs);
        }
    }

    // Gen dynamic method call
    static Expression GenDynamicCallExp(Type retType, Delegate method, Expression[] callArgs)
    {
        // call the dynamic method, skip the mysql expression
        MethodInfo callDynamicMethod = GetCallDynamicFunc(retType, callArgs.Select(p => p.Type).ToArray());
        Expression[] newCallArgs = new Expression[callArgs.Length + 1];
        newCallArgs[0] = Expression.Constant(method);
        for (int i = 0; i < callArgs.Length; i++)
            newCallArgs[i + 1] = callArgs[i];
        return Expression.Call(null, callDynamicMethod, newCallArgs);
    }

    #endregion
    
    #region Helper

    // Complie lambda to method
    static T ComplieDynamicMethod<T>(Expression block, params ParameterExpression[] inputs)
        => Expression.Lambda<T>(block, inputs).Compile();


    #region CallDynamicFunc

    // Call dynamic function
    static TR? CallDynamicFunc1<TR, T1>(Delegate method, T1 arg1)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc2<TR, T1, T2>(Delegate method, T1 arg1, T2 arg2)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc3<TR, T1, T2, T3>(Delegate method, T1 arg1, T2 arg2, T3 arg3)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc4<TR, T1, T2, T3, T4>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc5<TR, T1, T2, T3, T4, T5>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc6<TR, T1, T2, T3, T4, T5, T6>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc7<TR, T1, T2, T3, T4, T5, T6, T7>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] {arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] {arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] {arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!, arg16! });
        return result == null ? default : (TR)result;
    }
    static TR? CallDynamicFunc17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16, T17 arg17)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!, arg16!, arg17! });
        return result == null ? default : (TR)result;
    }
    #endregion

    #region Call Server Func

    static TR? GetResult<TR>(JsonNode? token)
    {
        Type tr = typeof(TR);
        bool isNullable = tr.IsSubclassOfGenericType(typeof(Nullable<>));
        tr = isNullable ? tr.GetGenericArguments()[0] : tr;
        if (tr == typeof(JsonArray))
        {
            return token is JsonArray arr ? (TR)(object)arr : isNullable ? (TR?)(object?)null : default;
        }
        else if (tr == typeof(JsonObject))
        {
            return token is JsonObject obj ? (TR) (object) obj : isNullable ? (TR?)(object?)null : default;
        }
        else if (token is JsonValue val)
        {
            return tr == typeof(JsonValue) ? (TR)(object)val : val.GetValue<TR>();
        }
        return isNullable ? (TR?)(object?)null : default;
    }
    
    /// <summary>
    /// Call the data dict function with arguments
    /// </summary>
    static async Task<TR?> CallRemoteFunction0<TR>(SchemaContext context, string name, string[] generic) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray(), generic));
    static async Task<TR?> CallRemoteFunction1<TR, T1>(SchemaContext context, string name, string[] generic, T1 v1) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1 }, generic));
    static async Task<TR?> CallRemoteFunction2<TR, T1, T2>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2 }, generic));
    static async Task<TR?> CallRemoteFunction3<TR, T1, T2, T3>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3 }, generic));
    static async Task<TR?> CallRemoteFunction4<TR, T1, T2, T3, T4>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4 }, generic));
    static async Task<TR?> CallRemoteFunction5<TR, T1, T2, T3, T4, T5>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5 }, generic));
    static async Task<TR?> CallRemoteFunction6<TR, T1, T2, T3, T4, T5, T6>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6 }, generic));
    static async Task<TR?> CallRemoteFunction7<TR, T1, T2, T3, T4, T5, T6, T7>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7 }, generic));
    static async Task<TR?> CallRemoteFunction8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8 }, generic));
    static async Task<TR?> CallRemoteFunction9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9 }, generic));
    static async Task<TR?> CallRemoteFunction10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10 }, generic));
    static async Task<TR?> CallRemoteFunction11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11 }, generic));
    static async Task<TR?> CallRemoteFunction12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12 }, generic));
    static async Task<TR?> CallRemoteFunction13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13 }, generic));
    static async Task<TR?> CallRemoteFunction14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14 }, generic));
    static async Task<TR?> CallRemoteFunction15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15 }, generic));
    static async Task<TR?> CallRemoteFunction16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16 }, generic));
    static async Task<TR?> CallRemoteFunction17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16, T17 v17) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16, v17 }, generic));
    
    #endregion

    /// <summary>
    /// Gets the info from data dict func
    /// </summary>
    internal SchemaFuncInfo? GetSchemaFuncInfo()
    {
        if (FuncInfo != null) return FuncInfo.Method != null ? FuncInfo : null;

        // Check is static
        if (StaticMethodMap.TryGetValue(Name, out SchemaFuncInfo? result) && (result.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result.FunctionNode = this;
            FuncInfo = result;
            return result;
        }

        // Compile
        FuncInfo = CompileFunction();
        return FuncInfo;
    }

    // Gets the call dynamic func
    static MethodInfo GetCallDynamicFunc(Type ret, params Type[] inputs)
    {
        MethodInfo method = typeof(FunctionNode).GetMethod($"CallDynamicFunc{inputs.Length}", BindingFlags.Static | BindingFlags.NonPublic)!;
        return method!.MakeGenericMethod(inputs.Prepend(ret).ToArray());
    }
    
    #endregion
    
    #region Utility

    void ResizeGeneric(int count)
    {
        if (Generic.Length >= count) return;
        AnySchemaNode?[] generic = new AnySchemaNode?[count];
        for(int i = 0; i < Math.Min(count, Generic.Length); i++)
            generic[i] = Generic[i];
        Generic = generic;
    }

    // staitc mappings
    private static readonly ConcurrentDictionary<string, SchemaFuncInfo> StaticMethodMap = new();
    private static readonly ConcurrentDictionary<string, MethodInfo> CallConvertNullableExp = new();

    #endregion

    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(FunctionNode? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Func = new FunctionSchema
            {
                Return = schema.Return,
                Args = schema.Args.Select(a => new FunctionArgumentInfo
                {
                    Name = a.Name,
                    Type = a.Type,
                    Nullable = a.Nullable
                }).ToArray(),
                Exps = schema.Exps.Select(e => new FunctionExpression
                {
                    Name = e.Name,
                    Func = e.Func,
                    Type = e.Type ?? ExpressionType.Call,
                    Return = e.Return,
                    Args = e.Args
                }).ToArray(),
                Generic = schema.Generic.Where(g => g is not null).Select(g => g!.Name).ToArray(),
                Server = schema.Server,
                Nocache = schema.Nocache,
            }
        };
    }
    
    #endregion
}

/// <summary>
/// The expression tree
/// </summary>
public class FunctionNodeExpTree
{
    /// <summary>
    /// The leaf nodes as sub expressions
    /// </summary>
    public FunctionNodeExpTree?[] LeafNodes { get; set; } = [];

    /// <summary>
    /// The type node
    /// </summary>
    public AnySchemaNode? TypeNode { get; set; }

    /// <summary>
    /// The used by count, could be used to improve the dynamic complier
    /// </summary>
    public int Used { get; set; }
}

/// <summary>
/// The function node argument
/// </summary>
public class FunctionNodeArgument : FunctionNodeExpTree
{
    #region Data

    /// <summary>
    /// The argument name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The argument type
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Whether nullable
    /// </summary>
    public bool? Nullable { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;
    
    #endregion
    
    #region Conversion

    public static implicit operator FunctionNodeArgument(FunctionArgumentInfo arg)
    {
        return new FunctionNodeArgument
        {
            Name = arg.Name,
            Type = arg.Type,
            Nullable = arg.Nullable,
        };
    }
    
    #endregion
}

/// <summary>
/// The function node expression
/// </summary>
public class FunctionNodeExpression : FunctionNodeExpTree
{
    #region Data
    
    /// <summary>
    /// The expression name, normally be E1, E2, E3.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The function to be called.
    /// </summary>
    public required string Func { get; set; }

    /// <summary>
    /// The function used to map array elements
    /// </summary>
    public ExpressionType? Type { get; set; } = ExpressionType.Call;

    /// <summary>
    /// The namespace.
    /// </summary>
    public required string Return { get; set; }

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public FunctionCallArgument[] Args { get; set; } = [];

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;

    /// <summary>
    /// The index of the array used for Map/Reduce/First
    /// </summary>
    public int? ArrayIndex { get; set; }

    #endregion

    #region Relationship

    /// <summary>
    /// The function node
    /// </summary>
    public FunctionNode? FuncNode { get; set; }

    #endregion

    #region Conversion

    public static implicit operator FunctionNodeExpression(FunctionExpression exp)
    {
        return new FunctionNodeExpression
        {
            Name = exp.Name,
            Type = exp.Type,
            Return = exp.Return,
            Args = exp.Args,
            Func = exp.Func,
        };
    }

    #endregion
}

/// <summary>
/// The function node expression tree
/// </summary>
public class StructResultExpNode : FunctionNodeExpTree
{
}

/// <summary>
/// The constant expression tree
/// </summary>
public class ConstantExpNode : FunctionNodeExpTree
{
    /// <summary>
    /// The constant value
    /// </summary>
    public required object? Value { get; init; }
}

/// <summary>
/// The data dict func info
/// </summary>
internal class SchemaFuncInfo
{
    /// <summary>
    /// The method name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The method info
    /// </summary>
    public MethodInfo? Method { get; set; }

    /// <summary>
    /// The dynamic method generated by expression
    /// </summary>
    public Delegate? DynamicMethod { get; set; }

    /// <summary>
    /// The function node
    /// </summary>
    public FunctionNode? FunctionNode { get; set; }

    /// <summary>
    ///  The sign of the function
    /// </summary>
    public int Sign { get; set; }

    /// <summary>
    /// The generic info
    /// </summary>
    public SchemaParamTypeInfo[] Generics { get; init; } = [];
    
    /// <summary>
    /// The argument info
    /// </summary>
    public SchemaParamTypeInfo[] Args { get; init; } = [];
    
    /// <summary>
    /// The return info
    /// </summary>
    public required SchemaParamTypeInfo Return { get; init; }

    /// <summary>
    /// The generic instances
    /// </summary>
    public ConcurrentDictionary<string, MethodInfo> GenericMethods { get; } = new();
}


public class GenericTypeNode: AnySchemaNode
{
    /// <summary>
    /// Possible base type
    /// </summary>
    public AnySchemaNode? BaseNode { get; set; }

    /// <summary>
    /// The index in generic array
    /// </summary>
    public int GenericIndex { get; set; }
}
