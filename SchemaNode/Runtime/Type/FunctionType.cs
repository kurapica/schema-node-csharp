using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Schema;
using ExpressionType = SchemaNode.Enum.ExpressionType;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory function schema representation
/// </summary>
public class FunctionType: AnySchemeType
{
    #region Data
    
    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    public string Return { get; protected set; } = string.Empty;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FunctionNodeArgument[] Args { get; protected set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FunctionNodeExpression[] Exps { get; protected set; } = [];

    /// <summary>
    /// The basic type of generic types, provided to T(single generic type),
    /// T1, T2(for multi generic type)
    /// </summary>
    public AnySchemeType?[] Generic { get; protected set; } = [];

    /// <summary>
    /// Call server if server provided
    /// </summary>
    public bool? Server  { get; protected set; }

    /// <summary>
    /// The client should not cache the result
    /// </summary>
    public bool? Nocache  { get; protected set; }
    
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Func;
    
    /// <summary>
    /// Whether the function will be used to construct the object
    /// </summary>
    public bool IsStructConstructor { get; private set; }

    /// <summary>
    /// Whether the function is remote call only
    /// </summary>
    public bool IsRemoteCall { get; private set; }

    /// <summary>
    /// Whether the function require call server
    /// </summary>
    public bool RequireRemoteCall { get; private set; }

    /// <summary>
    /// Whether the function is defined as system, direct call
    /// </summary>
    public bool IsSystemCall { get; private set; }

    /// <summary>
    /// The function info
    /// </summary>
    internal SchemaFuncInfo? FuncInfo { get; set; }
    
    #endregion
    
    #region Ref
    
    /// <summary>
    /// The return type node
    /// </summary>
    public AnySchemeType? ReturnNode { get; internal set; }

    /// <summary>
    /// The root expression trees
    /// </summary>
    public List<FunctionNodeExpTree> ExpTrees { get; protected set; } = [];
    
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
        Generic = func?.Generic != null ? new AnySchemeType?[func.Generic.Length] : [];
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
                    Generic[i] = await context.GetSchemaTypeAsync(name);
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
            ReturnNode = await context.GetSchemaTypeAsync(Return);
            if (ReturnNode is not { IsValueType: true }) Status = SchemaNodeStatus.FunctionWrongReturnType;
        }

        // Generate the exp trees
        bool isOkay = Status == SchemaNodeStatus.Ready;
        if (isOkay) await PreCompileAsync(context);

