using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static SchemaNode.Utility.Constant;
using System.Text.RegularExpressions;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory function schema representation
/// </summary>
public class FunctionNode: NamespaceNode
{
    #region Data
    
    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FunctionArgumentInfo[] Args { get; set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FunctionExpression[] Exps { get; set; } = [];

    /// <summary>
    /// The basic type of generic types, provided to T(single generic type),
    /// T1, T2(for multi generic type)
    /// </summary>
    public string[]? Generic { get; set; }

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
    public override SchemaType Type => SchemaType.Function;
    
    /// <summary>
    /// Whether the function will be used to construct the object
    /// </summary>
    public bool IsStructConstructor { get; set; }

    /// <summary>
    /// Whether the function is server only
    /// </summary>
    public bool IsServerOnly { get; set; }

    /// <summary>
    /// Whether the function require call server
    /// </summary>
    public bool RequireServerCall { get; set; }

    /// <summary>
    /// Whether the function client only
    /// </summary>
    public bool IsClientOnly { get; set; }
    
    /// <summary>
    /// The function info
    /// </summary>
    public SchemaFuncInfo? FuncInfo { get; set; }
    
    #endregion
    
    #region Ref
    
    /// <summary>
    /// The return type node
    /// </summary>
    public NamespaceNode? ReturnNode { get; set; }

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
        Args = func?.Args ?? [];
        Exps = func?.Exps ?? [];
        Generic = func?.Generic;
        Server = func?.Server;
        Nocache = func?.Nocache;

        // Status
        if (func == null)
        {
            Status = SchemaNodeStatus.NoDefinition;
            return;
        }

        // Generic check
        NamespaceNode?[] genericNodes = new NamespaceNode[Generic?.Length ?? 0];
        if (Generic?.Length > 0)
        {
            for(int i = 0; i < Generic.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(Generic[i]) && !Regex.IsMatch(Generic[i], @"^[tT]\d*$"))
                {
                    genericNodes[i] = await context.GetSchemaNodeAsync(Generic[i]);
                }
            }
        }

        // Gets return type
        if (string.IsNullOrWhiteSpace(Return))
        {
            Status = SchemaNodeStatus.FunctionWrongReturnType;
        }
        else if (Regex.IsMatch(Return, @"^[tT]\d*$"))
        {
            // Generic type
            int index = Return.Length > 1 && int.TryParse(Return[1..], out int i) ? i : 1;
            ReturnNode = new GenericTypeNode
            {
                GenericIndex = index,
                BaseNode = genericNodes.Length >= index ? genericNodes[index - 1] : null
            };
        }
        else
        {
            ReturnNode = await context.GetSchemaNodeAsync(Return);
            if (ReturnNode == null) Status = SchemaNodeStatus.FunctionWrongReturnType;
        }

        // Check if server or client only
        IsServerOnly = !DataDictContext.IsPublicService && Exps is { Count: 0 } && !staticMethodMap.ContainsKey(Name) && Name.StartsWith($"{NS_SYSTEM}.");
        RequireServerCall = IsServerOnly;
        if(staticMethodMap.TryGetValue(Name, out SchemaFuncInfo funcInfo))
            IsClientOnly = funcInfo.ClientOnly;

        // Generate the exp trees
        if (Status == TypeNodeStatus.Fullfill)
        {
            (List<FunctionNodeExpTree> trees, string error) = await GenerateExpTrees(context, Name, ReturnNode, Args, Exps);
            if (!string.IsNullOrWhiteSpace(error))
                Status = TypeNodeStatus.FunctionExpsHasComplieError;
            else
                ExpTrees = trees;

            // Check if client only
            if (Exps is { Count: > 0 })
            {
                foreach (FunctionNodeExpression exp in Exps.Where(e => e.FunctionNode != null))
                {
                    if (exp.FunctionNode.IsClientOnly)
                        IsClientOnly = true;
                    if (exp.FunctionNode.RequireServerCall)
                        RequireServerCall = true;
                }
            }

            // Check if used to generate the object
            IsStructConstructor = trees.LastOrDefault() is StructResultExpNode;
        }

