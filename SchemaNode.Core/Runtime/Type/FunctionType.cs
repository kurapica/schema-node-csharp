using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Service;
using ExpType = SchemaNode.Enum.ExpType;
using JsonNode = System.Text.Json.Nodes.JsonNode;
using SchemaNode.Property.Function;
using SchemaNode.Schema.Provider;
using SchemaNode.Struct;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory function schema representation
/// </summary>
public sealed class FunctionType : NodeType
{
    #region Properties

    /// <summary>
    /// The return type node
    /// </summary>
    public ValueType Return { get; private set; } = null!;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FunctionNodeArgument[] Args { get; private set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FunctionNodeExpression[] Exps { get; private set; } = [];

    /// <summary>
    /// As type converter
    /// </summary>
    internal bool? Converter { get; private set; }

    /// <summary>
    /// The method info of the function if it's a system function
    /// </summary>
    internal MethodInfo? MethodInfo => FuncInfo?.Method;
    
    /// <summary>
    /// Whether the function require call server
    /// </summary>
    internal bool RequireRemoteCall { get; private set; }

    /// <summary>
    /// Whether the function is remote call only
    /// </summary>
    internal bool IsRemoteCall => (LoadState & SchemaLoadState.Remote) > 0;

    /// <summary>
    /// Whether the function is defined as system, direct call
    /// </summary>
    internal bool IsSystemCall => (LoadState & SchemaLoadState.System) > 0;

    /// <summary>
    /// The function info
    /// </summary>
    internal SchemaFuncInfo? FuncInfo { get; private set; }
    
    #endregion
    
    #region Methods

    /// <summary>
    /// Whether the function has the specific flags, like NoCache and etc
    /// </summary>
    public bool? HasFlag<T>() where T : Property<bool>
    {
        var prop = GetProperty<T>();
        if (prop != null) return prop.Value;

        foreach (FunctionNodeExpression exp in Exps)
            if (exp.FuncNode?.HasFlag<T>() is { } flags)
                return flags;

        return null;
    }
    
    #endregion
        
    #region Implementation
    
    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        FunctionSchema? func = GetProperty<FuncProperty>()?.Value;
        
        // Status
        if (func == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }

        // Return type
        ValueType? retType = !string.IsNullOrWhiteSpace(func.Return)
            ? await context.GetNodeTypeAsync<ValueType>(func.Return, Generics)
            : null;
        if (retType == null || retType is GenericType && !IsSystemCall)
        {
            Error = ErrorCodes.FUNC_WRONG_RETURN;
            return;
        }
        Return = retType;
        
        // Data
        Args = func.Args.Select(a => (FunctionNodeArgument)a).ToArray();
        Exps = func.Exps.Select(e => (FunctionNodeExpression)e).ToArray();

        Converter = func.GetProperty<Converter>()?.Value;
        FuncInfo = FunctionGenerator.GetSystemFuncInfo(Name);

        // Check if server or direct call
        RequireRemoteCall = IsRemoteCall;

        // Argument types
        HashSet<string> existNames = [];
        foreach (FunctionNodeArgument arg in Args)
        {
            if (string.IsNullOrWhiteSpace(arg.Name))
            {
                Error ??= ErrorCodes.FUNC_ARG_NO_NAME;
                return;
            }
            else if (!existNames.Add(arg.Name))
            {
                Error ??= ErrorCodes.FUNC_ARG_DUPLICATE_NAME;
                return;
            }

            arg.ValueType = !string.IsNullOrWhiteSpace(arg.Type) 
                ? await context.GetNodeTypeAsync<ValueType>(arg.Type, Generics)
                : null;

            if (arg.ValueType == null || arg.ValueType is GenericType && !IsSystemCall)
            {
                Error ??= ErrorCodes.FUNC_ARG_WRONG_TYPE;
                return;
            }
        }
        
        // Generate the exp trees
        await PreCompileAsync(context);
        
        foreach (FunctionNodeExpression exp in Exps)
        {
            // State taint
            if (exp.FuncNode?.RequireRemoteCall == true)
                RequireRemoteCall = true;
        }
    }
    
    /// <inheritdoc />
    public override void Unload()
    {
        Args = [];
        Exps = [];

        // Clear function info to be re-compiled
        ClearFunctionInfo();
    }

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (Return == null) yield break;
        