        // Add usages
        if (Status == SchemaNodeStatus.Ready)
        {
            ReturnNode?.AddRef(this);
            foreach (FunctionNodeArgument arg in Args)
                arg.SchemaType?.AddRef(this);
        }
        else if (isOkay)
        {
            // hacky way to force re-compile
            Injection.ReCompileFuncTypes?.Add(this);
        }
    }
    
    /// <inheritdoc />
    public override void Release()
    {
        ReturnNode?.RemoveRef(this);
        ReturnNode = null;
        foreach (FunctionNodeArgument arg in Args)
        {
            arg.SchemaType?.RemoveRef(this);
            arg.SchemaType = null;
        }

        foreach (FunctionNodeExpression exp in Exps)
        {
            exp.SchemaType?.RemoveRef(this);
            exp.SchemaType = null;
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
    public override bool CanBeUseAs(AnySchemeType other) => false;

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false) => null;

    /// <inheritdoc />
    public override IEnumerable<AnySchemeType> GetDependNodes()
    {
        if (ReturnNode != null && ReturnNode is not GenericTypeNode)
            yield return ReturnNode;

        foreach (FunctionNodeArgument arg in Args)
        {             
            if (arg.SchemaType != null && arg.SchemaType is not GenericTypeNode)
                yield return arg.SchemaType;
        }

        foreach(FunctionNodeExpression exp in Exps)
        {
            if (exp.SchemaType != null && exp.SchemaType is not GenericTypeNode)
                yield return exp.SchemaType;

            if (exp.FuncNode != null)
                yield return exp.FuncNode;

            if (exp.Args is { Length: > 0 })
            {
                foreach (FuncCallArg callArg in exp.Args)
                {
                    if (callArg.SchemeType != null && callArg.SchemeType is not GenericTypeNode)
                        yield return callArg.SchemeType;
                }
            }
        }
    }

    /// <summary>
    /// Pre-compile the function expression trees
    /// </summary>
    public async Task PreCompileAsync(SchemaContext context)
    {
        if (ExpTrees.Count > 0 || Status != SchemaNodeStatus.Ready) return; // already pre-compiled
        
        // Build the exp trees
        (List<FunctionNodeExpTree> trees, string? error) = await BuildExpTrees(context);
        if (!string.IsNullOrEmpty(error))
        {
            Status = Status == SchemaNodeStatus.Ready ? SchemaNodeStatus.FunctionExpsHasCompileError : Status;
            return;
        }
        
        // Record the exp trees
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
        
        // Add ref
        foreach (FunctionNodeExpression exp in Exps)
        {
            exp.SchemaType?.AddRef(this);
            exp.FuncNode?.AddRef(this);
        }
    }
    
    #endregion

    #region Exp Tree
    
    /// <summary>
    /// Build the expression tree based on the arguments and expressions
    /// </summary>
    async Task<(List<FunctionNodeExpTree>, string? error)> BuildExpTrees(SchemaContext context)
    {
        List<FunctionNodeExpTree> trees = [];
        Dictionary<string, FunctionNodeExpTree> treeMap = [];
        
        // Validate the arguments and reset the states
        int argIdx = 0;
        foreach(FunctionNodeArgument arg in Args)
        {
            arg.Index = argIdx++;
            arg.Used = 0;
            arg.Status = null;
            
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
                    int index = arg.Type.Length > 1 && int.TryParse(arg.Type[1..], out int idx) ? idx : 1;
                    ResizeGeneric(index);
                    arg.SchemaType = new GenericTypeNode
                    {
                        GenericIndex = index,
                        BaseNode = Generic[index - 1]
                    };
                }
            }
            else
            {
                AnySchemeType? node = await context.GetSchemaTypeAsync(arg.Type);
                if (node is not { IsValueType: true })
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    return (trees, TYPE_FUNC_ARG_TYPE_NOT_VALID);
                }
                arg.SchemaType = node;
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
        for (int k = 0; k < Exps.Length; k++)
        {
            FunctionNodeExpression exp = Exps[k];
            
            #region input & output check

            int arrayArg = -1; // the array argument index
            AnySchemeType? arrayRequireEle = null;
            bool isMapReduce = exp.Type != ExpressionType.Call;

            // reset
            exp.Used = 0;
            exp.Status = null;

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
            if (await context.GetSchemaTypeAsync(exp.Func) is not FunctionType funcNode)
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
                case ExpressionType.First when funcNode.ReturnNode is not ScalarType { IsBool: true }:
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncForFirst;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncForFirst;
                    return (trees, TYPE_FUNC_CANT_USE_AS_FIRST);

                // Check last function
                case ExpressionType.Last when funcNode.ReturnNode is not ScalarType { IsBool: true }:
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncForLast;
                    Status = SchemaNodeStatus.FunctionExpWrongFuncForLast;
                    return (trees, TYPE_FUNC_CANT_USE_AS_LAST);

                // Check filter function
                case ExpressionType.Filter when funcNode.ReturnNode is not ScalarType { IsBool: true }:
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
            AnySchemeType?[] genericTypes = funcNode.Generic.ToArray();
            
            // Gets the return type
            if (string.IsNullOrWhiteSpace(exp.Return))
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                Status = SchemaNodeStatus.FunctionWrongReturnType;
                return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
            }
            else
            {
                AnySchemeType? node = await context.GetSchemaTypeAsync(exp.Return);
                if (node is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                    Status = SchemaNodeStatus.FunctionWrongReturnType;
                    return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                }
                exp.SchemaType = node;

                // validate the exp type & return type & func type
                bool skipMatch = false;
                if (exp.Type is ExpressionType.Map or ExpressionType.Filter)
                {
                    if (node is not ArrayType arr || arr.ElementSchemaType is null)
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        Status = SchemaNodeStatus.FunctionWrongReturnType;
                        return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    node = arr.ElementSchemaType;
                    if (exp.Type is ExpressionType.Filter)
                    {
                        arrayRequireEle = node;
                        skipMatch = true;
                    }
                }
                else if (exp.Type is ExpressionType.First or ExpressionType.Last)
                {
                    if (node is ArrayType)
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
                               && funcNode.ReturnNode is ArrayType { ElementSchemaType: not null } arr
                               && arr.ElementSchemaType.CanBeUseAs(node)))
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        Status = SchemaNodeStatus.FunctionWrongReturnType;
                        return (trees, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                }
            }

            #endregion

            #region call arguments check
            
            // exp leaf
            exp.LeafNodes = new FunctionNodeExpTree[funcNode.Args.Length];
            void setLeafNodes(int index, FunctionNodeExpTree node)
            {
                index = Math.Min(index, exp.LeafNodes.Length - 1);
                if (!(funcNode.Args[index].Params ?? false))
                {
                    exp.LeafNodes[index] = node;
                }
                else if (exp.LeafNodes[index] is ParamsExpNode paramsExp)
                {
                    node.Used++;
                    paramsExp.LeafNodes = paramsExp.LeafNodes.Append(node).ToArray();
                }
                else
                {
                    node.Used++;
                    exp.LeafNodes[index] = new ParamsExpNode
                    {
                        LeafNodes = [node]
                    };
                }
            }

            // Check function call arguments and gets the return type
            // Also bind the type node to the expression call arguments, before compile
            if (funcNode.Args is { Length: > 0 })
            {
                // fill all exp args for checking
                if (exp.Args.Length < funcNode.Args.Length)
                    exp.Args = exp.Args.Concat(Enumerable.Range(0, funcNode.Args.Length - exp.Args.Length)
                        .Select(_ => new FuncCallArg())).ToArray();
                
                // check exp use variables first, they provide type infos
                for (int i = 0; i < exp.Args.Length; i++)
                {
                    FuncCallArg callArg = exp.Args[i];
                    if (string.IsNullOrWhiteSpace(callArg.Name)) continue;

                    FunctionNodeArgument funcArg = funcNode.Args.ElementAtOrDefault(i) ?? funcNode.Args.Last();
                    AnySchemeType? funcArgType = funcArg.SchemaType;
                    if (funcArgType is GenericTypeNode gn) funcArgType = genericTypes[gn.GenericIndex - 1];
                    
                    // Gets the arg/exp
                    string[] access = callArg.Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    if (treeMap.TryGetValue(access[0], out FunctionNodeExpTree? value))
                    {
                        AnySchemeType? argTypeNode;
                        
                        // Convert the field access
                        if (access.Length > 1)
                        {
                            AnySchemeType? type = value.SchemaType;
                            bool isArrayAccess = type is ArrayType;
                            if (isArrayAccess)
                                type = (type as ArrayType)!.ElementSchemaType;
                            
                            for (int j = 1; j < access.Length; j++)
                            {
                                if (type is StructType @struct && @struct.Fields.FirstOrDefault(f => f.Name.Equals(access[j], StringComparison.OrdinalIgnoreCase)) is { } config)
                                {
                                    type = config.SchemeType;
                                }
                                else
                                {
                                    // Error
                                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                    Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                    return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST);
                                }
                            }
                            value.Used++;
                            value = new FieldAccessExpNode
                            {
                                LeafNodes = [value],
                                FieldName = string.Join(".", access.Skip(1)),
                                SchemaType = isArrayAccess ? await context.GetArraySchemaTypeAsync(type) : type
                            };
                        }
                        
                        switch (value)
                        {
                            case FunctionNodeArgument rArg:
                                setLeafNodes(i, rArg); // Add to leaf
                                argTypeNode = rArg.SchemaType;
                                if (argTypeNode is GenericTypeNode generic)
                                {
                                    argTypeNode = genericTypes[generic.GenericIndex - 1];
                                    if (argTypeNode == null && !string.IsNullOrEmpty(callArg.Type))
                                    {
                                        AnySchemeType? givenType = await context.GetSchemaTypeAsync(callArg.Type);
                                        if (givenType != null)
                                        {
                                            genericTypes[generic.GenericIndex - 1] = givenType;
                                            argTypeNode = givenType;
                                        }
                                    }
                                }
                                if (argTypeNode == null)
                                {
                                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                    Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                    return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST); // For safe
                                }
                                break;
                            
                            case FunctionNodeExpression:
                            case FieldAccessExpNode:
                                setLeafNodes(i, value); // Add to leaf
                                argTypeNode = value.SchemaType; // always exist
                                break;
                            
                            default:
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST); // For safe
                        }
                        
                        // Check if used as array type
                        if (isMapReduce && arrayArg < 0 && 
                            argTypeNode is ArrayType { ElementSchemaType: not null } array && 
                            funcArgType is not ArrayType && 
                            (funcArgType is null || array.ElementSchemaType.CanBeUseAs(funcArgType)) &&
                            (arrayRequireEle is null || array.ElementSchemaType.CanBeUseAs(arrayRequireEle) || 
                             (value is FieldAccessExpNode && 
                             value.LeafNodes.ElementAtOrDefault(0)?.SchemaType is ArrayType { ElementSchemaType: not null } arr && 
                             arr.ElementSchemaType.CanBeUseAs(arrayRequireEle))))
                        {
                            arrayArg = i;
                            argTypeNode = array.ElementSchemaType;
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
                        else if (funcArg.SchemaType is GenericTypeNode g)
                        {
                            genericTypes[g.GenericIndex - 1] = argTypeNode;
                        }
                        
                        // save the type
                        callArg.SchemeType = argTypeNode;
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        return (trees, TYPE_FUNC_CALL_ARG_NOT_EXIST);
                    }
                }
                
                // Check constant value arguments
                for (int i = 0; i < exp.Args.Length; i++)
                {
                    FuncCallArg callArg = exp.Args[i];
                    if (!string.IsNullOrWhiteSpace(callArg.Name)) continue;

                    FunctionNodeArgument funcArg = funcNode.Args.ElementAtOrDefault(i) ?? funcNode.Args.Last();
                    AnySchemeType? funcArgType = funcArg.SchemaType;
                    if (funcArgType is GenericTypeNode g)
                        funcArgType = genericTypes[g.GenericIndex - 1];
                    
                    // check given type
                    if (funcArgType == null && !string.IsNullOrEmpty(callArg.Type))
                    {
                        AnySchemeType? givenType = await context.GetSchemaTypeAsync(callArg.Type);
                        if (givenType != null)
                        {
                            if (funcArg.SchemaType is GenericTypeNode generic)
                                genericTypes[generic.GenericIndex - 1] = givenType;
                            funcArgType = givenType;
                        }
                    }

                    // for safe, couldn't be
                    if (funcArgType == null)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        return (trees, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
                    }
                    
                    if ((funcArg.Params ?? false) && funcArgType is ArrayType arr)
                        funcArgType = arr.ElementSchemaType;
                    
                    // save type
                    callArg.SchemeType = funcArgType;
                    
                    // Check nullable
                    if (callArg.Value == null || callArg.Value.IsEmpty())
                    {
                        if ((funcNode.Args[i].Nullable ?? true) || (exp.Type == ExpressionType.Reduce && i == 1)) // Nullable or Reduce
                        {
                            setLeafNodes(i, new ConstantExpNode
                            {
                                Value = null,
                                SchemaType = funcArgType
                            });
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
                        (AnySchemaNode? r, JsonNode? e) = await funcArgType.ValidateValueAsync(context, callArg.Value);
                        if (e != null && !e.IsEmpty())
                        {
                            exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            return (trees, TYPE_FUNC_EXP_CALL_CONSTANT_NOT_VALID);
                        }                        
                        callArg.Value = r?.ToJson();

                        // Add to leaf
                        setLeafNodes(i, new ConstantExpNode
                        {
                            Value = r,
                            SchemaType = funcArgType
                        });
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

            // App data source access check
            exp = await VisitAppDataSourceAccessExp(context, exp);
            Exps[k] = exp;
            if (exp.Status != null && exp.Status != SchemaNodeStatus.Ready)
            {
                Status = exp.Status.Value;
                return (trees, TYPE_FUNC_EXP_ARGS_NOT_VALID);
            }
            
            #endregion

            // Add to the tree
            treeMap[exp.Name] = exp;
        }

        #region Struct Build
        
        // Validate the function return
        StructResultExpNode? structResultNode = null;
        if (!Exps.Last().SchemaType!.CanBeUseAs(ReturnNode!))
        {
            if (ReturnNode is StructType { Fields: { Length: > 0 } } @struct)
            {
                // Check the struct fields
                List<FunctionNodeExpTree> leafNodes = []; // leaf nodes
                foreach (StructFieldConfig field in @struct.Fields.Where(f => !(f.DisplayOnly ?? false)))
                {
                    if (treeMap.TryGetValue(field.Name, out FunctionNodeExpTree? leaf))
                    {
                        if (leaf.SchemaType is null || !leaf.SchemaType.CanBeUseAs(field.SchemeType!))
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
                    SchemaType = @struct,
                    LeafNodes = leafNodes.ToArray()
                };
            }
            else
            {
                return (trees, TYPE_FUNC_RETURN_NOT_VALID);
            }
        }

        #endregion
        
        // Reduce the trees, only the result and non-one-time-used exp will be added to the tree
        trees = Exps.Where(e => e.Used is > 1 or 0)
            .Select(p => (FunctionNodeExpTree)p).ToList();
        if (structResultNode != null) trees.Add(structResultNode);
        
        // Pre-compile done
        return (trees, null);
    }
    
    /// <summary>
    /// Visit app data source access expression and return the replacement
    /// </summary>
    async Task<FunctionNodeExpression> VisitAppDataSourceAccessExp(SchemaContext context, FunctionNodeExpression exp)
    {
        // app data source access
        if (exp.Func.Equals(GET_DATA_SOURCE, StringComparison.OrdinalIgnoreCase))
        {
            string? app = exp.LeafNodes.ElementAtOrDefault(0) is ConstantExpNode appExp ? appExp.Value?.ToValue<string>() :  null;
            string? field = exp.LeafNodes.ElementAtOrDefault(1) is ConstantExpNode fldExp ? fldExp.Value?.ToValue<string>() :  null;

            // App & Field must be provided
            if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(field))
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                return exp;
            }
            
            AppType? appType = await context.GetAppTypeAsync(app);
            AppFieldType? appField = appType?.GetField(field);
            if (appField is not { SchemaType: ArrayType { ElementSchemaType: StructType structType } })
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                return exp;
            }

            AppDataSourceAccessExpNode accessExp = new(exp)
            {
                App = app,
                Field = field,
                Target = exp.LeafNodes.ElementAtOrDefault(2),
                StructType = structType,
                Result = AppDataSourceAccessResult.List
            };

            // replace for access
            exp = accessExp;
        }
        
        // record action or process the query
        else if (exp.LeafNodes.ElementAtOrDefault(0) is AppDataSourceAccessExpNode accessExp)
        {
            switch (exp.Type)
            {
                case ExpressionType.Call:
                {
                    switch (exp.FuncNode?.Name ?? exp.Func)
                    {
                        // source.length
                        case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.arrlen)}":
                        {
                            // replace for access
                            exp = new AppDataSourceAccessExpNode(exp, accessExp)
                            {
                                Result = AppDataSourceAccessResult.Count
                            };
                            break;
                        }
                        
                        // source.orderby(field, desc)
                        case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.orderby)}":
                        {
                            string orderField = exp.LeafNodes.ElementAtOrDefault(1) is ConstantExpNode fieldExp ? fieldExp.Value?.ToValue<string>() ?? "" : "";
                            bool descending = exp.LeafNodes.ElementAtOrDefault(2) is ConstantExpNode descExp && descExp.Value?.ToValue<bool>() == true;

                            if (string.IsNullOrEmpty(orderField))
                            {
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                return exp;
                            }
                            
                            // replace for access
                            exp = new AppDataSourceAccessExpNode(exp, accessExp)
                            {
                                Order = new AppSchemaDataOrder(orderField, descending),
                            };
                            break;
                        }
                        
                        // source.skip(n)
                        case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.skip)}":
                        {
                            // replace for access
                            exp = new AppDataSourceAccessExpNode(exp, accessExp)
                            {
                                Skip = exp.LeafNodes.ElementAtOrDefault(1)
                            };
                            break;
                        }
                        
                        // source.take(n)
                        case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.take)}":
                        {
                            // replace for access
                            exp = new AppDataSourceAccessExpNode(exp, accessExp)
                            {
                                Take = exp.LeafNodes.ElementAtOrDefault(1)
                            };
                            break;
                        }
                    }
                    break;
                }
                case ExpressionType.First:
                case ExpressionType.Last:
                case ExpressionType.Filter:
                {
                    // init the exp map
                    Dictionary<FunctionNodeExpTree, AccessExpNode> expMap = [];
                    expMap[accessExp] = new StructAccessExpNode(accessExp.StructType!);
                    // use arg node to block deep visit
                    for (int i = 1; i < exp.LeafNodes.Length; i++)
                    {
                        expMap[exp.LeafNodes[i]!] = exp.LeafNodes[i] is ConstantExpNode constExp
                            ? new ValueAccessExpNode(constExp.SchemaType, constExp.Value)
                            : new ArgNode(
                                exp.LeafNodes[i]?.SchemaType ??
                                throw new NotSupportedException($"The {exp.Func} can't be used as row access filter"),
                                i - 1);
                    }

                    try
                    {
                        AccessExpNode filter = await context.VisitExp(exp, expMap, true);

                        // replace for access
                        exp = new AppDataSourceAccessExpNode(exp, accessExp)
                        {
                            Filter = filter,
                            Result = exp.Type switch
                            {
                                ExpressionType.First => AppDataSourceAccessResult.First,
                                ExpressionType.Last => AppDataSourceAccessResult.Last,
                                _ => AppDataSourceAccessResult.List
                            },
                        };
                    }
                    catch(Exception ex)
                    {
                        context.Logger.LogError("The {Func} can't be used as row access filter: {Error}, will cause the query execution", exp.Func, ex.Message);
                    }
                    break;
                }
            }
        }
        
        // field access
        else if ((exp.LeafNodes.Any(l => l is FieldAccessExpNode fa &&
                  fa.LeafNodes.ElementAtOrDefault(0) is AppDataSourceAccessExpNode)))
        {
            switch (exp.Type)
            {
                case ExpressionType.First:
                case ExpressionType.Last:
                case ExpressionType.Filter:
                {
                    // init the exp map
                    var fieldAccess = exp.LeafNodes.First(l => l is FieldAccessExpNode fa &&
                                                               fa.LeafNodes.ElementAtOrDefault(0) is
                                                                   AppDataSourceAccessExpNode) as FieldAccessExpNode;
                    var fieldAccessExp = fieldAccess!.LeafNodes.ElementAtOrDefault(0) as AppDataSourceAccessExpNode;
                    
                    Dictionary<FunctionNodeExpTree, AccessExpNode> expMap = [];
                    StructAccessExpNode structAccessExpNode = new StructAccessExpNode(fieldAccessExp!.StructType!);
                    expMap[fieldAccessExp] = structAccessExpNode;
                    expMap[fieldAccess] = new FieldAccessAccessExpNode(structAccessExpNode, fieldAccess.FieldName);
                    
                    // use arg node to block deep visit
                    int index = 0;
                    int sourceIndex = 0;
                    for (int i = 0; i < exp.LeafNodes.Length; i++)
                    {
                        if (exp.LeafNodes[i] == fieldAccess) { sourceIndex = i ; continue; };
                        expMap[exp.LeafNodes[i]!] = exp.LeafNodes[i] is ConstantExpNode constExp
                            ? new ValueAccessExpNode(constExp.SchemaType, constExp.Value)
                            : new ArgNode(
                                exp.LeafNodes[i]?.SchemaType ??
                                throw new NotSupportedException($"The {exp.Func} can't be used as row access filter"),
                                index);
                        index++;
                    }

                    try
                    {
                        AccessExpNode filter = await context.VisitExp(exp, expMap, true);

                        // replace for access
                        exp = new AppDataSourceAccessExpNode(exp, fieldAccessExp)
                        {
                            Filter = filter,
                            SourceIndex = sourceIndex,
                            Result = exp.Type switch
                            {
                                ExpressionType.First => AppDataSourceAccessResult.First,
                                ExpressionType.Last => AppDataSourceAccessResult.Last,
                                _ => AppDataSourceAccessResult.List
                            },
                        };
                        exp.LeafNodes[sourceIndex] = fieldAccessExp;
                    }
                    catch(Exception ex)
                    {
                        context.Logger.LogError("The {Func} can't be used as row access filter: {Error}, will cause the query execution", exp.Func, ex.Message);
                    }
                    break;
                }
            }
        }
        
        return exp;
    }
    
    // Clear the function info to be re-complied
    internal void ClearFunctionInfo()
    {
        if (FuncInfo != null && (FuncInfo.Sign & FUNC_SIGN_IMMUTABLE) > 0) return; // Immutable, no need to clear

        FuncInfo = null;
        if (UsedBy == null || UsedBy.IsEmpty) return;
        foreach ((AnySchemeType other, _) in UsedBy)
        {
            if (other is FunctionType func)
                func.ClearFunctionInfo();
        }
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
        SchemaAttribute? funcAttr = method.GetCustomAttribute<SchemaAttribute>();
        if (funcAttr == null) return null;

        int sign = FUNC_SIGN_IMMUTABLE; // The system method won't be changed and already compiled
        if (method.IsGenericMethodDefinition) sign |= FUNC_SIGN_GENERIC;
        
        // Generate the arguments and result type
        ParameterInfo[] parameters = method.GetParameters();
        SchemaParamTypeInfo[] genInfos = method.GetGenericArguments().Select(g => g.GetSchemaTypeInfo(true, ns)!).ToArray(); // The generic type infos

        // The schema context must be the first if used
        if (parameters.Length > 0 && (parameters[0].ParameterType == typeof(SchemaContext) || 
                                      parameters[0].ParameterType.IsSubclassOf(typeof(SchemaContext))))
        {
            sign |= FUNC_SIGN_CONTEXT;
            parameters = parameters.Skip(1).ToArray();
        }

        // Generate func schema
        var name = funcAttr.Name ?? $"{(string.IsNullOrEmpty(ns) ? "" : $"{ns}.")}{method.Name.ToLowerInvariant()}";
        NodeSchema funcSchema = new NodeSchema
        {
            Name = name,
            Type = SchemaType.Func,
            Display = funcAttr.Display?? method.GetSummaryFromXmlDoc() ?? name,
            Func = new FunctionSchema
            {
                Return = string.Empty,
                Args = new FuncArg[parameters.Length],
                Exps = [],
                Nocache = method.GetCustomAttribute<NoCacheAttribute>() != null,
                Server = method.GetCustomAttribute<ServerOnlyAttribute>() != null,
                Generic = genInfos.Select(g => g is { AnyArray: false, Number: true } ? NS_SYSTEM_NUMBER : "").ToArray(),
            }
        };

        // Return type
        SchemaParamTypeInfo? retInfo = method.ReturnType.GetSchemaTypeInfo(true, ns);
        if (retInfo == null) return null;
        if (retInfo.Task) sign |= FUNC_SIGN_ASYNC;
        if (retInfo.Nullable) sign |= FUNC_SIGN_NULLABLE_RET;
        var nullabilityContext = new NullabilityInfoContext();
        var returnNullability = nullabilityContext.Create(method.ReturnParameter);
        if (returnNullability.ReadState == NullabilityState.Nullable)
            sign |= FUNC_SIGN_NULLABLE_RET;

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
        else if (Regex.IsMatch(retInfo.SchemaType, REGEX_GENERIC_TYPE)) // AnySchemaNode
        {
            funcSchema.Func.Return = $"T{genInfos.Length + 1}";
        }
        else
        {
            funcSchema.Func.Return = retInfo.SchemaType;
        }
        
        // Parameter types
        SchemaParamTypeInfo?[] paramInfos = parameters.Select(p => p.ParameterType.GetSchemaTypeInfo(true, ns)).ToArray();
        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo p = parameters[i];
            SchemaParamTypeInfo? pt = paramInfos[i];
            if (pt == null) return null;
            
            FuncArg arg = new ()
            {
                Name = p.Name ?? $"arg{i}",
                Nullable = pt.Nullable || p.HasDefaultValue || 
                    p.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute") != null ||
                    p.IsDefined(typeof(DefaultAttribute), false),
                Default = p.GetCustomAttribute<DefaultAttribute>()?.Value, // not the default value of the parameter
            };
            funcSchema.Func.Args[i] = arg;
            if (arg.Nullable ?? false) pt.Kind |= ParameterTypeKind.Nullable;

            // Params
            if (p.IsDefined(typeof(ParamArrayAttribute), false))
            {
                arg.Params = true;
                arg.Nullable = true;
                pt.Kind |= ParameterTypeKind.Params;
            }

            // Check dynamic type
            SchemaAttribute? schemaTypeAttr = p.GetCustomAttribute<SchemaAttribute>();
            if (schemaTypeAttr != null && !string.IsNullOrWhiteSpace(schemaTypeAttr.Name))
            {
                pt.SchemaType = schemaTypeAttr.Name;
                arg.Type = pt.SchemaType;
            }
            else if (pt.Generic != null)
            {
                if (pt.AnyArray && !(arg.Params ?? false))
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
                return null;
            }
            else
            {
                arg.Type = (arg.Params ?? false) && pt.SchemaType.EndsWith("s") && GetSystemNodeSchema(pt.SchemaType)?.Type == SchemaType.Array 
                    ? pt.SchemaType[..^1] 
                    : pt.SchemaType;
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
    SchemaFuncInfo CompileFunction(SchemaContext context)
    {
        // Only full-filled function can be complied
        if (Status != SchemaNodeStatus.Ready) throw new Exception($"The {Name} can't be compiled because of {Status}");

        SchemaFuncInfo funcInfo = new ()
        {
            Name = Name,
            Sign = FUNC_SIGN_CONTEXT, // always use context for dynamic func
            FunctionNode = this,
            Args = Args.Select(a =>
            {
                var info = a.SchemaType!.GetSchemaTypeInfo()!;
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
            var paramExpMap = new Dictionary<string, Expression>();
            var variableExpMap = new Dictionary<string, ParameterExpression>();
            var paramExps = new ParameterExpression[Args.Length + 1];

            // Build the parameters, no generic type for custom methods
            // Always add SchemaContext as the first parameters for inner call
            paramExps[0] = Expression.Parameter(typeof(SchemaContext)); 
            for (int i = 0; i < Args.Length; i++)
            {
                FunctionNodeArgument arg = Args[i];
                ParameterExpression paramExp = Expression.Parameter(arg.SchemaType?.ToCSharpType(arg.Nullable) 
                    ?? throw new Exception($"The {Name} can't be compiled - expression compile failed"));
                paramExps[i + 1] = paramExp;
                paramExpMap[arg.Name] = paramExp;
            }

            // Expression Tree -> Function Body
            List<Expression> expBlocks = [];
            LabelTarget? returnLabel = ExpTrees.Any(e => e is FunctionNodeExpression exp && RetFunc.Contains(exp.Func, StringComparer.OrdinalIgnoreCase))
                ? Expression.Label(ReturnNode!.ToCSharpType(funcInfo.Return.Nullable)) : null;
            
            int expCount = 0;
            ParameterExpression? finalVar = null;
            foreach (FunctionNodeExpTree exp in ExpTrees)
            {
                Expression result = CompileFunctionNodeExpression(context, paramExps[0], paramExpMap, variableExpMap, expBlocks, exp, returnLabel);
                if (result == null) throw new Exception($"The {Name} can't be compiled - expression compile failed");

                // exp = result
                string expName = exp is FunctionNodeExpression nodeExp ? nodeExp.Name : $"_expResult{++expCount}";
                ParameterExpression expRes = Expression.Parameter(result.Type);
                variableExpMap.Add(expName, expRes);
                
                // Logger @TODO
                //if (exp is FunctionNodeExpression callexp)
                //    expBlocks.Add(Expression.Call(paramExps[0], typeof(SchemaContext).GetMethod(nameof(SchemaContext.LogInformation))!, Expression.Constant($"Calling expression {callexp.Name}")));
                expBlocks.Add(Expression.Assign(expRes, result));
                finalVar = expRes;
            }
            
            if (finalVar == null) throw new Exception($"The {Name} can't be compiled - no expression found");

            // Conversion last type
            Type lastType = ReturnNode?.ToCSharpType() ?? throw new Exception($"The {Name} can't be compiled - return type not valid");
            if (lastType != finalVar.Type)
            {
                var convExp = Expression.Variable(lastType, "_final");
                variableExpMap.Add("_final", convExp);
                expBlocks.Add(Expression.Assign(convExp, ConvertExp(context, lastType, finalVar)));
                finalVar = convExp;
            }

            // Handle return label if existed
            if (returnLabel != null)
            {
                expBlocks.Add(Expression.Return(returnLabel, finalVar));
                expBlocks.Add(Expression.Label(returnLabel, Expression.Default(returnLabel.Type)));
            }

            // Build block
            BlockExpression blockExpr = Expression.Block(variableExpMap.Values.ToArray(), expBlocks);

            // Build the dynamic method
            Delegate dynamicMethod = CompileMethod(lastType, paramExps, blockExpr);

            // Build the function info
            funcInfo.Method = dynamicMethod.Method;
            funcInfo.DynamicMethod = dynamicMethod;
            return funcInfo;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Compile function {Name} failed", Name);
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
        return (Delegate)typeof(FunctionType)
            .GetMethod(nameof(CompileDynamicMethod), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(lambdaType)
            .Invoke(null, [blockExpr, paramExps])!;
    }
    
    /// <summary>
    /// Compile a function node expression to expression
    /// </summary>
    static Expression CompileFunctionNodeExpression(SchemaContext context, Expression contextExp, IReadOnlyDictionary<string, Expression> paramMap, Dictionary<string, ParameterExpression> expMap, List<Expression> blocks, FunctionNodeExpTree expTree, LabelTarget? returnLabel = null, Type? expectedType = null)
    {
        switch (expTree)
        {
            // App data source access, like Linq
            case AppDataSourceAccessExpNode appData:
            {
                AppDataSourceAccessExpNode? currentAcc = appData;
                string app = appData.App;
                string field = appData.Field;
                FunctionNodeExpTree? target = null;
                FunctionNodeExpTree? skip = null;
                FunctionNodeExpTree? take = null;
                List<AppSchemaDataOrder> orders = [];
                Expression? filter = null;
                    
                while (currentAcc != null)
                {
                    // Collect the access info
                    target ??= currentAcc.Target;
                    skip ??= currentAcc.Skip;
                    take ??= currentAcc.Take;
                    if (currentAcc.Order != null && !orders.Any(o => o.Field.Equals(currentAcc.Order.Field, StringComparison.OrdinalIgnoreCase)))
                        orders.Add(currentAcc.Order);
                    
                    // combine filter
                    if (currentAcc.Filter != null)
                    {
                        Expression? genFilter;
                        if (currentAcc.LeafNodes.Length > 1)
                        {
                            Expression[] args = new Expression[currentAcc.LeafNodes.Length - 1];
                            int index = 0;
                            for (int i = 0; i < currentAcc.LeafNodes.Length; i++)
                            {
                                if (i == currentAcc.SourceIndex) continue; // skip source index
                                var leaf = currentAcc.LeafNodes[i]!;

                                // Gets the type
                                Type callType = leaf.SchemaType?.ToCSharpType() ?? throw new Exception($"The expression {currentAcc.Name}'s {i} argument type not valid.");
                               
                                // Build the call arguments
                                switch (leaf)
                                {
                                    case ParamsExpNode:
                                        args[index] = CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, leaf, returnLabel, callType);
                                        callType = callType.MakeArrayType();
                                        break;

                                    default:
                                        args[index] = CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, leaf, returnLabel, callType);
                                        break;
                                }

                                // Add Conversion
                                args[index] = ConvertExp(context, callType, args[index]);
                                index++;
                            }
                            genFilter = Expression.Call(null,
                                typeof(RowAccessExpTreeVisitor).GetMethod(nameof(RowAccessExpTreeVisitor.Expand))!,
                                Expression.Constant(currentAcc.Filter, typeof(AccessExpNode)),
                                Expression.NewArrayInit(typeof(object), args));
                        }
                        else
                        {
                            genFilter = Expression.Constant(currentAcc.Filter, typeof(AccessExpNode));
                        }

                        // Combine filter
                        if (filter == null)
                            filter = genFilter;
                        else
                            filter = Expression.Call(null,
                                typeof(RowAccessExpTreeVisitor).GetMethod(nameof(RowAccessExpTreeVisitor.And))!,
                                filter, genFilter);
                    }
                    
                    // Check base query
                    currentAcc = currentAcc.LeafNodes[currentAcc.SourceIndex] as AppDataSourceAccessExpNode;
                }

                // Process the query
                MethodInfo queryMethod = typeof(AppDataQueryExtension).GetMethod(nameof(AppDataQueryExtension.GetFilterFieldDataAsync))!;

                // Gets the task result
                MethodCallExpression callExp = Expression.Call(null, queryMethod, contextExp, Expression.Constant(app), Expression.Constant(field), 
                    target != null ? CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, target, returnLabel, typeof(string)) : Expression.Constant(string.Empty), 
                    filter ?? Expression.Constant(null, typeof(AccessExpNode)),
                    Expression.Constant(appData.Result),
                    skip != null ? CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, skip, returnLabel, typeof(int)) : Expression.Constant(0),
                    take != null ? CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, take, returnLabel, typeof(int)) : Expression.Constant(0),
                    Expression.Constant(orders.ToArray()));
                callExp = Expression.Call(callExp, callExp.Type.GetMethod(nameof(Task.GetAwaiter), System.Type.EmptyTypes)!);
                Expression result = Expression.Call(callExp, callExp.Type.GetMethod(nameof(TaskAwaiter.GetResult), System.Type.EmptyTypes)!);

                switch (appData.Result)
                {
                    case AppDataSourceAccessResult.List:
                        result = Expression.Convert(result, typeof(ArrayTypeNode));
                        break;
                    case AppDataSourceAccessResult.First:
                        result = Expression.Convert(result, typeof(StructTypeNode));
                        break;
                    case AppDataSourceAccessResult.Last:
                        result = Expression.Convert(result, typeof(StructTypeNode));
                        break;
                    case AppDataSourceAccessResult.Count:
                        result = Expression.Convert(result, typeof(ScalarTypeNode));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                return ConvertExp(context, expectedType ?? result.Type, result);
            }
            
            // Const exp
            case ConstantExpNode constExp:
            {
                // For reduce
                if (constExp.Value == null && expectedType != null && !expectedType.IsNullable())
                    return Expression.Default(expectedType);

                object? value = constExp.Value;
                if (expectedType != null)
                {
                    if (value != null) value = expectedType.GetNotNullType().TryConvert(value);
                    return value != null && value.GetType().IsSafeConstantValue()
                        ? Expression.Constant(value, expectedType)
                        : Expression.Default(expectedType);
                }
                return value != null && value.GetType().IsSafeConstantValue()
                    ? (Expression)Expression.Constant(value)
                    : throw new Exception("Constant expression value type not supported");
            }

            // Argument exp
            case FunctionNodeArgument argExp:
            {
                return paramMap.TryGetValue(argExp.Name, out Expression? arg) ? arg : throw new Exception($"The argument {argExp.Name} not found in parameter map");
            }
            
            // Params exp
            case ParamsExpNode paramsExp:
            {
                expectedType ??= paramsExp.LeafNodes[0]?.SchemaType?.ToCSharpType() ?? throw new Exception("Params expression must have expected type");
                List<Expression> paramExps = [];
                foreach (var p in paramsExp.LeafNodes)
                {
                    if (p == null) continue;
                    paramExps.Add(CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, p, returnLabel, expectedType));
                }
                return Expression.NewArrayInit(expectedType, paramExps);
            }
            
            // Common function call
            case FunctionNodeExpression exp:
            {
                // inner call, use the variable directly
                if (exp.Used != 1 && expectedType != null) return expMap[exp.Name];

                // function validate
                SchemaFuncInfo callFuncInfo = exp.FuncNode?.GetSchemaFuncInfo(context) ?? throw new Exception($"The expression {exp.Name} can't be compiled - return type not supported");
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
                    FunctionNodeExpTree leaf = exp.LeafNodes[i] ?? throw new Exception($"The expression {exp.Name}'s {i} argument not valid.");
                    
                    // Gets the type
                    Type callType = (exp.FuncNode.Args[i].SchemaType is GenericTypeNode ? exp.Args[i].SchemeType : exp.FuncNode.Args[i].SchemaType)
                                    ?.ToCSharpType(exp.FuncNode!.Args[i].Nullable) ?? throw new Exception($"The expression {exp.Name}'s {i} argument type not valid.");

                    Type? expectedLeafType = exp.ArrayIndex == i 
                        ? callType.IsAssignableTo(typeof(AnySchemaNode)) ? typeof(ArrayTypeNode) : null
                        : callType;
                    
                    // Build the call arguments
                    switch (leaf)
                    {
                        case ParamsExpNode:
                            callArgs[i + useContext] = CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, leaf, returnLabel, expectedLeafType);
                            callType = callType.MakeArrayType();
                            break;
                        default:
                            callArgs[i + useContext] = CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, leaf, returnLabel, expectedLeafType);
                            break;
                    }

                    // Convert the call type to argument type
                    if (exp.ArrayIndex != i) // !exp.LeafNodes[i].UseAsArray)
                    {
                        // Add Conversion
                        callArgs[i + useContext] = ConvertExp(context, callType, callArgs[i + useContext]);
                        callArgTypes[i + useContext] = callArgs[i + useContext].Type;
                    }
                    else
                    {
                        arrayIndex = i + useContext;
                        callArgTypes[i + useContext] = exp.LeafNodes[i]?.SchemaType is ArrayType a ? (a.ElementSchemaType?.ToCSharpType() ?? callType) : callType;
                    }
                }

                // Call the functions
                MethodInfo callMethod = callFuncInfo.Method!;
                bool hasClosure = callFuncInfo.DynamicMethod != null && callFuncInfo.DynamicMethod.HasClosure();
                Type expReturnType = exp.SchemaType?.ToCSharpType((callFuncInfo.Sign & FUNC_SIGN_NULLABLE_RET) > 0) ?? throw new Exception($"The expression {exp.Name}'s type not valid");
                Type epxReturnElement = (exp.Type is ExpressionType.Map or ExpressionType.Filter) && exp.SchemaType is ArrayType arr
                    ? (arr.ElementSchemaType?.ToCSharpType() ?? throw new Exception($"The expression {exp.Name}'s type not valid"))
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
                            type = epxReturnElement.GetNotNullType();
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
                    string genSign = string.Join('|', genTypes.Select(p => (Nullable.GetUnderlyingType(p!) ?? p!).FullName));
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
                    callMethod = typeof(FunctionType).GetMethod($"CallRemoteFunction{count}", BindingFlags.Static | BindingFlags.NonPublic)!;

                    // Make generic type
                    if (count > 0)callMethod = callMethod.MakeGenericMethod(callArgs.Skip(3).Select(e => e.Type).Prepend(epxReturnElement).ToArray());
                }

                // Generate call body
                if (exp.Type == ExpressionType.Call)
                    return GenMethodCallExp(context, callFuncInfo, callMethod, callArgs, epxReturnElement, returnLabel: returnLabel);

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
                    ExpressionType.Map => expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode),
                    ExpressionType.Reduce => callMethodReturn,
                    ExpressionType.First => callArgTypes[arrayIndex],
                    ExpressionType.Last => callArgTypes[arrayIndex],
                    ExpressionType.Filter => expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode),
                    _ => throw new ArgumentOutOfRangeException()
                });
                ParameterExpression start = Expression.Parameter(typeof(int), "_start");
                ParameterExpression stop = Expression.Parameter(typeof(int), "_stop");
                LabelTarget forLabel = Expression.Label(typeof(int));
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
                    innerCallArgs[arrayIndex] = Expression.MakeIndex(jarray, jarray.Type.GetProperty("Item", [typeof(int)])!, 
                        [exp.Type == ExpressionType.Last ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start)]);
                }

                // Conversion
                Type ctype = callMethod.GetParameters()[arrayIndex + (hasClosure ? 1 : 0)].ParameterType;
                innerCallArgs[arrayIndex] = ConvertExp(context, ctype, innerCallArgs[arrayIndex]);
                callArgTypes[arrayIndex] = innerCallArgs[arrayIndex].Type;

                // Generate call body
                Delegate innerCall;
                Expression arrayLen = jarray.Type.IsSZArray ? Expression.ArrayLength(jarray) : Expression.Property(jarray, "Count");
                Expression ctor = resExp.Type == typeof(ArrayTypeNode)
                    ? Expression.New(resExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType!), Expression.Constant(null))
                    : Expression.New(resExp.Type);

                switch (exp.Type)
                {
                    // Map the element
                    case ExpressionType.Map:
                        {
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                [resExp, start, stop, final],
                                Expression.Assign(resExp, ctor),
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        resExp.Type.IsArrayType() 
                                            ? callMethodReturn.IsArrayType()
                                                ? Expression.Call(resExp, resExp.Type.GetMethod("AddRange", [typeof(IEnumerable<>).MakeGenericType(expReturnType)
                                                ])!, GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel))
                                                : Expression.Call(resExp, resExp.Type.GetMethod("Add")!, GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel))
                                            : callMethodReturn == typeof(ArrayTypeNode)
                                                ? Expression.Call(resExp, typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.AddRange))!, GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel))
                                                : Expression.Call(resExp, typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.Add))!, GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel)),
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
                            Expression init = innerCallArgs.Length > sumIndex ? innerCallArgs[sumIndex] : Expression.Condition(
                                Expression.GreaterThan(arrayLen, Expression.Constant(0)),
                                innerCallArgs[arrayIndex],
                                Expression.Default(callMethodReturn)
                            );

                            // Replace the sum exp
                            innerCallArgs[sumIndex] = resExp;

                            // Compile
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                [resExp, start, stop, final],
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Assign(resExp, init),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        Expression.Assign(resExp, GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel)),
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

                            // Compile
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                [resExp, start, stop, init, final],
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Assign(init, Expression.Default(callArgTypes[arrayIndex])),
                                Expression.Assign(resExp, init),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        Expression.Block(new List<Expression>()
                                        {
                                            Expression.Assign(resExp, temp),
                                            Expression.IfThenElse(GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel), Expression.Break(forLabel, stop), Expression.Assign(resExp, init))
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

                            // Compile
                            innerCall = CompileMethod(resExp.Type, innerParams, Expression.Block(
                                [resExp, start, stop, init, final],
                                Expression.Assign(stop, Expression.Constant(0, typeof(int))),
                                Expression.Assign(start, arrayLen),
                                Expression.Assign(init, Expression.Default(callArgTypes[arrayIndex])),
                                Expression.Assign(resExp, init),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.GreaterThan(start, stop),
                                        Expression.Block(new List<Expression>()
                                        {
                                            Expression.Assign(resExp, temp),
                                            Expression.IfThenElse(GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel), Expression.Break(forLabel, stop), Expression.Assign(resExp, init))
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
                                [resExp, start, stop, final, curr],
                                Expression.Assign(resExp, ctor),
                                Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                Expression.Assign(stop, arrayLen),
                                Expression.Loop(
                                    Expression.IfThenElse(
                                        Expression.LessThan(start, stop),
                                        Expression.Block(new List<Expression>()
                                        {
                                            Expression.Assign(curr, temp),
                                            Expression.IfThen(GenMethodCallExp(context, callFuncInfo, callMethod, innerCallArgs, returnLabel: returnLabel),
                                                Expression.Call(resExp, resExp.Type.GetMethod("Add")!, curr)
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

            // Generate the field access
            case FieldAccessExpNode access:
            {
                FunctionType getFieldType = (context
                    .GetSchemaTypeAsync($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}").GetAwaiter()
                    .GetResult() as FunctionType)!;
                SchemaFuncInfo callFuncInfo = getFieldType.GetSchemaFuncInfo(context)!;
                Expression[] callArgs = new Expression[2];
                callArgs[0] = CompileFunctionNodeExpression(context, contextExp, paramMap, expMap, blocks, access.LeafNodes[0]!, returnLabel, typeof(StructTypeNode));
                callArgs[0] = ConvertExp(context, typeof(StructTypeNode), callArgs[0]);
                callArgs[1] = Expression.Constant(access.FieldName, typeof(string));

                // Call the functions
                Type expReturnType = expectedType ?? access.SchemaType?.ToCSharpType((callFuncInfo.Sign & FUNC_SIGN_NULLABLE_RET) > 0) ?? throw new Exception($"The expression {access.FieldName}'s type not valid");
                
                // Generate the generic method
                Type?[] genTypes = [expReturnType];
                string genSign = string.Join('|', genTypes.Select(p => (Nullable.GetUnderlyingType(p!) ?? p!).FullName));
                MethodInfo callMethod = callFuncInfo.GenericMethods.GetOrAdd(genSign, _ => callFuncInfo.Method!.MakeGenericMethod(genTypes!));
                
                return GenMethodCallExp(context, callFuncInfo, callMethod, callArgs, expectedType, returnLabel: returnLabel);
            }
            
            // Generate the result
            case StructResultExpNode strt:
            {
                // Only one struct result can exist
                ParameterExpression resultVar = Expression.Parameter(typeof(StructTypeNode));
                expMap.Add("_retobject", resultVar);
                blocks.Add(Expression.Assign(resultVar, Expression.New(typeof(StructTypeNode).GetConstructors()[0], Expression.Constant(strt.SchemaType), Expression.Constant(null))));
                MethodInfo objectAdd = typeof(StructTypeNode).GetMethod(nameof(StructTypeNode.SetField))!;

                // Build the result
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
                            continue; // skip
                    }

                    // Build the exp
                    blocks.Add(Expression.Call(resultVar, objectAdd, Expression.Constant(name, typeof(string)), ConvertExp(context, typeof(Object), memberExp)));
                }
                return resultVar;
            }
        }
        // won't hit
        return Expression.Empty();
    }

    // Convert expression
    static Expression ConvertExp(SchemaContext context, Type ctype, Expression exp)
    {
        if (ctype == exp.Type) return exp;
        if (ctype.IsAssignableFrom(exp.Type) || exp.Type == typeof(object)) return Expression.Convert(exp, ctype);

        Expression notNullExp = exp.Type.IsNullable() ? Expression.Call(exp, exp.Type.GetMethod("GetValueOrDefault", System.Type.EmptyTypes)!) : exp;
        Expression? resExp = null;
        Type rctype = ctype.GetNotNullType();
        
        // convert csharp type to schema type node
        if (ctype.IsAssignableTo(typeof(AnySchemaNode)))
        {
            string schema = exp.Type.GetSchemaType(true) ?? throw new Exception($"The type {exp.Type.FullName} can't be converted to schema type node");
            AnySchemeType schemaType = context.GetSchemaTypeAsync(schema).GetAwaiter().GetResult() ?? throw new Exception($"The schema type node {schema} not found");
            MethodInfo method = typeof(AnySchemeType).GetMethod(nameof(CreateNode))!;
            return Expression.Convert(Expression.Call(Expression.Constant(schemaType), method, notNullExp), ctype);
        }

        // simple type conversion
        if (!notNullExp.Type.IsAssignableTo(typeof(AnySchemaNode)))
        {
            resExp = System.Type.GetTypeCode(rctype) switch
            {
                TypeCode.Boolean => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToBoolean), [notNullExp.Type])!, notNullExp),
                TypeCode.Char => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToChar), [notNullExp.Type])!, notNullExp),
                TypeCode.SByte => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSByte), [notNullExp.Type])!, notNullExp),
                TypeCode.Byte => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToByte), [notNullExp.Type])!, notNullExp),
                TypeCode.Int16 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt16), [notNullExp.Type])!, notNullExp),
                TypeCode.UInt16 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt16), [notNullExp.Type])!, notNullExp),
                TypeCode.Int32 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt32), [notNullExp.Type])!, notNullExp),
                TypeCode.UInt32 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt32), [notNullExp.Type])!, notNullExp),
                TypeCode.Int64 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt64), [notNullExp.Type])!, notNullExp),
                TypeCode.UInt64 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt64), [notNullExp.Type])!, notNullExp),
                TypeCode.Single => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSingle), [notNullExp.Type])!, notNullExp),
                TypeCode.Double => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDouble), [notNullExp.Type])!, notNullExp),
                TypeCode.Decimal => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDecimal), [notNullExp.Type])!, notNullExp),
                TypeCode.DateTime => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDateTime), [notNullExp.Type])!, notNullExp),
                TypeCode.String => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToString), [notNullExp.Type])!, notNullExp),
                _ => resExp
            };
        }

        // for complex types
        if (resExp == null)
        {
            MethodInfo method = typeof(Extension).GetMethod(nameof(Extension.TryConvert), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            resExp = Expression.Convert(Expression.Call(null, method, Expression.Constant(ctype), notNullExp), ctype);
        }

        // nullable result
        return rctype == ctype
            ? resExp
            : Expression.Condition(Expression.NotEqual(exp, Expression.Constant(null, exp.Type)), resExp, Expression.Constant(null, ctype));
    }

    // Gen method call
    static Expression GenMethodCallExp(SchemaContext context, SchemaFuncInfo callFuncInfo, MethodInfo callMethod, Expression[] callArgs, Type? returnType = null, LabelTarget? returnLabel = null)
    {
        // Call the method
        Expression result;
        if ((callFuncInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC)
        {
            // Gets the task result
            MethodCallExpression callExp = Expression.Call(null, callMethod, callArgs);
            callExp = Expression.Call(callExp, callExp.Type.GetMethod(nameof(Task.GetAwaiter), System.Type.EmptyTypes)!);
            result = Expression.Call(callExp, callExp.Type.GetMethod(nameof(TaskAwaiter.GetResult), System.Type.EmptyTypes)!);
        }
        else if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result = callFuncInfo.Name switch
            {
                // system.math
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.e)}" => Expression.Constant(Math.E),
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.pi)}" => Expression.Constant(Math.PI),
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.add)}" => Expression.Add(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.subtract)}" => Expression.Subtract(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.multiply)}" => Expression.Multiply(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.divide)}" => Expression.Divide(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_MATH}.{nameof(SystemMath.modulo)}" => Expression.Modulo(callArgs[0], callArgs[1]),
                
                // system.conv
                $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}" => callArgs[0],
                $"{NS_SYSTEM_CONV}.null" => Expression.Constant(null, callMethod.ReturnType.GetNullableType()),

                // system.logic
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isnull)}" => Expression.Equal(callArgs[0], Expression.Constant(null, callArgs[0].Type)),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.cond)}" => Expression.Condition(callArgs[0], callArgs[1], callArgs[2]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessthan)}" => Expression.LessThan(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessequal)}" => Expression.LessThanOrEqual(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.equal)}" => Expression.Equal(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notequal)}" => Expression.NotEqual(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greatethan)}" => Expression.GreaterThan(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greateequal)}" => Expression.GreaterThanOrEqual(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.andalso)}" => Expression.AndAlso(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.orelse)}" => Expression.OrElse(callArgs[0], callArgs[1]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}" => Expression.Not(callArgs[0]),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}" => Expression.AndAlso( // (value, min, max, includeMin, includeMax)
                    Expression.Condition(callArgs[3], Expression.GreaterThanOrEqual(callArgs[0], callArgs[1]), Expression.GreaterThan(callArgs[0], callArgs[1])),
                    Expression.Condition(callArgs[4], Expression.LessThanOrEqual(callArgs[0], callArgs[2]), Expression.LessThan(callArgs[0], callArgs[2]))
                    ),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifret)}" => Expression.Block(
                    Expression.IfThen(
                        callArgs[0],
                        Expression.Return(returnLabel!, callArgs[1])
                    ),
                    callArgs[1]
                ),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnot)}" => Expression.Block(
                    Expression.IfThen(
                        Expression.Not(callArgs[0]),
                        Expression.Return(returnLabel!, callArgs[1])
                    ),
                    callArgs[1]
                ),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnull)}" => Expression.Block(
                    Expression.IfThen(
                        Expression.Equal(callArgs[0], Expression.Constant(null, callArgs[0].Type)),
                        Expression.Return(returnLabel!, callArgs[1])
                    ),
                    callArgs[1]
                ),
                $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifempty)}" => Expression.Block(
                    Expression.IfThen(
                        Expression.Call(typeof(SystemLogic).GetMethod(nameof(SystemLogic.isempty))!, callArgs[0]),
                        Expression.Return(returnLabel!, callArgs[1])
                    ),
                    callArgs[1]
                ),
                
                // default
                _ => Expression.Call(null, callMethod, callArgs)
            };
        }
        else
        {
            result = GenDynamicCallExp(callMethod.ReturnType, callFuncInfo.DynamicMethod!, callArgs);
        }

        if (returnType != null && returnType != result.Type)
        {
            result = ConvertExp(context, returnType, result);
        }
        return result;
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

    // Compile lambda to method
    static T CompileDynamicMethod<T>(Expression block, params ParameterExpression[] inputs)
        => Expression.Lambda<T>(block, inputs).Compile();


    #region CallDynamicFunc

    static TR? CallDynamicFunc<TR>(Delegate del, object[] args)
    {
        var method = del.Method;
        var target = del.Target;
        var parms = method.GetParameters();
        int parmCount = parms.Length;
        bool firstIsClosure = parmCount > 0 && parms[0].ParameterType.FullName == "System.Runtime.CompilerServices.Closure";

        // Case A: static method
        if (method.IsStatic)
        {
            // If static method expects a Closure as first parameter and we do have a closure target,
            // pass the closure as first arg and invoke with null target.
            if (firstIsClosure && target != null && parmCount == args.Length + 1)
            {
                var newArgs = new object?[args.Length + 1];
                newArgs[0] = target;
                Array.Copy(args, 0, newArgs, 1, args.Length);
                return (TR?)method.Invoke(null, newArgs);
            }

            // If parameter count exactly matches args, call as plain static
            if (parmCount == args.Length)
                return (TR?)method.Invoke(null, args);

            // otherwise fallback to DynamicInvoke (safer but slower)
            return (TR?)del.DynamicInvoke(args);
        }

        // Case B: instance method
        // Normal: instance method => call with target as instance, args match parmCount
        if (!method.IsStatic)
        {
            if (parmCount == args.Length)
                return (TR?)method.Invoke(target, args);

            // Special: open-instance-like delegate: target == null, but method expects instance as first parameter:
            // if parmCount == args.Length + 1, treat args[0] as the instance
            if (target == null && parmCount == args.Length + 1)
            {
                var newTarget = args[0];
                var remaining = new object?[args.Length - 1];
                Array.Copy(args, 1, remaining, 0, remaining.Length);
                return (TR?)method.Invoke(newTarget, remaining);
            }

            // Fallback to DynamicInvoke
            return (TR?)del.DynamicInvoke(args);
        }

        // Fallback (shouldn't reach here)
        return (TR?)del.DynamicInvoke(args);
    }

    // Call dynamic function
    static TR? CallDynamicFunc1<TR, T1>(Delegate method, T1 arg1)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!]);
    }
    static TR? CallDynamicFunc2<TR, T1, T2>(Delegate method, T1 arg1, T2 arg2)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!]);
    }
    static TR? CallDynamicFunc3<TR, T1, T2, T3>(Delegate method, T1 arg1, T2 arg2, T3 arg3)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!]);
    }
    static TR? CallDynamicFunc4<TR, T1, T2, T3, T4>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!]);
    }
    static TR? CallDynamicFunc5<TR, T1, T2, T3, T4, T5>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!]);
    }
    static TR? CallDynamicFunc6<TR, T1, T2, T3, T4, T5, T6>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!]);
    }
    static TR? CallDynamicFunc7<TR, T1, T2, T3, T4, T5, T6, T7>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!]);
    }
    static TR? CallDynamicFunc8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!]);
    }
    static TR? CallDynamicFunc9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!]);
    }
    static TR? CallDynamicFunc10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!]);
    }
    static TR? CallDynamicFunc11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11! ]);
    }
    static TR? CallDynamicFunc12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12! ]);
    }
    static TR? CallDynamicFunc13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13! ]);
    }
    static TR? CallDynamicFunc14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14! ]);
    }
    static TR? CallDynamicFunc15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!
        ]);
    }
    static TR? CallDynamicFunc16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!, arg16!]);
    }
    static TR? CallDynamicFunc17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16, T17 arg17)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!, arg16!, arg17!]);
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
    internal SchemaFuncInfo? GetSchemaFuncInfo(SchemaContext context)
    {
        if (FuncInfo != null) return FuncInfo.Method != null ? FuncInfo : null;

        // Check is static
        if (StaticMethodMap.TryGetValue(Name, out SchemaFuncInfo? result) && (result.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result.FunctionNode = this;
            FuncInfo = result;
            return result;
        }
        
        // Build Exp
        PreCompileAsync(context).GetAwaiter().GetResult();

        // Compile
        FuncInfo = CompileFunction(context);
        return FuncInfo;
    }

    // Gets the call dynamic func
    static MethodInfo GetCallDynamicFunc(Type ret, params Type[] inputs)
    {
        MethodInfo method = typeof(FunctionType).GetMethod($"CallDynamicFunc{inputs.Length}", BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.MakeGenericMethod(inputs.Prepend(ret).ToArray());
    }

    #endregion

    #region Utility

    protected void ResizeGeneric(int count)
    {
        if (Generic.Length >= count) return;
        AnySchemeType?[] generic = new AnySchemeType?[count];
        for(int i = 0; i < Math.Min(count, Generic.Length); i++)
            generic[i] = Generic[i];
        Generic = generic;
    }

    // staitc mappings
    private static readonly ConcurrentDictionary<string, SchemaFuncInfo> StaticMethodMap = new();
    private static readonly ConcurrentDictionary<string, MethodInfo> CallConvertNullableExp = new();

    private static readonly string[] RetFunc =
    [
        $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifret)}",
        $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnot)}",
        $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnull)}",
        $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifempty)}",
    ];
    
    const string GET_DATA_SOURCE = $"{NS_SYSTEM_DATA}.{nameof(SystemData.getdatasource)}";

    #endregion

    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(FunctionType? schema)
    {
        return schema?.ToSchema().With(new FunctionSchema
        {
            Return = schema.Return,
            Args = schema.Args.Select(a => new FuncArg
            {
                Name = a.Name.ToCamelCase(),
                Type = a.Type,
                Nullable = a.Nullable,
                Params = a.Params,
                Default = a.Default,
                Status = a.Status != null && a.Status != SchemaNodeStatus.Ready ? a.Status : null,
            }).ToArray(),
            Exps = schema.Exps.Select(e => new FuncExp
            {
                Name = e.Name.ToCamelCase(),
                Func = e.Func,
                Type = e.Type ?? ExpressionType.Call,
                Return = e.Return,
                Args = e.Args,
                Status = e.Status != null && e.Status != SchemaNodeStatus.Ready ? e.Status : null,
            }).ToArray(),
            Generic = schema.Generic.Where(g => g is not null).Select(g => g!.Name).ToArray(),
            Server = schema.Server,
            Nocache = schema.Nocache,
        });
    }
    
    #endregion
}