        // Add usages
        if (Status == TypeNodeStatus.Fullfill)
        {
            ReturnNode?.AddUsage(this);
            Args?.ForEach(a =>
            {
                a.TypeNode?.AddUsage(this);
            });
            Exps?.ForEach(a =>
            {
                a.FunctionNode?.AddUsage(this);
                a.TypeNode?.AddUsage(this);
            });
        }
        
    }

    /// <inheritdoc />
    public override void Release()
    {
        ReturnNode?.RemoveRef(this);
        ReturnNode = null;
        foreach (FunctionArgumentInfo arg in Args)
        {
            arg.TypeNode?.RemoveRef(this);
            arg.TypeNode = null;
        }

        foreach (FunctionExpression exp in Exps)
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
    public override bool CanBeUseAs(NamespaceNode other) => false;

    /// <inheritdoc />
    public override ArrayNode? GetArrayNode(bool exactly = false) => null;
    
    // Clear the function info to be re-complied
    void ClearFunctionInfo()
    {
        FuncInfo = null;
        if (UsedBy == null || UsedBy.IsEmpty) return;
        foreach ((NamespaceNode other, _) in UsedBy)
        {
            if (other is FunctionNode func)
                func.ClearFunctionInfo();
        }
    }

    #endregion

    #region Static

    /// <summary>
    /// Generate the expression tree based on the arguments and expressions
    /// </summary>
    public static async Task<(List<FunctionNodeExpTree>, string)> GenerateExpTrees(SchemaContext context, string name, NamespaceNode returnNode, List<FunctionNodeArgument> args, List<FunctionNodeExpression> exps)
    {
        // Init the result & help variables
        bool isSystemDefined = IsMethodSystemDefined(name);
        List<FunctionNodeExpTree> trees = new();
        Dictionary<string, FunctionNodeExpTree> treeMap = new();

        // Validate the arguments and reset the states
        if (args is { Count: > 0 })
        {
            for (int i = 0; i < args.Count; i++)
            {
                FunctionNodeArgument arg = args[i];
                arg.Used = 0; // Reset the use count

                // Check argument name
                if (string.IsNullOrWhiteSpace(arg.Name))
                {
                    arg.Status = TypeNodeStatus.FunctionArgumentNoName;
                    return (trees, TYPE_FUNC_ARG_NAME_REQUIRED);
                }
                else if (treeMap.ContainsKey(arg.Name))
                {
                    arg.Status = TypeNodeStatus.FunctionArgumentDuplicateName;
                    return (trees, TYPE_FUNC_ARG_NAME_DUPLICATE);
                }

                // Check argument type
                if (!string.IsNullOrWhiteSpace(arg.Type))
                {
                    NamespaceNode node = await context.GetNamespaceNodeAsync(arg.Type);
                    if (node == null || node.Token == System_UnUsable_Token)
                    {
                        arg.Status = TypeNodeStatus.FunctionArgumentWrongType;
                        return (trees, string.Format(TYPE_FUNC_ARG_TYPE_NOT_VALID, arg.Name));
                    }
                    arg.TypeNode = node;
                    arg.Type = node.Name; // fix the name
                }
                else if (arg.UseArgType.HasValue)
                {
                    if (arg.UseArgType > i || arg.UseArgType < 1)
                    {
                        arg.Status = TypeNodeStatus.FunctionArgumentWrongType;
                        return (trees, string.Format(TYPE_FUNC_ARG_USETYPE_NOT_VALID, arg.Name));
                    }
                    arg.TypeNode = new RefArgTypeNode(arg.UseArgType.Value);
                }
                else if (!isSystemDefined)
                {
                    // Only system defined function can use generic type
                    arg.Status = TypeNodeStatus.FunctionArgumentNoType;
                    return (trees, string.Format(TYPE_FUNC_ARG_TYPE_REQUIRED, arg.Name));
                }

                // Add to the tree
                treeMap[arg.Name] = arg;
            }
        }
        //else if (!isSystemDefined)
        //    return (trees, TYPE_FUNC_NEED_ARGS);

        // The system provide expression has no expression body
        if (exps == null || exps.Count == 0)
            return (trees, !isSystemDefined ? TYPE_FUNC_NEED_EXPS : null);

        // Validate the expressions and build the tree
        foreach (FunctionNodeExpression exp in exps)
        {
            List<FunctionNodeExpTree> leafNodes = new(); // leaf nodes
            int arrayArg = -1; // the array argument index
            bool isMapReduce = exp.CallType != ExpressionType.Call;
            HashSet<ArgSchema> isInfers = new(); // The type infered from the value
            HashSet<ArgSchema> upgradeArg = new(); // Whether the call argument type can be downgrade
            Dictionary<ArgSchema, NamespaceNode> baseTypeOfUpgradeArg = new(); // The base type of the upgraded type

            // reset
            exp.LeafNodes = leafNodes;
            exp.Used = 0;
            exp.Status = TypeNodeStatus.Fullfill;

            // Check exp name
            if (string.IsNullOrWhiteSpace(exp.ExpName))
            {
                exp.Status = TypeNodeStatus.FunctionExpNoName;
                return (trees, TYPE_FUNC_EXP_NAME_REQUIRED);
            }
            else if (treeMap.ContainsKey(exp.ExpName))
            {
                exp.Status = TypeNodeStatus.FunctionExpDuplicateName;
                return (trees, string.Format(TYPE_FUNC_EXP_NAME_CONFLICT_ARG, exp.ExpName));
            }

            // Check function
            if (string.IsNullOrWhiteSpace(exp.Call))
            {
                exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                return (trees, string.Format(TYPE_FUNC_CALL_REQUIRE, exp.ExpName));
            }
            if (await context.GetNamespaceNodeAsync(exp.Call) is not FunctionNode funcNode)
            {
                exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                return (trees, string.Format(TYPE_FUNC_CALL_NOT_EXIST, exp.ExpName));
            }
            switch (exp.CallType)
            {
                // Check reduce function
                case ExpressionType.Reduce when funcNode.Args.Count is 0 or > 2:
                    exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                    return (trees, string.Format(TYPE_FUNC_CANT_USE_AS_REDUCE, exp.Call));

                // Check first function
                case ExpressionType.First when funcNode.ReturnNode is not ScalarNode { IsBool: true }:
                    exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                    return (trees, string.Format(TYPE_FUNC_CANT_USE_AS_FIRST, exp.Call));

                // Check last function
                case ExpressionType.Last when funcNode.ReturnNode is not ScalarNode { IsBool: true }:
                    exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                    return (trees, string.Format(TYPE_FUNC_CANT_USE_AS_LAST, exp.Call));

                // Check filter function
                case ExpressionType.Filter when funcNode.ReturnNode is not ScalarNode { IsBool: true }:
                    exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                    return (trees, string.Format(TYPE_FUNC_CANT_USE_AS_FILTER, exp.Call));
            }
            exp.FunctionNode = funcNode;

            // Gets the function info of the function node, only need static method info for generic types
            bool isGenericFunc = funcNode.IsGeneric;
            int[] genericMap = funcNode.GetGenericMap();

            // Check function call arguments and gets the return type
            // Also bind the type node to the expression call arguments, prepare for complier
            if (funcNode.Args is { Count: > 0 })
            {
                // Check arguments
                exp.Args ??= new List<ArgSchema>();
                for (int i = 0; i < funcNode.Args.Count; i++)
                {
                    // Prepare the argument
                    if (exp.Args.Count <= i) exp.Args.Add(new ArgSchema());
                    ArgSchema callArg = exp.Args[i];
                    callArg.TypeNode = funcNode.Args[i].TypeNode; // Use function argument as default

                    // Check nullable
                    if (callArg.Value.IsEmpty() && string.IsNullOrWhiteSpace(callArg.Name))
                    {
                        if ((funcNode.Args[i].Nullable ?? true) || (exp.CallType == ExpressionType.Reduce && i == 1)) // Nullable or Reduce
                        {
                            leafNodes.Add(new ConstantExpNode
                            {
                                Value = null
                            });
                        }
                        else
                        {
                            exp.Status = TypeNodeStatus.FunctionExpWrongFuncArgs;
                            return (trees, string.Format(TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH, exp.ExpName));
                        }
                    }
                    else
                    {
                        // Gets the reference arg
                        ArgSchema refArg = callArg;
                        bool isGeneric = isGenericFunc && genericMap.Contains(i + 1);
                        while (refArg.TypeNode is RefArgTypeNode n)
                        {
                            refArg = exp.Args[n.UseArgType - 1];
                            if (isGenericFunc && genericMap.Contains(n.UseArgType))
                                isGeneric = true;
                        }

                        // Check the value
                        string argName = $"第{i + 1}个";
                        if (string.IsNullOrWhiteSpace(callArg.Name))
                        {
                            // Calc the arg type based on the const value
                            int token = refArg.TypeNode?.Token ?? 0;
                            if (token == System_Enum_Token)
                            {
                                token = ((EnumNode)refArg.TypeNode).ValueType switch
                                {
                                    EnumValueType.String => System_String_Token,
                                    EnumValueType.Int => System_Int_Token,
                                    EnumValueType.Float => System_Float_Token,
                                    EnumValueType.Double => System_Double_Token,
                                    EnumValueType.Flags => System_Int_Token,
                                    _ => throw new ArgumentOutOfRangeException()
                                };
                            }
                            (object res, bool pass, string atype) = GetScalarTypeValue(token, callArg.Value);
                            if (!pass || res == null)
                            {
                                exp.Status = TypeNodeStatus.FunctionExpWrongFuncArgs;
                                return (trees, string.Format(TYPE_FUNC_CALL_ARG_NOT_EXIST, exp.ExpName, argName));
                            }
                            callArg.Value = res switch
                            {
                                JObject obj => obj,
                                JArray arr => arr,
                                _ => new JValue(res)
                            };

                            // Validate the value
                            if (refArg.TypeNode != null)
                            {
                                (_, JToken error) = await refArg.TypeNode.ValidateValue(context, callArg.Value);
                                if (!error.IsEmpty())
                                {
                                    exp.Status = TypeNodeStatus.FunctionExpWrongFuncArgs;
                                    return (trees, string.Format(TYPE_FUNC_CALL_ARG_NOT_EXIST, exp.ExpName, argName));
                                }
                            }
                            else
                            {
                                // Any Type
                                refArg.TypeNode = await context.GetNamespaceNodeAsync(atype);
                                isInfers.Add(refArg);
                            }

                            // Add to leafes
                            leafNodes.Add(new ConstantExpNode
                            {
                                Value = res
                            });
                        }
                        else if (treeMap.TryGetValue(callArg.Name, out FunctionNodeExpTree value))
                        {
                            // Gets the exp/arg type
                            NamespaceNode argTypeNode;
                            switch (value)
                            {
                                case FunctionNodeArgument rarg:
                                    leafNodes.Add(rarg); // Add to leafes
                                    argTypeNode = rarg.TypeNode;
                                    while (argTypeNode is RefArgTypeNode refArgNode)
                                        argTypeNode = args[refArgNode.UseArgType - 1].TypeNode;
                                    if (argTypeNode == null)
                                        return (trees, string.Format(TYPE_FUNC_CALL_ARG_NOT_EXIST, exp.ExpName, argName)); // For safe
                                    break;
                                case FunctionNodeExpression rexp:
                                    leafNodes.Add(rexp); // Add to leafes
                                    argTypeNode = rexp.TypeNode;
                                    break;
                                default:
                                    return (trees, string.Format(TYPE_FUNC_CALL_ARG_NOT_EXIST, exp.ExpName, argName)); // For safe
                            }

                            // Check if used as array type
                            if (isMapReduce && arrayArg < 0 && argTypeNode is ArrayNode { BaseNode: { } } array && refArg.TypeNode is not ArrayNode)
                            {
                                arrayArg = i;
                                //leafNodes.Last().UseAsArray = true;
                                argTypeNode = array.BaseNode;
                            }

                            // Match the type
                            if (refArg.TypeNode != null)
                            {
                                if (refArg.TypeNode == argTypeNode) continue;

                                // Upgrade check
                                if (argTypeNode.CanBeUseAs(refArg.TypeNode))
                                {
                                    // Upgrade the type to real type like number -> int
                                    if ((isInfers.Contains(refArg) || isGeneric) && !upgradeArg.Contains(refArg))
                                    {
                                        if (isInfers.Contains(refArg))
                                            isInfers.Remove(refArg);
                                        else
                                            baseTypeOfUpgradeArg[refArg] = refArg.TypeNode; // Record the base type, can't downgrade the type to lower
                                        refArg.TypeNode = argTypeNode;
                                        upgradeArg.Add(refArg);
                                    }
                                    continue;
                                }

                                // Downgrade check
                                if (refArg.TypeNode.CanBeUseAs(argTypeNode) &&
                                    (isInfers.Contains(refArg) || (upgradeArg.Contains(refArg) &&
                                      (!baseTypeOfUpgradeArg.ContainsKey(refArg) || refArg.TypeNode.CanBeUseAs(baseTypeOfUpgradeArg[refArg]))
                                     )))
                                {
                                    if (isInfers.Contains(refArg)) isInfers.Remove(refArg);
                                    refArg.TypeNode = argTypeNode; // downgrade like int -> number
                                    upgradeArg.Add(refArg);
                                    continue;
                                }

                                // Error
                                exp.Status = TypeNodeStatus.FunctionExpWrongFuncArgs;
                                return (trees, string.Format(TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL, exp.ExpName, argName));
                            }
                            else
                            {
                                // Any Type
                                refArg.TypeNode = argTypeNode;
                                upgradeArg.Add(refArg);
                            }
                        }
                        else
                        {
                            exp.Status = TypeNodeStatus.FunctionExpWrongFuncArgs;
                            return (trees, string.Format(TYPE_FUNC_CALL_ARG_NOT_EXIST, exp.ExpName, argName));
                        }
                    }
                }
            }

            // Check array argument
            if (isMapReduce && arrayArg < 0)
            {
                exp.Status = TypeNodeStatus.FunctionArgumentWrongType;
                return (trees, TYPE_FUNC_ARG_HAS_NOARRAY);
            }

            // Mark leaf nodes used count++
            leafNodes.ForEach(l => l.Used++);

            // Gets return type
            exp.TypeNode = null; // clear
            if (!string.IsNullOrWhiteSpace(exp.Type))
            {
                NamespaceNode node = await context.GetNamespaceNodeAsync(exp.Type);
                if (node == null)
                {
                    exp.Status = TypeNodeStatus.FunctionExpWrongType;
                    return (trees, string.Format(TYPE_FUNC_RET_TYPE_NOT_VALD, exp.ExpName));
                }
                exp.TypeNode = node;
            }

            // Validate the return type
            if (exp.CallType is ExpressionType.First or ExpressionType.Last)
            {
                // Use base type, already done
                NamespaceNode firstType = exp.Args[arrayArg].TypeNode;
                if (exp.TypeNode != null)
                {
                    // Match the type
                    if (!firstType.CanBeUseAs(exp.TypeNode))
                    {
                        exp.Status = TypeNodeStatus.FunctionExpWrongType;
                        return (trees, string.Format(TYPE_FUNC_RET_TYPE_NOT_VALD, exp.ExpName));
                    }
                }
                else
                {
                    exp.TypeNode = firstType;
                }
            }
            else if (exp.CallType is ExpressionType.Filter)
            {
                NamespaceNode firstType = exp.Args[arrayArg].TypeNode;
                if (exp.TypeNode != null)
                {
                    if (exp.TypeNode is not ArrayNode array || !firstType.CanBeUseAs(array.BaseNode))
                    {
                        exp.Status = TypeNodeStatus.FunctionExpWrongType;
                        return (trees, string.Format(TYPE_FUNC_RET_TYPE_NOT_VALD, exp.ExpName));
                    }
                }
                else
                {
                    exp.TypeNode = firstType.GetArrayNode() ?? await context.GetNamespaceNodeAsync("system.array");
                }
            }
            else if (funcNode.ReturnNode != null)
            {
                if (funcNode.ReturnNode is RefArgTypeNode refType)
                {
                    ArgSchema refArg = exp.Args[refType.UseArgType - 1];
                    while (refArg.TypeNode is RefArgTypeNode n)
                    {
                        refArg = exp.Args[n.UseArgType - 1];
                    }
                    if (refArg.TypeNode != null)
                    {
                        if (exp.TypeNode != null)
                        {
                            // Gets the exp type
                            NamespaceNode expTypeNode = exp.TypeNode;
                            if ((exp.CallType is ExpressionType.Map or ExpressionType.Filter) && expTypeNode is ArrayNode array)
                                expTypeNode = array.BaseNode;

                            // Match type
                            if (!expTypeNode.CanBeUseAs(refArg.TypeNode))
                            {
                                // Downgrade
                                if (refArg.TypeNode.CanBeUseAs(expTypeNode) &&
                                    (isInfers.Contains(refArg) || (upgradeArg.Contains(refArg) &&
                                        (!baseTypeOfUpgradeArg.ContainsKey(refArg) || refArg.TypeNode.CanBeUseAs(baseTypeOfUpgradeArg[refArg])))))
                                {
                                    refArg.TypeNode = expTypeNode;
                                }
                                else
                                {
                                    exp.Status = TypeNodeStatus.FunctionExpWrongType;
                                    return (trees, string.Format(TYPE_FUNC_RET_TYPE_NOT_VALD, name));
                                }
                            }
                        }
                        else
                        {
                            exp.TypeNode = refArg.TypeNode;
                        }
                    }
                    else if (exp.TypeNode != null)
                    {
                        // Any Type
                        refArg.TypeNode = (exp.CallType is ExpressionType.Map or ExpressionType.Filter) && exp.TypeNode is ArrayNode array ? array.BaseNode : exp.TypeNode;
                    }
                    else
                    {
                        exp.Status = TypeNodeStatus.FunctionExpWrongType;
                        return (trees, string.Format(TYPE_FUNC_RET_TYPE_NOT_VALD, exp.ExpName));
                    }
                }
                else
                {
                    if (exp.TypeNode != null)
                    {
                        // Gets the exp type
                        NamespaceNode expTypeNode = exp.TypeNode;
                        if ((exp.CallType is ExpressionType.Map or ExpressionType.Filter) && expTypeNode is ArrayNode array)
                            expTypeNode = array.BaseNode;

                        // Match the type
                        if (!funcNode.ReturnNode.CanBeUseAs(expTypeNode))
                        {
                            exp.Status = TypeNodeStatus.FunctionExpWrongType;
                            return (trees, string.Format(TYPE_FUNC_RET_TYPE_NOT_VALD, exp.ExpName));
                        }
                    }
                    else
                    {
                        exp.TypeNode = funcNode.ReturnNode;
                    }
                }

                // Check if need convert the expTypeNode to array
                if ((exp.CallType is ExpressionType.Map or ExpressionType.Filter) && exp.TypeNode is not ArrayNode)
                {
                    exp.TypeNode = exp.TypeNode.GetArrayNode();
                    if (exp.TypeNode == null)
                    {
                        exp.Status = TypeNodeStatus.FunctionExpWrongType;
                        return (trees, string.Format(TYPE_FUNC_RET_TYPE_HAS_NOARRAY, exp.ExpName));
                    }
                }
            }
            else if (exp.TypeNode == null)
            {
                // No use but cover the case
                exp.Status = TypeNodeStatus.FunctionExpWrongFunc;
                return (trees, string.Format(TYPE_FUNC_EXP_FUNC_NOT_VALID, name));
            }

            // Check Map & Filter
            if ((exp.CallType is ExpressionType.Map or ExpressionType.Filter) && exp.TypeNode is not ArrayNode)
            {
                exp.TypeNode = exp.TypeNode.GetArrayNode();
                if (exp.TypeNode == null)
                    return (trees, string.Format(TYPE_FUNC_EXP_FUNC_NOT_VALID, name));
            }

            // Validate all arguments type node again
            if (funcNode.Args is { Count: > 0 })
            {
                for (int i = 0; i < funcNode.Args.Count; i++)
                {
                    // Prepare the argument
                    if (exp.Args[i].TypeNode != null) continue;
                    exp.Status = TypeNodeStatus.FunctionExpWrongFuncArgs;
                    return (trees, string.Format(TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL, exp.ExpName, $"第{i + 1}个"));
                }
            }

            // Add to the tree
            treeMap[exp.ExpName] = exp;

            // Record the array index
            if (isMapReduce)
                exp.ArrayIndex = arrayArg;
        }

        // Validate the function return
        StructResultExpNode structResultNode = null;
        if (returnNode is RefArgTypeNode refReturn)
        {
            // Check argument index
            if (refReturn.UseArgType < 1 || refReturn.UseArgType > (args?.Count ?? 0))
                return (trees, TYPE_FUNC_RET_REF_NOT_VALID);

            // Gets the type
            FunctionNodeArgument refArg = args[refReturn.UseArgType - 1];
            while (refArg?.TypeNode is RefArgTypeNode refArgType)
                refArg = args[refArgType.UseArgType - 1];

            // Validate the final expression type
            if (refArg?.TypeNode == null || !exps.Last().TypeNode.CanBeUseAs(refArg.TypeNode))
                return (trees, TYPE_FUNC_RET_REF_NOT_VALID);
        }
        else if (returnNode != null && !exps.Last().TypeNode.CanBeUseAs(returnNode))
        {
            if (returnNode is StructNode { Fields: { Count: > 0 } } @struct)
            {
                // Check the struct fields
                List<FunctionNodeExpTree> leafNodes = new(); // leaf nodes
                foreach (StructNodeField field in @struct.Fields.Where(field => treeMap.ContainsKey(field.Name)))
                {
                    if (!treeMap[field.Name].TypeNode.CanBeUseAs(field.TypeNode))
                        return (trees, string.Format(TYPE_FUNC_RET_STRUCT_MEMBER_NOT_VALID, field.Name));
                    leafNodes.Add(treeMap[field.Name]);
                }

                // Mark all leaf nodes as require
                leafNodes.ForEach(l => l.Used = 0);

                // Add struct result as the last exp tree
                structResultNode = new StructResultExpNode
                {
                    TypeNode = @struct,
                    LeafNodes = leafNodes
                };
            }
            else
            {
                return (trees, TYPE_FUNC_RET_REF_NOT_VALID);
            }
        }

        // Reduce the trees, only the result and non-one-time-used exp will be added to the tree
        trees = exps.Where(e => e.Used is > 1 or 0).Select(p => (FunctionNodeExpTree)p).ToList();
        if (structResultNode != null) trees.Add(structResultNode);
        return (trees, null);
    }

    #endregion

    #region Complie

    #region Register System Functions

    /// <summary>
    /// Register all datadict function and its namespace
    /// </summary>
    public static bool RegisterStaticMethod(Type type, MethodInfo method)
    {
        // Scan system functions
        DataDictFuncContainerAttribute container = (DataDictFuncContainerAttribute)type.GetCustomAttribute(typeof(DataDictFuncContainerAttribute));
        if (container == null || !method.IsStatic) return false;

        // Check function attribute
        DataDictFuncAttribute funcAttr = (DataDictFuncAttribute)method.GetCustomAttribute(typeof(DataDictFuncAttribute));
        if (funcAttr == null) return false;

        string funcName = $"{container.Namespace}.{method.Name.ToLowerInvariant()}";
        int sign = FUNC_SIGN_IMMUTABLE; // The system method won't be changed
        
        // Generate the arguments and result type
        Type[] genTypes = method.GetGenericArguments();
        ParameterInfo[] parameters = method.GetParameters();
        int[] genMap = new int[genTypes.Length]; // The generic type map to the arguments

        // Skip the first mysql parameter
        if (parameters.Length > 0 && (parameters[0].ParameterType == typeof(SchemaContext) || parameters[0].ParameterType.IsSubclassOf(typeof(SchemaContext))))
        {
            sign |= FUNC_SIGN_CONTEXT;
            parameters = parameters.Skip(1).ToArray();
        }

        // Check if return type is Task
        Type retType = method.ReturnType;
        if (retType.IsGenericType && retType.BaseType == typeof(Task))
        {
            sign |= FUNC_SIGN_ASYNC;
        }

        // Generate the function declaration
        if (method.IsGenericMethodDefinition)
        {
            sign |= FUNC_SIGN_GENERIC;

            // Check the return type and argument type
            Dictionary<string, int> typeRel = new();
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i]; 

                // Check nullable
                Type t = p.ParameterType;
                bool isNullable = t.IsSubclassOfGenericType(typeof(Nullable<>));
                if (isNullable) t = t.GetGenericArguments()[0];
                
                // Check dynamic type
                if (genTypes.Any(g => g.Name == t.Name))
                {
                    if (!typeRel.ContainsKey(t.Name))
                    {
                        typeRel[t.Name] = i + 1;
                        for (int j = 0; j < genTypes.Length; j++)
                        {
                            if (genTypes[j].Name == t.Name)
                            {
                                genMap[j] = i + 1;
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        // Save the method info to cache
        staticMethodMap.TryAdd(funcName, new SchemaFuncInfo
        {
            Name = funcName,
            Method = method,
            EnableNullableReturn = funcAttr.EnableNullableReturn,
            ClientOnly = funcAttr.ClientOnly,
            Sign = sign,
            GenericTypeMap = genMap
        });

        return true;
    }

    /// <summary>
    /// Whether the function is generic
    /// </summary>
    public bool IsGeneric => staticMethodMap.ContainsKey(Name) && FUNC_SIGN_GENERIC == (FUNC_SIGN_GENERIC & staticMethodMap[Name].Sign);

    /// <summary>
    /// Get the generic map
    /// </summary>
    public int[] GetGenericMap() => IsGeneric ? staticMethodMap[Name].GenericTypeMap : Array.Empty<int>();

    /// <summary>
    /// Whether the function is system defined
    /// </summary>
    public bool IsSystemDefined => staticMethodMap.ContainsKey(Name) || !SchemaContext.IsPublicService && Name.StartsWith($"{NS_SYSTEM}.");

    /// <summary>
    /// Whether the function is system defined
    /// </summary>
    public static bool IsMethodSystemDefined(string name) => staticMethodMap.ContainsKey(name) || !SchemaContext.IsPublicService && name.StartsWith($"{NS_SYSTEM}.");

    #endregion

    #region Compile Custom Functions

    /// <summary>
    /// Complie a custom function node to dynamic method
    /// </summary>
    SchemaFuncInfo CompileFunction()
    {
        // Only fullfilled function can be complied
        if (Status != TypeNodeStatus.Fullfill)
            throw new Exception($"The {Name} can't be compiled because of {Status}");

        // Server call only
        if (IsServerOnly)
        {
            return new SchemaFuncInfo
            {
                Name = Name,
                Sign = FUNC_SIGN_CONTEXT | FUNC_SIGN_SERVERCALL | FUNC_SIGN_ASYNC,
                FunctionNode = this,
            };
        }

        // Compile the dynamic function node
        try
        {
            // Prepare
            Dictionary<string, Expression> paramExpMap = new();
            Dictionary<string, ParameterExpression> variableExpMap = new();
            ParameterExpression[] paramExps = new ParameterExpression[Args.Count + 1];

            // Build the parameters, no generic type for custom methods
            paramExps[0] = Expression.Parameter(typeof(SchemaContext)); // Add SchemaContext as the first parameters
            for (int i = 0; i < Args.Count; i++)
            {
                FunctionNodeArgument arg = Args[i];
                ParameterExpression paramExp = Expression.Parameter(arg.UseArgType.HasValue ? paramExps[arg.UseArgType.Value].Type : GetTypeByNode(arg.TypeNode, arg.Nullable ?? false));
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
                string expName = exp is FunctionNodeExpression nodeExp ? nodeExp.ExpName : $"_expResult{++expCount}";
                ParameterExpression expRes = Expression.Parameter(result.Type);
                variableExpMap.Add(expName, expRes);
                expBlocks.Add(Expression.Assign(expRes, result));
            }

            // Conversion last type
            // Gets the function and expression
            Type lastType = GetTypeByNode(ReturnNode is RefArgTypeNode refRetNode ? Args[refRetNode.UseArgType - 1].TypeNode : ReturnNode);
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
            Delegate dynamicMethod = ComplieMethod(lastType, paramExps, blockExpr);

            // Build the function info
            return new SchemaFuncInfo
            {
                Name = Name,
                Sign = FUNC_SIGN_CONTEXT,
                FunctionNode = this,
                Method = dynamicMethod!.Method,
                DynamicMethod = dynamicMethod
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    // Complie the method to delegate
    static Delegate ComplieMethod(Type retType, IReadOnlyList<ParameterExpression> paramExps, BlockExpression blockExpr)
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
            .Invoke(null, new object[] { blockExpr, paramExps });
    }
    
    /// <summary>
    /// Compile a function node expression to expression
    /// </summary>
    static Expression CompileFunctionNodeExpression(Expression mySqlExp, IReadOnlyDictionary<string, Expression> paramMap, Dictionary<string, ParameterExpression> expMap, List<Expression> blocks, FunctionNodeExpTree expTree)
    {
        switch (expTree)
        {
            case FunctionNodeExpression exp:
                {
                    SchemaFuncInfo callFuncInfo = exp.FunctionNode.GetSchemaFuncInfo();
                    int useContext = (callFuncInfo.Sign & FUNC_SIGN_CONTEXT) == FUNC_SIGN_CONTEXT ? 1 : 0;

                    // Prepare the call arguments
                    int arrayIndex = -1;
                    Expression[] callArgs = new Expression[exp.LeafNodes.Count + useContext];
                    Type[] callArgTypes = new Type[exp.LeafNodes.Count + useContext];
                    if (useContext > 0)
                    {
                        callArgs[0] = mySqlExp;
                        callArgTypes[0] = typeof(SchemaContext);
                    }

                    // Add leaf nodes
                    for (int i = 0; i < exp.LeafNodes.Count; i++)
                    {
                        // Gets the type
                        bool isNullable = exp.FunctionNode.Args[i].Nullable ?? false;
                        Type callType = exp.Args[i].TypeNode is RefArgTypeNode refArg
                            ? GetTypeWithNullable(callArgTypes[refArg.UseArgType - 1 + useContext], isNullable)
                            : GetTypeByNode(exp.Args[i].TypeNode, isNullable);

                        // Build the call arguments
                        switch (exp.LeafNodes[i])
                        {
                            case ConstantExpNode constExp:
                                if (constExp.Value == null && !IsNullType(callType))
                                {
                                    // For reduce
                                    callArgs[i + useContext] = Expression.Default(callType);
                                }
                                else
                                {
                                    object value = constExp.Value;
                                    if (value != null)
                                    {
                                        if (callType == typeof(int))
                                        {
                                            value = Convert.ToInt32(value);
                                        }
                                        else if (callType == typeof(long))
                                        {
                                            value = Convert.ToInt64(value);
                                        }
                                        else if (callType == typeof(float))
                                        {
                                            value = Convert.ToSingle(value);
                                        }
                                        else if (callType == typeof(double))
                                        {
                                            value = Convert.ToDouble(value);
                                        }
                                        else if (callType == typeof(decimal))
                                        {
                                            value = Convert.ToDecimal(value);
                                        }
                                        else if (callType == typeof(string))
                                        {
                                            value = Convert.ToString(value);
                                        }
                                        else if (callType == typeof(bool))
                                        {
                                            value = Convert.ToBoolean(value);
                                        }
                                        else if (callType == typeof(DateTime))
                                        {
                                            value = Convert.ToDateTime(value);
                                        }
                                    }
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
                                    callArgs[i + useContext] = CompileFunctionNodeExpression(mySqlExp, paramMap, expMap, blocks, otherExp);
                                }
                                else
                                {
                                    // Use variable expression
                                    callArgs[i + useContext] = expMap[otherExp.ExpName];
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
                            callArgTypes[i + useContext] = exp.LeafNodes[i].TypeNode is ArrayNode arr ? GetTypeByNode(arr.BaseNode) : callType;
                        }
                    }

                    // Call the functions
                    MethodInfo callMethod = callFuncInfo.Method;

                    // Make generic method for system defined methods
                    if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE && (callFuncInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
                    {
                        Type retType = exp.TypeNode is RefArgTypeNode refRetNode ? callArgTypes[refRetNode.UseArgType] : GetTypeByNode(exp.TypeNode, callFuncInfo.EnableNullableReturn);
                        // Check Map return type
                        if ((exp.CallType is ExpressionType.Map or ExpressionType.Filter) && retType == typeof(JArray) && exp.TypeNode is ArrayNode arr)
                            retType = GetTypeByNode(arr.BaseNode);

                        // Generate the generic method
                        Type[] genTypes = callFuncInfo.GenericTypeMap.Select(p => p > 0 ? GetNotNullType(callArgTypes[p - 1 + useContext]) : retType).ToArray();
                        string genSign = string.Join('|', genTypes.Select(p => p.IsSubclassOfGenericType(typeof(Nullable<>)) ? $"{p.GetGenericArguments()[0].Name}?" : p.Name));
                        callMethod = callFuncInfo.GenericMethods.GetOrAdd(genSign, _ => callFuncInfo.Method.MakeGenericMethod(genTypes));
                    }
                    // Use server call
                    else if ((callFuncInfo.Sign & FUNC_SIGN_SERVERCALL) == FUNC_SIGN_SERVERCALL)
                    {
                        Type retType = exp.TypeNode is RefArgTypeNode refRetNode ? callArgTypes[refRetNode.UseArgType] : GetTypeByNode(exp.TypeNode, callFuncInfo.EnableNullableReturn);
                        // Check Map return type
                        if ((exp.CallType is ExpressionType.Map or ExpressionType.Filter) && retType == typeof(JArray) && exp.TypeNode is ArrayNode arr)
                            retType = GetTypeByNode(arr.BaseNode);

                        // Genreate the call
                        Expression[] convCallArgs = new Expression[callArgs.Length + 2];
                        convCallArgs[0] = callArgs[0];
                        convCallArgs[1] = Expression.Constant(callFuncInfo.Name);
                        convCallArgs[2] = Expression.Constant(exp.Type);
                        for (int i = 1; i < callArgs.Length; i++)
                            convCallArgs[i + 2] = callArgs[i];
                        callArgs = convCallArgs;
                        if (arrayIndex >= 0) arrayIndex += 2;
                        int count = callArgs.Length - 3;
                        callMethod = typeof(FunctionNode).GetMethod($"CallServerFunction{count}", BindingFlags.Static | BindingFlags.NonPublic);

                        // Make generic type
                        if (count > 0)
                            callMethod = callMethod.MakeGenericMethod(callArgs.Skip(3).Select(e => e.Type).Prepend(retType).ToArray());
                    }

                    // Generate call body
                    if (exp.CallType == ExpressionType.Call)
                        return GenMethodCallExp(callFuncInfo, callMethod, callArgs);

                    // Generate the lambda for collection operations
                    Type callMethodReturn = callMethod.ReturnType;
                    if (callMethodReturn.IsSubclassOfGenericType(typeof(Task<>)))
                        callMethodReturn = callMethodReturn.GetGenericArguments()[0];

                    // Parameters
                    ParameterExpression[] innerParams = callArgs.Select(p => Expression.Parameter(p.Type)).ToArray();
                    Expression[] innerCallArgs = innerParams.Select(p => (Expression)p).ToArray();
                    Expression jarray = innerCallArgs[arrayIndex];
                    ParameterExpression varExp = Expression.Parameter(exp.CallType switch
                    {
                        ExpressionType.Map => typeof(JArray),
                        ExpressionType.Reduce => callMethodReturn,
                        ExpressionType.First => callArgTypes[arrayIndex],
                        ExpressionType.Last => callArgTypes[arrayIndex],
                        ExpressionType.Filter => typeof(JArray),
                        _ => throw new ArgumentOutOfRangeException()
                    });
                    ParameterExpression start = Expression.Parameter(typeof(int), "_start");
                    ParameterExpression stop = Expression.Parameter(typeof(int), "_stop");
                    LabelTarget forLabel = Expression.Label(typeof(int));
                    ParameterExpression array = Expression.Parameter(typeof(JToken[]), "_array");
                    ParameterExpression final = Expression.Parameter(varExp.Type, "_final");

                    // Convert the call argument
                    if (callArgTypes[arrayIndex] == typeof(JObject))
                    {
                        // (JObject)array[start++]
                        innerCallArgs[arrayIndex] = Expression.Convert(Expression.ArrayIndex(array, exp.CallType == ExpressionType.Last ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start)), typeof(JObject));
                    }
                    else
                    {
                        // JValue.ToObject<T>(array[start++])
                        innerCallArgs[arrayIndex] = Expression.Call(Expression.ArrayIndex(array, exp.CallType == ExpressionType.Last ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start)), typeof(JValue).GetMethod(nameof(JValue.ToObject), System.Type.EmptyTypes).MakeGenericMethod(callArgTypes[arrayIndex]));

                        // Conversion
                        Type ctype = callMethod.GetParameters()[arrayIndex].ParameterType;
                        innerCallArgs[arrayIndex] = ConvertExp(ctype, innerCallArgs[arrayIndex]);
                        callArgTypes[arrayIndex] = innerCallArgs[arrayIndex].Type;
                    }

                    // Generate call body
                    Delegate innerCall;

                    switch (exp.CallType)
                    {
                        // Map the element
                        case ExpressionType.Map:
                            {
                                innerCall = ComplieMethod(varExp.Type, innerParams, Expression.Block(
                                    new[] { varExp, start, stop, array, final },
                                    Expression.Assign(varExp, Expression.New(typeof(JArray))),
                                    Expression.Assign(array, Expression.Call(null, typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(JToken)), jarray)),
                                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                    Expression.Assign(stop, Expression.ArrayLength(array)),
                                    Expression.Loop(
                                        Expression.IfThenElse(
                                            Expression.LessThan(start, stop),
                                            Expression.Call(varExp, typeof(JArray).GetMethod(nameof(JArray.Add), new[] { typeof(object) }), Expression.Convert(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs), typeof(object))),
                                            Expression.Break(forLabel, stop)
                                        ),
                                        forLabel
                                    ),
                                    Expression.Assign(final, varExp)
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
                                    Expression.GreaterThan(Expression.ArrayLength(array), Expression.Constant(0)),
                                    innerCallArgs[arrayIndex],
                                    callMethod.ReturnType == typeof(JObject) ? Expression.New(typeof(JObject)) : callMethod.ReturnType == typeof(JArray) ? Expression.New(typeof(JArray)) : Expression.Default(callMethod.ReturnType)
                                );

                                // Replace the sum exp
                                innerCallArgs[sumIndex] = varExp;

                                // Complie
                                innerCall = ComplieMethod(varExp.Type, innerParams, Expression.Block(
                                    new[] { varExp, start, stop, array, final },
                                    Expression.Assign(array, Expression.Call(null, typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(JToken)), jarray)),
                                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                    Expression.Assign(stop, Expression.ArrayLength(array)),
                                    Expression.Assign(varExp, init),
                                    Expression.Loop(
                                        Expression.IfThenElse(
                                            Expression.LessThan(start, stop),
                                            Expression.Assign(varExp, GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs)),
                                            Expression.Break(forLabel, stop)
                                        ),
                                        forLabel
                                    ),
                                    Expression.Assign(final, varExp)
                                ));
                                break;
                            }

                        // First match
                        case ExpressionType.First:
                            {
                                // Replace the call args
                                Expression temp = innerCallArgs[arrayIndex];
                                innerCallArgs[arrayIndex] = varExp;

                                // New init parameter
                                ParameterExpression init = Expression.Parameter(varExp.Type, "_init");

                                // Complie
                                innerCall = ComplieMethod(varExp.Type, innerParams, Expression.Block(
                                    new[] { varExp, start, stop, array, init, final },
                                    Expression.Assign(array, Expression.Call(null, typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(JToken)), jarray)),
                                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                    Expression.Assign(stop, Expression.ArrayLength(array)),
                                    Expression.Assign(init, varExp.Type == typeof(JObject) ? Expression.New(typeof(JObject)) : varExp.Type == typeof(JArray) ? Expression.New(typeof(JArray)) : Expression.Default(callArgTypes[arrayIndex])),
                                    Expression.Assign(varExp, init),
                                    Expression.Loop(
                                        Expression.IfThenElse(
                                            Expression.LessThan(start, stop),
                                            Expression.Block(new List<Expression>()
                                            {
                                        Expression.Assign(varExp, temp),
                                        Expression.IfThenElse(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs), Expression.Break(forLabel, stop), Expression.Assign(varExp, init))
                                            }),
                                            Expression.Break(forLabel, stop)
                                        ),
                                        forLabel
                                    ),
                                    Expression.Assign(final, varExp)
                                ));
                                break;
                            }

                        // Last match
                        case ExpressionType.Last:
                            {
                                // Replace the call args
                                Expression temp = innerCallArgs[arrayIndex];
                                innerCallArgs[arrayIndex] = varExp;

                                // New init parameter
                                ParameterExpression init = Expression.Parameter(varExp.Type, "_init");

                                // Complie
                                innerCall = ComplieMethod(varExp.Type, innerParams, Expression.Block(
                                    new[] { varExp, start, stop, array, init, final },
                                    Expression.Assign(array, Expression.Call(null, typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(JToken)), jarray)),
                                    Expression.Assign(stop, Expression.Constant(0, typeof(int))),
                                    Expression.Assign(start, Expression.ArrayLength(array)),
                                    Expression.Assign(init, varExp.Type == typeof(JObject) ? Expression.New(typeof(JObject)) : varExp.Type == typeof(JArray) ? Expression.New(typeof(JArray)) : Expression.Default(callArgTypes[arrayIndex])),
                                    Expression.Assign(varExp, init),
                                    Expression.Loop(
                                        Expression.IfThenElse(
                                            Expression.GreaterThan(start, stop),
                                            Expression.Block(new List<Expression>()
                                            {
                                        Expression.Assign(varExp, temp),
                                        Expression.IfThenElse(GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs), Expression.Break(forLabel, stop), Expression.Assign(varExp, init))
                                            }),
                                            Expression.Break(forLabel, stop)
                                        ),
                                        forLabel
                                    ),
                                    Expression.Assign(final, varExp)
                                ));
                                break;
                            }

                        // Filter the elements
                        case ExpressionType.Filter:
                            {
                                Expression temp = innerCallArgs[arrayIndex];
                                ParameterExpression curr = Expression.Parameter(temp.Type, "_curr");
                                innerCallArgs[arrayIndex] = curr;

                                innerCall = ComplieMethod(varExp.Type, innerParams, Expression.Block(
                                    new[] { varExp, start, stop, array, final, curr },
                                    Expression.Assign(varExp, Expression.New(typeof(JArray))),
                                    Expression.Assign(array, Expression.Call(null, typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(JToken)), jarray)),
                                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                                    Expression.Assign(stop, Expression.ArrayLength(array)),
                                    Expression.Loop(
                                        Expression.IfThenElse(
                                            Expression.LessThan(start, stop),
                                            Expression.Block(new List<Expression>()
                                            {
                                        Expression.Assign(curr, temp),
                                        Expression.IfThen(
                                            GenMethodCallExp(callFuncInfo, callMethod, innerCallArgs),
                                            Expression.Call(varExp, typeof(JArray).GetMethod(nameof(JArray.Add), new[] { typeof(object) }), Expression.Convert(curr, typeof(object)))
                                        )
                                            }),
                                            Expression.Break(forLabel, stop)
                                        ),
                                        forLabel
                                    ),
                                    Expression.Assign(final, varExp)
                                ));
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    // Gets the call
                    return GenDynamicCallExp(varExp.Type, innerCall, callArgs);
                }
            // Generate the result
            case StructResultExpNode strt:
                {
                    // Only one struct result can exist
                    ParameterExpression resultVar = Expression.Parameter(typeof(JObject));
                    expMap.Add("_retobject", resultVar);
                    blocks.Add(Expression.Assign(resultVar, Expression.New(typeof(JObject).GetConstructor(System.Type.EmptyTypes)!)));
                    MethodInfo objectAdd = typeof(JObject).GetMethod(nameof(JObject.Add), new[] { typeof(string), typeof(JToken) })!;

                    // Build the JObject result
                    foreach (FunctionNodeExpTree leafNode in strt.LeafNodes)
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
                                name = leafExp.ExpName;
                                memberExp = expMap[name];
                                break;
                            default:
                                continue; // won't hit
                        }

                        // Gets the struct member
                        StructNodeField fld = ((StructNode)strt.TypeNode)?.Fields?.FirstOrDefault(p => p.Name == name);
                        if (fld == null) continue;
                        Type fldType = GetTypeByToken(fld.TypeNode.Token, !fld.Require);
                        memberExp = ConvertExp(fldType, memberExp);

                        // Build the exp
                        if (IsNullType(memberExp.Type))
                        {
                            blocks.Add(Expression.IfThen(
                                Expression.Property(memberExp, memberExp.Type.GetProperty("HasValue")!),
                                Expression.Call(resultVar, objectAdd, new Expression[] { Expression.Constant(name, typeof(string)), Expression.New(typeof(JValue).GetConstructor(new[] { GetNotNullType(memberExp.Type) }), Expression.Property(memberExp, memberExp.Type.GetProperty("Value"))) })
                            ));
                        }
                        else if (memberExp.Type == typeof(JArray) || memberExp.Type == typeof(JObject))
                        {
                            blocks.Add(Expression.IfThen(
                                Expression.NotEqual(memberExp, Expression.Constant(null)),
                                // ReSharper disable once RedundantExplicitArrayCreation
                                Expression.Call(resultVar, objectAdd, new Expression[] { Expression.Constant(name, typeof(string)), memberExp })
                            ));
                        }
                        else
                        {
                            blocks.Add(Expression.Call(resultVar, objectAdd, new Expression[] { Expression.Constant(name, typeof(string)), Expression.New(typeof(JValue).GetConstructor(new[] { GetNotNullType(memberExp.Type) }), memberExp) }));
                        }
                    }
                    return resultVar;
                }
        }
        return null;
    }

    // Convert expression
    static Expression ConvertExp(Type ctype, Expression exp)
    {
        if(ctype == typeof(object))
            return Expression.Convert(exp, ctype);

        Type expType = exp.Type;

        if (IsNullType(expType))
        {
            if (IsNullType(ctype))
            {
                return GetNotNullType(ctype) == GetNotNullType(expType) ? exp : Expression.Call(null, GetConvertNullableExp(ctype, expType), exp);
            }
            else
            {
                exp = Expression.Call(exp, expType.GetMethod("GetValueOrDefault", System.Type.EmptyTypes)!);
                expType = GetNotNullType(expType);
                if (ctype == expType) return exp;
            }
        }
        // @todo: more check
        else if (IsNullType(ctype))
        {
            return Expression.Convert(exp, ctype);
        }

        // Default
        ctype = GetNotNullType(ctype);
        if (ctype == expType) return exp;
        if (ctype == typeof(int))
        {
            return Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt32), new[] { exp.Type })!, exp);
        }
        else if (ctype == typeof(long))
        {
            return Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt64), new[] { exp.Type })!, exp);
        }
        else if (ctype == typeof(float))
        {
            return Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSingle), new[] { exp.Type })!, exp);
        }
        else if (ctype == typeof(double))
        {
            return Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDouble), new[] { exp.Type })!, exp);
        }
        else if (ctype == typeof(decimal))
        {
            return Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDecimal), new[] { exp.Type })!, exp);
        }
        return exp;
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
                $"{DATA_DICT_FUNC_ARTH_NS}.add" => Expression.Add(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_ARTH_NS}.subtract" => Expression.Subtract(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_ARTH_NS}.multiply" => Expression.Multiply(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_ARTH_NS}.divide" => Expression.Divide(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_ARTH_NS}.modulo" => Expression.Modulo(callArgs[0], callArgs[1]),

                // system.conv
                $"{DATA_DICT_FUNC_CONV_NS}.assign" => callArgs[0],
                $"{DATA_DICT_FUNC_CONV_NS}.null" => Expression.Constant(null,GetTypeWithNullable(callMethod.ReturnType, true)),

                // system.logic
                $"{DATA_DICT_FUNC_LOGIC_NS}.isnull" => Expression.Call(null, callMethod, Expression.Convert(callArgs[0], typeof(object))),
                $"{DATA_DICT_FUNC_LOGIC_NS}.condition" => Expression.Condition(callArgs[0], callArgs[1], callArgs[2]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.lessthan" => Expression.LessThan(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.lessequal" => Expression.LessThanOrEqual(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.equal" => Expression.Equal(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.notequal" => Expression.NotEqual(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.greatethan" => Expression.GreaterThan(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.greateequal" => Expression.GreaterThanOrEqual(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.andalso" => Expression.AndAlso(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.orelse" => Expression.OrElse(callArgs[0], callArgs[1]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.not" => Expression.Not(callArgs[0]),
                $"{DATA_DICT_FUNC_LOGIC_NS}.between" => Expression.AndAlso( // (value, min, max, includeMin, includeMax)
                    Expression.Condition(callArgs[3], Expression.GreaterThanOrEqual(callArgs[0], callArgs[1]), Expression.GreaterThan(callArgs[0], callArgs[1])),
                    Expression.Condition(callArgs[4], Expression.LessThanOrEqual(callArgs[0], callArgs[2]), Expression.LessThan(callArgs[0], callArgs[2]))
                    ),

                // default
                _ => Expression.Call(null, callMethod, callArgs)
            };
        }
        else
        {
            return GenDynamicCallExp(callMethod.ReturnType, callFuncInfo.DynamicMethod, callArgs);
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
    static TR CallDynamicFunc1<TR, T1>(Delegate method, T1 arg1)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc2<TR, T1, T2>(Delegate method, T1 arg1, T2 arg2)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc3<TR, T1, T2, T3>(Delegate method, T1 arg1, T2 arg2, T3 arg3)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc4<TR, T1, T2, T3, T4>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc5<TR, T1, T2, T3, T4, T5>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc6<TR, T1, T2, T3, T4, T5, T6>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc7<TR, T1, T2, T3, T4, T5, T6, T7>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16 });
        return result == null ? default : (TR)result;
    }
    static TR CallDynamicFunc17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16, T17 arg17)
    {
        // Invoke the dynamic method
        object result = ((dynamic)method).DynamicInvoke(new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17 });
        return result == null ? default : (TR)result;
    }
    #endregion

    #region Call Server Func

    static TR GetResult<TR>(JToken token)
    {
        Type tr = typeof(TR);
        bool isNullable = tr.IsSubclassOfGenericType(typeof(Nullable<>));
        tr = isNullable ? tr.GetGenericArguments()[0] : tr;
        if (tr == typeof(JArray))
        {
            return token is JArray arr ? (TR)(object)arr : isNullable ? (TR)(object)null : default;
        }
        else if (tr == typeof(JObject))
        {
            return token is JObject obj ? (TR) (object) obj : isNullable ? (TR)(object)null : default;
        }
        else if (token is JValue val)
        {
            return tr == typeof(JValue) ? (TR)(object)val : val.Value<TR>();
        }
        return isNullable ? (TR)(object)null : default;
    }
    
    /// <summary>
    /// Call the data dict function with arguments
    /// </summary>
    static async Task<TR> CallServerFunction0<TR>(SchemaContext context, string name, string retType) => GetResult<TR>(await context.CallFunction(name, new JArray(), retType));
    static async Task<TR> CallServerFunction1<TR, T1>(SchemaContext context, string name, string retType, T1 v1) => GetResult<TR>(await context.CallFunction(name, new JArray { v1 }, retType));
    static async Task<TR> CallServerFunction2<TR, T1, T2>(SchemaContext context, string name, string retType, T1 v1, T2 v2) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2 }, retType));
    static async Task<TR> CallServerFunction3<TR, T1, T2, T3>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3 }, retType));
    static async Task<TR> CallServerFunction4<TR, T1, T2, T3, T4>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4 }, retType));
    static async Task<TR> CallServerFunction5<TR, T1, T2, T3, T4, T5>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5 }, retType));
    static async Task<TR> CallServerFunction6<TR, T1, T2, T3, T4, T5, T6>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6 }, retType));
    static async Task<TR> CallServerFunction7<TR, T1, T2, T3, T4, T5, T6, T7>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7 }, retType));
    static async Task<TR> CallServerFunction8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8 }, retType));
    static async Task<TR> CallServerFunction9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9 }, retType));
    static async Task<TR> CallServerFunction10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10 }, retType));
    static async Task<TR> CallServerFunction11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11 }, retType));
    static async Task<TR> CallServerFunction12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12 }, retType));
    static async Task<TR> CallServerFunction13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13 }, retType));
    static async Task<TR> CallServerFunction14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14 }, retType));
    static async Task<TR> CallServerFunction15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15 }, retType));
    static async Task<TR> CallServerFunction16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16 }, retType));
    static async Task<TR> CallServerFunction17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(SchemaContext context, string name, string retType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16, T17 v17) => GetResult<TR>(await context.CallFunction(name, new JArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16, v17 }, retType));
    
    #endregion

    // Nullable exp conversion
    static T1 ConvertNullableExp<T1, T2>(T2 input)
    {
        if (input == null) return default;
        Type retType = GetNotNullType(typeof(T1));
        if (retType == typeof(int))
        {
            return (T1)(object)(Convert.ToInt32(input));
        }
        else if (retType == typeof(long))
        {
            return (T1)(object)(Convert.ToInt64(input));
        }
        else if (retType == typeof(float))
        {
            return (T1)(object)(Convert.ToSingle(input));
        }
        else if (retType == typeof(double))
        {
            return (T1)(object)(Convert.ToDouble(input));
        }
        else if (retType == typeof(decimal))
        {
            return (T1)(object)(Convert.ToDecimal(input));
        }
        else
            return (T1)(object)input;
    }

    /// <summary>
    /// Gets the info from data dict func
    /// </summary>
    public SchemaFuncInfo GetSchemaFuncInfo()
    {
        if (FuncInfo != null)
            return FuncInfo;

        // Check is static
        if (staticMethodMap.TryGetValue(Name, out SchemaFuncInfo result) && (result.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result.FunctionNode = this;
            FuncInfo = result;
            return result;
        }

        // Compile
        FuncInfo = CompileFunction();
        return FuncInfo;
    }

    // Gets the convert nullable exp
    static MethodInfo GetConvertNullableExp(Type ret, Type input) => callConvertNullableExp.GetOrAdd($"{GetNotNullType(ret).FullName}^{GetNotNullType(input).FullName}", _ => typeof(FunctionNode).GetMethod(nameof(ConvertNullableExp), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(ret, input));

    // Gets the call dynamic func
    static MethodInfo GetCallDynamicFunc(Type ret, params Type[] inputs)
    {
        MethodInfo method = typeof(FunctionNode).GetMethod($"CallDynamicFunc{inputs.Length}", BindingFlags.Static | BindingFlags.NonPublic);
        return method!.MakeGenericMethod(inputs.Prepend(ret).ToArray());
    }

    #endregion
    
    #region Utility

    // staitc mappings
    static readonly ConcurrentDictionary<string, SchemaFuncInfo> staticMethodMap = new();
    static readonly ConcurrentDictionary<string, MethodInfo> callConvertNullableExp = new();

    #endregion

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
    public List<FunctionNodeExpTree> LeafNodes { get; set; }

    /// <summary>
    /// The type node
    /// </summary>
    public NamespaceNode TypeNode { get; set; }

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
    public string Name { get; set; }

    /// <summary>
    /// The argument type
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Whether use previous argument type
    /// </summary>
    public int? UseArgType { get; set; }

    /// <summary>
    /// Whether nullable
    /// </summary>
    public bool? Nullable { get; set; }

    /// <summary>
    /// The description
    /// </summary>
    public string Desc { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public TypeNodeStatus Status { get; set; }

    #endregion

    #region Conversion

    /// <summary>
    /// Get function node argument from info
    /// </summary>
    public static implicit operator FunctionNodeArgument(FunctionArgSchema info)
    {
        if (info == null) return null;
        return new FunctionNodeArgument
        {
            Name = info.Name,
            Type = info.Type,
            UseArgType = info.UseArgType,
            Nullable = info.Nullable,
            Desc = info.Desc,
        };
    }

    /// <summary>
    /// Get funciton argument info from node
    /// </summary>
    public static explicit operator FunctionArgSchema(FunctionNodeArgument arg)
    {
        if (arg == null) return null;
        return new FunctionArgSchema
        {
            Name = arg.Name,
            Type = arg.Type,
            UseArgType = arg.UseArgType,
            Nullable = arg.Nullable,
            Desc = arg.Desc,
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
    public string ExpName { get; set; }

    /// <summary>
    /// The function to be called.
    /// </summary>
    public string Call { get; set; }

    /// <summary>
    /// The function used to map array elements
    /// </summary>
    public ExpressionType? CallType { get; set; } = ExpressionType.Call;

    /// <summary>
    /// The namespace.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Use Nth argument's type as the return type.
    /// </summary>
    public int? UseArgType { get; set; }

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public List<ArgSchema> Args { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public TypeNodeStatus Status { get; set; }

    /// <summary>
    /// The index of the array used for Map/Reduce/First
    /// </summary>
    public int? ArrayIndex { get; set; }

    #endregion

    #region Relationship

    /// <summary>
    /// The function node
    /// </summary>
    public FunctionNode FunctionNode { get; set; }

    #endregion

    #region Conversion

    /// <summary>
    /// Get function node argument from info
    /// </summary>
    public static implicit operator FunctionNodeExpression(FunctionExpSchema info)
    {
        if (info == null) return null;
        return new FunctionNodeExpression
        {
            ExpName = info.Name,
            Call = info.CallFunc,
            CallType = info.CallType,
            Type = info.Type,
            UseArgType = info.UseArgType,
            Args = info.Args,
        };
    }

    /// <summary>
    /// Convert the exp to schema
    /// </summary>
    /// <param name="exp"></param>
    public static explicit operator FunctionExpSchema(FunctionNodeExpression exp)
    {
        if(exp == null) return null;
        return new FunctionExpSchema
        {
            Name = exp.ExpName,
            CallType = exp.CallType ?? ExpressionType.Call,
            CallFunc = exp.Call,
            Type = exp.Type,
            UseArgType = exp.UseArgType,
            Args = exp.Args,
            ArrayIndex = exp.ArrayIndex
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
    public object Value { get; init; }
}


/// <summary>
/// The data dict func info
/// </summary>
public class SchemaFuncInfo
{
    /// <summary>
    /// The method name
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The method info
    /// </summary>
    public MethodInfo Method { get; init; }

    /// <summary>
    /// The dynamic method generated by expression
    /// </summary>
    public Delegate DynamicMethod { get; init; }

    /// <summary>
    /// The function node
    /// </summary>
    public FunctionNode FunctionNode { get; set; }

    /// <summary>
    /// Whether need wrap the return value as nullable
    /// </summary>
    public bool EnableNullableReturn { get; init; }

    /// <summary>
    /// Whether the func is client only
    /// </summary>
    public bool ClientOnly { get; init; }

    /// <summary>
    ///  The sign of the function
    /// </summary>
    public int Sign { get; init; }

    /// <summary>
    /// The generic type map
    /// </summary>
    public int[] GenericTypeMap { get; init; }

    /// <summary>
    /// The generic instances
    /// </summary>
    public ConcurrentDictionary<string, MethodInfo> GenericMethods { get; } = new();
}


public class GenericTypeNode: NamespaceNode
{
    /// <summary>
    /// Possible base type
    /// </summary>
    public NamespaceNode? BaseNode { get; set; }

    /// <summary>
    /// The index in generic array
    /// </summary>
    public int GenericIndex { get; set; }
}