        if (Return is not GenericType)
            yield return Return;

        foreach (FunctionNodeArgument arg in Args)
        {
            if (arg.ValueType != null && arg.ValueType is not GenericType)
                yield return arg.ValueType;
        }

        foreach (FunctionNodeExpression exp in Exps)
        {
            if (exp.ValueType != null && exp.ValueType is not GenericType)
                yield return exp.ValueType;

            if (exp.FuncNode != null)
                yield return exp.FuncNode;

            if (exp.Args is { Length: > 0 })
            {
                foreach (var callArg in exp.Args)
                {
                    if (callArg.ValueType != null && callArg.ValueType is not GenericType)
                        yield return callArg.ValueType;
                }
            }
        }

        foreach (var type in base.GetReferenceTypes())
            yield return type;
    }

    #endregion

    #region Cache Management
    
    /// <summary>
    /// Sets the runtime function cache
    /// </summary>
    public TV? SetRuntimeFuncCache<TV>(Type TK, TV? value)
    {
        if (value == null)
        {
            if (_runtimeFuncCache.TryGetValue(TK, out ConcurrentDictionary<Type, object>? dict))
                dict.TryRemove(typeof(TV), out _);
        }
        else
        {
            _runtimeFuncCache.GetOrAdd(TK, _ => new ConcurrentDictionary<Type, object>())
                .AddOrUpdate(typeof(TV), value, (_, _) => value);
        }
        return value;
    }
    
    /// <summary>
    /// Sets the runtime function cache
    /// </summary>
    public TV? SetRuntimeFuncCache<TK, TV>(TV? value) => SetRuntimeFuncCache(typeof(TK), value);

    /// <summary>
    /// Try to get the runtime function cache
    /// </summary>
    public bool TryGetRuntimeFuncCache<TV>(Type TK, out TV? value)
    {
        if (_runtimeFuncCache.TryGetValue(TK, out ConcurrentDictionary<Type, object>? dict) &&
            dict.TryGetValue(typeof(TV), out object? obj) &&
            obj is TV v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Try to get the runtime function cache
    /// </summary>
    public bool TryGetRuntimeFuncCache<TK, TV>(out TV? value)
    {
        if (TryGetRuntimeFuncCache(typeof(TK), out TV? obj))
        {
            value = obj;
            return true;
        }
        value = default;
        return false;
    }
    
    /// <summary>
    /// Clear the runtime function cache for the type
    /// </summary>
    public void ClearRuntimeFuncCache(Type TK)
    {
        _runtimeFuncCache.TryRemove(TK, out _);
    }
    
    /// <summary>
    /// Clear the runtime function cache for the type
    /// </summary>
    public void ClearRuntimeFuncCache<TK>()
    {
        _runtimeFuncCache.TryRemove(typeof(TK), out _);
    }

    #endregion

    #region Compilation
    
    /// <summary>
    /// Pre-compile the function expression trees
    /// </summary>
    public async Task<FunctionTypeSchema?> PreCompileAsync(SchemaContext context)
    {
        try
        {
            Error = null;
            return await context.VisitFunctionTypeAsync(this);
        }
        catch(FunctionVisitException fex)
        {
            Error = fex.Status;
        }
        catch(Exception ex)
        {
            context.LogError(ex, "FunctionType LoadAsync Error: {0}", Name);
            Error = ErrorCodes.FUNC_COMPILE_ERROR;
        }

        return null;
    }
    
    // Clear the function info to be re-complied
    private void ClearFunctionInfo()
    {
        if (FuncInfo != null && (FuncInfo.Sign & FunctionFlags.Immutable) > 0) return; // Immutable, no need to clear

        _runtimeFuncCache.Clear();
        FuncInfo = null;
        foreach (FunctionType f in GetUsedBy().OfType<FunctionType>())
            f.ClearFunctionInfo();
    }

    /// <summary>
    /// Gets the function info
    /// </summary>
    internal async Task<SchemaFuncInfo?> GetSchemaFuncInfoAsync(SchemaContext context)
    {
        if (FuncInfo != null) return FuncInfo.Method != null ? FuncInfo : null;
        
        // Only full-filled function can be complied
        if (Error != null) throw new Exception($"The {Name} can't be compiled because of {Error}");

        // Build Exp
        SchemaFuncInfo funcInfo = new ()
        {
            Name = Name,
            Sign = FunctionFlags.Context, // always use context for dynamic func
            Args = Args.Select(a =>
            {
                TypeDetail info = a.ValueType!.GetNodeTypeDetails();
                if (!a.Require) info.Kind |= TypeDetail.ParameterTypeKind.Nullable;
                return info;
            }).ToArray(),
            Return = Return.GetNodeTypeDetails()
        };

        // Remote call, no dynamic function required
        if (IsRemoteCall)
        {
            funcInfo.Sign |= FunctionFlags.Remote | FunctionFlags.Async;
            return funcInfo;
        }
        
        // Compile dynamic method
        funcInfo.DynamicMethod = await context.CompileFunctionTypeAsync(this) ?? throw new Exception($"The function {Name} dynamic method compile failed");
        funcInfo.Method = funcInfo.DynamicMethod.Method;
        
        // Compile
        FuncInfo = funcInfo;
        return FuncInfo;
    }

    #endregion

    #region Call Function
    
    /// <summary>
    /// Call the system function asynchronously
    /// </summary>
    private async Task<object?> CallSystemFuncAsync(SchemaContext context, object?[] args, string? rType = null)
    {
        // Argument validation
        SchemaFuncInfo funcInfo = await GetSchemaFuncInfoAsync(context) ?? throw new Exception($"Function {Name} can't be complied");
        
        // Generic types
        Type?[] generics = new Type?[funcInfo.Generics.Length];
        Type? GetArgType(TypeDetail arg, Type? maybeType = null)
        {
            if (arg.IsGenericParameter)
            {
                int idx = Array.FindIndex(funcInfo.Generics, f => f.CoreType == arg.CoreType);
                if (idx < 0) return maybeType;
                generics[idx] ??= maybeType;
                return generics[idx];
            }
            else if (arg.IsGenericType && arg.GenericArguments?.Any(g => g.IsGenericParameter) == true)
            {
                Type[] genArgs = new Type[arg.GenericArguments!.Length];
                if (maybeType != null && maybeType.GetGenericTypeDefinition() != arg.GenericDefine?.CoreType)
                    throw new Exception($"The generic type {maybeType.Name} is not match the definition {arg.GenericDefine?.CoreType.Name} for argument {arg.CoreType}");
                
                Type[]? maybeGenArgs = maybeType?.GetGenericArguments();
                for (int i = 0; i < arg.GenericArguments.Length; i++)
                    genArgs[i] = GetArgType(arg.GenericArguments[i], maybeGenArgs?.ElementAtOrDefault(i)) ?? throw new Exception($"Can't solve the generic type for argument {arg.CoreType}");
                return arg.GenericDefine?.CoreType.MakeGenericType(genArgs);
            }
            return arg.Type;
        }
        
        // parse return type
        if (!string.IsNullOrWhiteSpace(rType) && funcInfo.Return.IsGenericParameter)
        {
            Type? rCsharpType = (await context.GetNodeTypeAsync(rType))?.GetCsharpType();
            if (rCsharpType != null) GetArgType(funcInfo.Return, rCsharpType);
        }
        
        // parse parameters
        object?[] callArgs = new object[funcInfo.Args.Length];
        for (int i = 0; i < funcInfo.Args.Length; i++)
        {
            TypeDetail arg = funcInfo.Args[i];
            FunctionNodeArgument argInfo = Args[i];

            // non params
            if (!arg.Params)
            {
                object? argObj = args.ElementAtOrDefault(i);
                JsonNode? argJson = argObj as JsonNode;
                DataNode? argNode = argObj as DataNode;

                // check null or empty
                if (argObj == null || argJson != null && argJson.IsEmpty() || argNode is { IsEmpty: true })
                {
                    if (arg.Nullable) continue;
                    throw new Exception($"The {i + 1} argument must be provided");
                }

                // Parse argument
                var eleType = GetArgType(arg, argNode?.Type.GetCsharpType() ?? (argJson == null ? argObj.GetType() : null));

                // JsonNode
                if (argJson != null)
                {
                    (object? o, Type? _, Type? gen) = arg.ParseValue(argJson, eleType);
                    callArgs[i] = o ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                    if (eleType == null)
                    {
                        GetArgType(arg, gen ?? o.GetType());
                    }
                    else if (eleType.IsAssignableTo(typeof(DataNode)))
                    {
                        GetArgType(arg, eleType);
                        if (argInfo.ValueType != null && argInfo.ValueType is not GenericType)
                        {
                            DataNode node = argInfo.ValueType.Create();
                            if (!node.TrySetValue(argJson)) throw new Exception($"The {i + 1} argument must be provided and valid");
                            callArgs[i] = node;
                        }
                        else
                            throw new Exception($"Can't solve the {i + 1} argument type");
                    }
                }
                // DataNode
                else if (argNode != null)
                {
                    if (eleType != null && eleType.IsAssignableTo(typeof(DataNode)))
                        callArgs[i] = argNode;
                    else
                        callArgs[i] = eleType != null && argNode.TryGetValue(eleType, out var r) ? r : throw new Exception($"The {i + 1} argument must be provided and valid");
                }
                // object
                else
                {
                    callArgs[i] = eleType != null && eleType.TryConvert(argObj, out var r) ? r : throw new Exception($"The {i + 1} argument must be provided and valid");
                }
            }

            // Params
            else
            {
                Type? eleType = GetArgType(arg);
                ValueType? schemaType = argInfo.ValueType;
                Array? array = eleType != null ? Array.CreateInstance(eleType.GetElementType() ?? eleType, Math.Max(0, args.Length - funcInfo.Args.Length + 1)) : null;
                int count = 0;
                for (int j = funcInfo.Args.Length - 1; j < Math.Max(args.Length, funcInfo.Args.Length); j++)
                {
                    object? argObj = args.ElementAtOrDefault(j);
                    JsonNode? argJson = argObj as JsonNode;
                    DataNode? argNode = argObj as DataNode;

                    if (argObj == null || argJson != null && argJson.IsEmpty() || argNode is { IsEmpty: true }) continue;

                    // JsonNode
                    if (argJson != null)
                    {
                        (object? o, Type? _, Type? gen) = arg.ParseValue(argJson, eleType);
                        argObj = o ?? throw new Exception($"The {j + 1} argument not valid");
                        eleType ??= GetArgType(arg, gen ?? o.GetType());

                        if (eleType != null && eleType.IsAssignableTo(typeof(DataNode)))
                        {
                            if (schemaType != null && schemaType is not GenericType)
                            {
                                var node = schemaType.Create();
                                if (!node.TrySetValue(argJson)) throw new Exception($"The {j + 1} argument not valid");
                                argObj = node;
                            }
                            else
                                throw new Exception($"Can't solve the {j + 1} argument type");
                        }
                    }
                    // AnySchemaNode
                    else if (argNode != null)
                    {
                        if (eleType != null && eleType.IsAssignableTo(typeof(DataNode)))
                            argObj = argNode;
                        else
                        {
                            eleType ??= GetArgType(arg, argNode.Type.GetCsharpType());
                            argObj = argNode.TryGetValue(eleType!, out var r) ? r : throw new Exception($"The {j + 1} argument not valid");
                        }
                    }
                    // object
                    else
                    {
                        eleType ??= GetArgType(arg, argObj.GetType());
                        argObj = eleType!.TryConvert(argObj, out var r) ? r : throw new Exception($"The {j + 1} argument not valid");
                    }

                    if (eleType == null) throw new Exception($"The {j + 1} argument not valid");

                    array ??= Array.CreateInstance(eleType.GetElementType() ?? eleType, Math.Max(0, args.Length - j + 1));
                    array.SetValue(argObj, count++);
                }
                array ??= Array.CreateInstance(eleType?.GetElementType() ?? eleType ?? typeof(object), 0);
                callArgs[i] = array.Length == count ? array : array.SliceArray(count);
            }
        }
        
        if ((funcInfo.Sign & FunctionFlags.Context) > 0)
            callArgs = callArgs.Prepend(context).ToArray();
        
        // Call the method
        MethodInfo callMethod = funcInfo.Method!;

        // Gets the generic method instance
        if ((funcInfo.Sign & FunctionFlags.Generic) > 0)
        {
            for (int i = 0; i < generics.Length; i++) generics[i] ??= typeof(object);

            string genSign = string.Join('|', generics.Select(p => (Nullable.GetUnderlyingType(p!) ?? p!).FullName));
            callMethod = funcInfo.GenericMethods.GetOrAdd(genSign, _ => funcInfo.Method!.MakeGenericMethod(generics!));
        }

        // Call the method
        return (funcInfo.Sign & FunctionFlags.Async) > 0
            ? GetCallAsyncFunc(callMethod.ReturnType.GetGenericArguments()[0]).Invoke(null, [callMethod, callArgs])
            : callMethod.Invoke(null, callArgs);
    }
    
    /// <summary>
    /// Call the function asynchronously
    /// </summary>
    public async Task<T?> CallAsync<T, TC>(SchemaContext context, object?[] args, string? rType = null)
        where TC: CompileContext
    {
        object? result;

        // Remote call
        if (IsRemoteCall)
        {
            JsonArray cArgs = [];
            foreach (object? arg in args) 
                cArgs.Add(arg is DataNode node ? node.ToJsonNode() : arg.ToJsonNode());

            result = Provider != null && context.GetRequiredService(Provider) is IFunctionSchemaProvider provider
                ? await provider.CallFunctionAsync(Name, cArgs, rType)
                : null;
        }

        // Call system method
        else if (IsSystemCall)
        {
            result = await CallSystemFuncAsync(context, args, rType);
        }

        // Invoke the dynamic method
        else
        {
            // Argument validation
            FunctionTypeSchema funcSchema = await context.VisitFunctionTypeAsync<TC>(this);
        
            // parse parameters
            object?[] callArgs = new object[funcSchema.Args.Length];
            for (int i = 0; i < funcSchema.Args.Length; i++)
            {
                ArgumentExp arg = funcSchema.Args[i];
            
                // validate argument
                object? argObj = args.ElementAtOrDefault(i);
                JsonNode? argJson = argObj as JsonNode;
                DataNode? argNode = argObj as DataNode;

                // check null or empty
                if (argObj == null || argJson != null && argJson.IsEmpty() || argNode is { IsEmpty: true })
                {
                    if (!arg.Require) continue;
                    throw new Exception($"The {i + 1} argument must be provided");
                }

                // Parse argument
                var eleType = arg.ValueType.GetCsharpType()!;

                if (eleType.IsAssignableTo(typeof(DataNode)))
                {
                    var node = arg.ValueType.Create();
                    if (!node.TrySetValue(argObj))
                        throw new Exception($"The {i + 1} argument must be provided and valid");
                    callArgs[i] = node;
                }
                else
                {
                    // DataNode
                    if (argNode != null)
                    {
                        callArgs[i] = argNode.TryGetValue(eleType, out var o) ? o : throw new Exception($"The {i + 1} argument must be provided and valid");
                    }
                
                    // JsonNode | object
                    else if (argJson != null)
                    {
                        callArgs[i] = eleType.TryConvert(argJson, out var o) ? o : throw new Exception($"The {i + 1} argument must be provided and valid");
                    }
                    
                    // other
                    else
                    {
                        callArgs[i] = eleType.TryConvert(argObj, out var o) ? o : throw new Exception($"The {i + 1} argument must be provided and valid");
                    }
                }
            }
        
            // All custom function use context
            callArgs = callArgs.Prepend(context).ToArray();

            try
            {
                Delegate method = await context.CompileFunctionTypeAsync<TC>(this)
                    ?? throw new Exception($"The function {Name} dynamic method compile failed");
                result = method.DynamicInvoke(callArgs);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                // ReSharper disable once PossibleIntendedRethrow
                throw ex;
            }
        }

        // Parse the return type
        if (result == null) return default(T);
        if (result is T r) return r;
        if (typeof(T).IsAssignableTo(typeof(DataNode)))
        {
            return await context.GetSchemaNodeAsync(result) is T rNode ? rNode : default(T?);
        }
        return result.TryConvertTo<T>(out var f) ? f : default(T?);
    }

    /// <summary>
    /// Call the function asynchronously with default compile context
    /// </summary>
    public Task<T?> CallAsync<T>(SchemaContext context, object?[] args, string? rType = null)
        => CallAsync<T, CompileContext>(context, args, rType);

    #endregion

    #region Utility

    // Call async function
    static T? CallAsyncFunc<T>(MethodBase asyncCall, params object[] callArgs)
    {
        Task<T>? task = (Task<T>?)asyncCall.Invoke(null, callArgs);
        return task == null ? default : task.GetAwaiter().GetResult();
    }

    // Gets the call async method
    static MethodInfo GetCallAsyncFunc(Type t) => CallAsyncMethodMap.GetOrAdd(t, p => typeof(FunctionType).GetMethod(nameof(CallAsyncFunc), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(p));
    static readonly ConcurrentDictionary<Type, MethodInfo> CallAsyncMethodMap = new();

    // static mappings
    private static readonly ConcurrentDictionary<string, MethodInfo> CallConvertNullableExp = new();

    /// <summary>
    /// The runtime cache
    /// </summary>
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, object>> _runtimeFuncCache = new();

    #endregion
}

#region Inner Type

/// <summary>
/// The expression tree
/// </summary>
public abstract class FunctionNodeExpTree
{
    /// <summary>
    /// The type node
    /// </summary>
    public ValueType? ValueType { get; set; }
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
    public required string Name { get; init; }

    /// <summary>
    /// The argument type
    /// </summary>
    public required string Type { get; init; }
    
    /// <summary>
    /// The argument is required
    /// </summary>
    public bool Require { get; init; }

    /// <summary>
    /// Whether params argument
    /// </summary>
    public bool? Variadic { get; init; }

    /// <summary>
    /// The default value
    /// </summary>
    public object? Default { get; init; }

    /// <summary>
    /// The func argument
    /// </summary>
    private FuncArg? _source;

    #endregion
    
    #region Methdos

    /// <summary>
    /// Gets the property of the argument
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => _source?.GetProperty<T>();
    
    #endregion
    
    #region Conversion

    public static implicit operator FunctionNodeArgument(FuncArg arg)
    {
        return new FunctionNodeArgument
        {
            Name = arg.Name,
            Type = arg.Type,
            Variadic = arg.GetProperty<Variadic>()?.Value,
            Require = arg.GetProperty<Require>()?.GetValue<bool>() ?? false,
            Default = arg.GetProperty<Default>()?.Value,
            _source = arg
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
    public string Name { get; internal init; } = string.Empty;

    /// <summary>
    /// The function to be called.
    /// </summary>
    public string Func { get; internal init; } = string.Empty;

    /// <summary>
    /// The function used to map array elements
    /// </summary>
    public ExpType? Type { get; init; } = ExpType.Call;

    /// <summary>
    /// The namespace.
    /// </summary>
    public string Return { get; internal init; } = string.Empty;

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public CallArg[] Args { get; init; } = [];

    #endregion

    #region State

    /// <summary>
    /// The status
    /// </summary>
    public string? Status { get; set; }

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

#endregion

public static class FunctionTypeExtensions
{
    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="node">The function schema node</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return type</param>
    /// <returns>The result</returns>
    public static Task<JsonNode?> CallFunctionAsync(this SchemaContext context, FunctionType node, JsonArray args, string? rType = null)
        => node.CallAsync<JsonNode>(context, args.Select(object? (p) => p).ToArray(), rType);

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return types</param>
    /// <returns>The result</returns>
    public static Task<JsonNode?> CallFunctionAsync(this SchemaContext context, string name, JsonArray args, string? rType = null)
        => CallFunctionAsync<JsonNode>(context, name,  args.Select(object? (p) => p).ToArray(), rType);
    
    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return type</param>
    /// <returns>The result</returns>
    public static Task<T?> CallFunctionAsync<T>(this SchemaContext context, string name, object?[] args, string? rType = null) 
        => CallFunctionAsync<T, CompileContext>(context, name, args, rType);
    
    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return type</param>
    /// <returns>The result</returns>
    public static async Task<T?> CallFunctionAsync<T, TC>(this SchemaContext context, string name, object?[] args, string? rType = null) 
        where TC: CompileContext
    {
        FunctionType node = await context.GetNodeTypeAsync<FunctionType>(name) ?? throw new Exception($"Function {name} not found");
        return await node.CallAsync<T, TC>(context, args, rType);
    }
}