#region Inner Type

/// <summary>
/// The expression tree
/// </summary>
public abstract class FunctionNodeExpTree
{
    /// <summary>
    /// The leaf nodes as sub expressions
    /// </summary>
    public FunctionNodeExpTree?[] LeafNodes { get; set; } = [];

    /// <summary>
    /// The type node
    /// </summary>
    public AnySchemeType? SchemaType { get; set; }

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

    /// <summary>
    /// Whether params argument
    /// </summary>
    public bool? Params { get; set; }

    /// <summary>
    /// The default value
    /// </summary>
    public object? Default { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public SchemaNodeStatus? Status { get; set; }
    
    /// <summary>
    /// The index
    /// </summary>
    public int? Index { get; set; }
    
    #endregion
    
    #region Conversion

    public static implicit operator FunctionNodeArgument(FuncArg arg)
    {
        return new FunctionNodeArgument
        {
            Name = arg.Name,
            Type = arg.Type,
            Nullable = arg.Nullable,
            Params = arg.Params,
            Default = arg.Default,
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
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// The function to be called.
    /// </summary>
    public string Func { get; internal set; } = string.Empty;

    /// <summary>
    /// The function used to map array elements
    /// </summary>
    public ExpressionType? Type { get; init; } = ExpressionType.Call;

    /// <summary>
    /// The namespace.
    /// </summary>
    public string Return { get; internal set; } = string.Empty;

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public SchemaNodeStatus? Status { get; set; }

    /// <summary>
    /// The index of the array used for Map/Reduce/First
    /// </summary>
    public int? ArrayIndex { get; set; }

    #endregion

    #region Relationship

    /// <summary>
    /// The function node
    /// </summary>
    public FunctionType? FuncNode { get; set; }

    #endregion

    #region Conversion

    public static implicit operator FunctionNodeExpression(FuncExp exp)
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

public class FieldAccessExpNode : FunctionNodeExpTree
{
    /// <summary>
    /// The field name
    /// </summary>
    public required string FieldName { get; init; }
}

/// <summary>
/// The constant expression tree
/// </summary>
public class ConstantExpNode : FunctionNodeExpTree
{
    /// <summary>
    /// The constant value
    /// </summary>
    public AnySchemaNode? Value { get; init; }
}

/// <summary>
/// The params expression tree
/// </summary>
public class ParamsExpNode : FunctionNodeExpTree
{
}

/// <summary>
/// The app data source access expression tree
/// </summary>
public class AppDataSourceAccessExpNode: FunctionNodeExpression
{
    /// <summary>
    /// The app
    /// </summary>
    public string App { get; internal set; }

    /// <summary>
    /// The app field
    /// </summary>
    public string Field { get; internal set; }

    /// <summary>
    /// The app target
    /// </summary>
    public FunctionNodeExpTree? Target { get; internal set; }
    
    /// <summary>
    /// The struct type of the data source
    /// </summary>
    public StructType? StructType { get; internal set; }

    /// <summary>
    /// The filter expression
    /// </summary>
    public AccessExpNode? Filter { get; set; }

    /// <summary>
    /// The source index
    /// </summary>
    public int SourceIndex { get; set; } = 0;
    
    /// <summary>
    /// The result access type
    /// </summary>
    public AppDataSourceAccessResult Result { get; internal set; }
    
    /// <summary>
    /// The skip count
    /// </summary>
    public FunctionNodeExpTree? Skip { get; set; }
    
    /// <summary>
    /// The take count
    /// </summary>
    public FunctionNodeExpTree? Take { get; set; }
    
    /// <summary>
    /// The order
    /// </summary>
    public AppSchemaDataOrder? Order { get; set; }
    
    #region Constructor

    public AppDataSourceAccessExpNode(FunctionNodeExpression exp, AppDataSourceAccessExpNode? prev = null)
    {
        Name = exp.Name;
        Func = exp.Func;
        Type = exp.Type;
        Return = exp.Return;
        Args = exp.Args;
        Status = exp.Status;
        ArrayIndex = exp.ArrayIndex;
        FuncNode = exp.FuncNode;
        LeafNodes = exp.LeafNodes;
        SchemaType = exp.SchemaType;
        Used = exp.Used;

        App = prev?.App ?? string.Empty;
        Field = prev?.Field ?? string.Empty;
        StructType = prev?.StructType;
        Result = prev?.Result ?? AppDataSourceAccessResult.List;
    }

    #endregion
}

/// <summary>
/// The app data source access result
/// </summary>
public enum AppDataSourceAccessResult
{
    /// <summary>
    /// The list of records
    /// </summary>
    List,
    
    /// <summary>
    /// The first record
    /// </summary>
    First,
    
    /// <summary>
    /// The last record
    /// </summary>
    Last,
    
    /// <summary>
    /// The count of records
    /// </summary>
    Count,
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
    public FunctionType? FunctionNode { get; set; }

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


public class GenericTypeNode: AnySchemeType
{
    /// <summary>
    /// Possible base type
    /// </summary>
    public AnySchemeType? BaseNode { get; set; }

    /// <summary>
    /// The index in generic array
    /// </summary>
    public int GenericIndex { get; init; }
}

#endregion