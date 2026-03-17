using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Schema;
using ExpressionType = SchemaNode.Enum.ExpressionType;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory function schema representation
/// </summary>
public sealed class FunctionType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    public string Return { get; private set; } = string.Empty;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FunctionNodeArgument[] Args { get; private set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FunctionNodeExpression[] Exps { get; private set; } = [];

    /// <summary>
    /// The basic type of generic types, provided to T(single generic type),
    /// T1, T2(for multi generic type)
    /// </summary>
    public AnySchemaType?[] Generic { get; private set; } = [];

    /// <summary>
    /// Call server if server provided
    /// </summary>
    public bool? Server { get; private set; }

    /// <summary>
    /// The client should not cache the result
    /// </summary>
    public bool? Nocache { get; private set; }

    /// <summary>
    /// As type converter
    /// </summary>
    public bool? Converter { get; private set; }

    /// <summary>
    /// The function has side effects
    /// </summary>
    public bool? SideEffect { get; private set; }
    
    /// <summary>
    /// The function is workflow only
    /// </summary>
    public bool? WorkflowOnly { get; private set; }
    
    #endregion
    
    #region Status
    
    /// <summary>
    /// The method info of the function if it's a system function
    /// </summary>
    public MethodInfo? MethodInfo { get; private set; }
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Func;

    /// <summary>
    /// Whether the function is remote call only
    /// </summary>
    public bool IsRemoteCall => (LoadState & SchemaLoadState.Remote) > 0;

    /// <summary>
    /// Whether the function require call server
    /// </summary>
    public bool RequireRemoteCall { get; private set; }

    /// <summary>
    /// Whether the function is defined as system, direct call
    /// </summary>
    public bool IsSystemCall => (LoadState & SchemaLoadState.System) > 0;

    /// <summary>
    /// The function info
    /// </summary>
    internal SchemaFuncInfo? FuncInfo { get; private set; }
    
    /// <summary>
    /// The runtime cache
    /// </summary>
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, object>> _runtimeFuncCache = new();
    
    #endregion
    
    #region Ref
    
    /// <summary>
    /// The return type node
    /// </summary>
    public AnySchemaType? ReturnNode { get; internal set; }
    
    #endregion
    
    #region Implementation
    
    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        FunctionSchema? func = schema.Func;
        
        // Data
        Return = func?.Return ?? string.Empty;
        Args = func?.Args.Select(a => (FunctionNodeArgument)a).ToArray() ?? [];
        Exps = func?.Exps.Select(e => (FunctionNodeExpression)e).ToArray() ?? [];
        Generic = func?.Generic != null ? new AnySchemaType?[func.Generic.Length] : [];
        Server = func?.Server;
        Nocache = func?.Nocache;
        Converter = func?.Converter;
        SideEffect = func?.SideEffect;
        WorkflowOnly = func?.WorkflowOnly;
        MethodInfo = StaticMethodMap.TryGetValue(schema.Name, out SchemaFuncInfo? info) ? info.Method : null;

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
        RequireRemoteCall = IsRemoteCall;
        
        // Argument types
        foreach (FunctionNodeArgument arg in Args)
        {
            if (string.IsNullOrWhiteSpace(arg.Type))
            {
                Status = SchemaNodeStatus.FunctionArgumentWrongType;
            }
            else if (Regex.IsMatch(arg.Type, @"^[tT]\d*$"))
            {
                // Only system function can be generic
                if (!IsSystemCall)
                {
                    Status = SchemaNodeStatus.FunctionArgumentWrongType;
                }
                else
                {
                    // Generic type// Generic type
                    int index = arg.Type.Length > 1 && int.TryParse(arg.Type[1..], out int i) ? i : 1;
                    ResizeGeneric(index);
                    arg.SchemaType = new GenericType { Index = index };
                }
            }
            else
            {
                arg.SchemaType = await context.GetSchemaTypeAsync(arg.Type);
                if (arg.SchemaType is not { IsValueType: true }) Status = SchemaNodeStatus.FunctionArgumentWrongType;
            }
        }
        
        // Return type
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
                // Generic type// Generic type
                int index = Return.Length > 1 && int.TryParse(Return[1..], out int i) ? i : 1;
                ResizeGeneric(index);
                ReturnNode = new GenericType { Index = index };
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
            
            // Add ref
            foreach (FunctionNodeExpression exp in Exps)
            {
                exp.SchemaType?.AddRef(this);
                exp.FuncNode?.AddRef(this);
                
                // State taint
                if (exp.FuncNode?.RequireRemoteCall == true)
                    RequireRemoteCall = true;
                if (exp.FuncNode?.Server == true)
                    Server = true;
                if (exp.FuncNode?.WorkflowOnly == true)
                    WorkflowOnly = true;
                if (exp.FuncNode?.SideEffect == true)
                    SideEffect = true;
                if (exp.FuncNode?.Nocache == true)
                    Nocache = true;
            }
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

        // Clear function info to be re-compiled
        ClearFunctionInfo();
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemaType other, bool exactly = false) => false;

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false) => null;

    /// <inheritdoc />
    public override IEnumerable<AnySchemaType> GetDependNodes()
    {
        if (ReturnNode != null && ReturnNode is not GenericType)
            yield return ReturnNode;

        foreach (FunctionNodeArgument arg in Args)
        {             
            if (arg.SchemaType != null && arg.SchemaType is not GenericType)
                yield return arg.SchemaType;
        }

        foreach(FunctionNodeExpression exp in Exps)
        {
            if (exp.SchemaType != null && exp.SchemaType is not GenericType)
                yield return exp.SchemaType;

            if (exp.FuncNode != null)
                yield return exp.FuncNode;

            if (exp.Args is { Length: > 0 })
            {
                foreach (FuncCallArg callArg in exp.Args)
                {
                    if (callArg.SchemeType != null && callArg.SchemeType is not GenericType)
                        yield return callArg.SchemeType;
                }
            }
        }
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
            Status = SchemaNodeStatus.Ready;
            
            // Try build the function and validate
            return await context.VisitFunctionTypeAsync(this);
        }
        catch(FunctionVisitException fex)
        {
            Status = fex.Status;
        }
        catch(Exception ex)
        {
            context.LogError(ex, "FunctionType LoadAsync Error: {0}", Name);
            Status = SchemaNodeStatus.FunctionExpsHasCompileError;
        }

        return null;
    }
    
    // Clear the function info to be re-complied
    private void ClearFunctionInfo()
    {
        if (FuncInfo != null && (FuncInfo.Sign & FUNC_SIGN_IMMUTABLE) > 0) return; // Immutable, no need to clear

        _runtimeFuncCache.Clear();
        FuncInfo = null;
        if (UsedBy == null || UsedBy.IsEmpty) return;
        foreach ((AnySchemaType other, _) in UsedBy)
        {
            if (other is FunctionType func)
                func.ClearFunctionInfo();
        }
    }

    /// <summary>
    /// Gets the system function info
    /// </summary>
    public SchemaFuncInfo? GetSystemSchemaFuncInfo()
    {
        if (FuncInfo != null) return FuncInfo.Method != null ? FuncInfo : null;

        // Check is static
        if (!StaticMethodMap.TryGetValue(Name, out SchemaFuncInfo? result) || (result.Sign & FUNC_SIGN_IMMUTABLE) != FUNC_SIGN_IMMUTABLE) return null;
        result.FunctionNode = this;
        FuncInfo = result;
        return result;
    }

    /// <summary>
    /// Gets the function info
    /// </summary>
    public async Task<SchemaFuncInfo?> GetSchemaFuncInfoAsync(SchemaContext context)
    {
        if (FuncInfo != null) return FuncInfo.Method != null ? FuncInfo : null;

        // Check is static
        if (StaticMethodMap.TryGetValue(Name, out SchemaFuncInfo? result) && (result.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result.FunctionNode = this;
            FuncInfo = result;
            return result;
        }
        
        // Only full-filled function can be complied
        if (Status != SchemaNodeStatus.Ready) throw new Exception($"The {Name} can't be compiled because of {Status}");

        // Build Exp
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
        Type? GetArgType(SchemaParamTypeInfo arg, Type? maybeType = null)
        {
            if (arg.Generic == null) return arg.Type;
            int idx = Array.FindIndex(funcInfo.Generics, f => f.Generic == arg.Generic);
            if (idx < 0) return maybeType;
            generics[idx] ??= maybeType;
            return generics[idx];
        }
        
        // parse return type
        if (!string.IsNullOrWhiteSpace(rType) && funcInfo.Return.Generic != null)
        {
            var rSchemaType = await context.GetSchemaTypeAsync(rType);
            Type? rCsharpType = rSchemaType?.ToCSharpType();
            if (rCsharpType != null) GetArgType(funcInfo.Return, rCsharpType);
        }
        
        // parse parameters
        object?[] callArgs = new object[funcInfo.Args.Length];
        for (int i = 0; i < funcInfo.Args.Length; i++)
        {
            SchemaParamTypeInfo arg = funcInfo.Args[i];

            // non params
            if (!arg.Params)
            {
                object? argObj = args.ElementAtOrDefault(i);
                JsonNode? argJson = argObj as JsonNode;
                AnySchemaNode? argNode = argObj as AnySchemaNode;

                // check null or empty
                if (argObj == null || argJson != null && argJson.IsEmpty() || argNode is { IsEmpty: true })
                {
                    if (arg.Nullable) continue;
                    throw new Exception($"The {i + 1} argument must be provided");
                }

                // Parse argument
                var eleType = GetArgType(arg, argNode?.SchemaType.ToCSharpType() ?? (argJson == null ? argObj.GetType() : null));

                // JsonNode
                if (argJson != null)
                {
                    (object? o, Type? _, Type? gen) = arg.ParseValue(argJson, eleType);
                    callArgs[i] = o ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                    if (eleType == null)
                        GetArgType(arg, gen ?? o.GetType());
                    else if (eleType.IsAssignableTo(typeof(AnySchemaNode)))
                    {
                        GetArgType(arg, typeof(AnySchemaNode));
                        AnySchemaType? schemaType = !string.IsNullOrWhiteSpace(arg.SchemaType)
                            ? await context.GetSchemaTypeAsync(arg.SchemaType)
                            : null;

                        callArgs[i] = schemaType?.CreateNode(argJson)
                            ?? await context.GetSchemaNodeAsync(o)
                            ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                    }
                }
                // AnySchemaNode
                else if (argNode != null)
                {
                    if (eleType != null && eleType.IsAssignableTo(typeof(AnySchemaNode)))
                        callArgs[i] = argNode;
                    else
                        callArgs[i] = argNode.ToTypeValue(eleType!) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                }
                // object
                else
                {
                    callArgs[i] = eleType?.TryConvert(argObj) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                }
            }

            // Params
            else
            {
                Type? eleType = GetArgType(arg);
                AnySchemaType? schemaType = !string.IsNullOrWhiteSpace(arg.SchemaType) ? await context.GetSchemaTypeAsync(arg.SchemaType) : null;
                Array? array = eleType != null ? Array.CreateInstance(eleType.GetElementType() ?? eleType, Math.Max(0, args.Length - funcInfo.Args.Length + 1)) : null;
                int count = 0;
                for (int j = funcInfo.Args.Length - 1; j < Math.Max(args.Length, funcInfo.Args.Length); j++)
                {
                    object? argObj = args.ElementAtOrDefault(j);
                    JsonNode? argJson = argObj as JsonNode;
                    AnySchemaNode? argNode = argObj as AnySchemaNode;

                    if (argObj == null || argJson != null && argJson.IsEmpty() || argNode is { IsEmpty: true }) continue;

                    // JsonNode
                    if (argJson != null)
                    {
                        (object? o, Type? _, Type? gen) = arg.ParseValue(argJson, eleType);
                        argObj = o ?? throw new Exception($"The {j + 1} argument not valid");
                        eleType ??= GetArgType(arg, gen ?? o.GetType());

                        if (eleType != null && eleType.IsAssignableTo(typeof(AnySchemaNode)))
                        {

                            argObj = schemaType?.CreateNode(argJson) ?? await context.GetSchemaNodeAsync(o)
                                ?? throw new Exception($"The {j + 1} argument not valid");
                        }
                    }
                    // AnySchemaNode
                    else if (argNode != null)
                    {
                        if (eleType != null && eleType.IsAssignableTo(typeof(AnySchemaNode)))
                            argObj = argNode;
                        else
                        {
                            eleType ??= GetArgType(arg, argNode.SchemaType.ToCSharpType());
                            argObj = argNode.ToTypeValue(eleType!) ?? throw new Exception($"The {j + 1} argument not valid");
                        }
                    }
                    // object
                    else
                    {
                        eleType ??= GetArgType(arg, argObj.GetType());
                        argObj = eleType!.TryConvert(argObj) ?? throw new Exception($"The {j + 1} argument not valid");
                    }

                    if (eleType == null) throw new Exception($"The {j + 1} argument not valid");

                    array ??= Array.CreateInstance(eleType.GetElementType() ?? eleType, Math.Max(0, args.Length - j + 1));
                    array.SetValue(argObj, count++);
                }
                array ??= Array.CreateInstance(eleType?.GetElementType() ?? eleType ?? typeof(object), 0);
                callArgs[i] = array.Length == count ? array : array.SliceArray(count);
            }
        }
        
        if ((funcInfo.Sign & FUNC_SIGN_CONTEXT) > 0)
            callArgs = callArgs.Prepend(context).ToArray();
        
        // Call the method
        MethodInfo callMethod = funcInfo.Method!;

        // Gets the generic method instance
        if ((funcInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
        {
            for (int i = 0; i < generics.Length; i++) generics[i] ??= typeof(object);
            if (generics.Any(g => g is null)) throw new Exception("The generic types must be provided");

            string genSign = string.Join('|', generics.Select(p => p!.Name));
            callMethod = funcInfo.GenericMethods.GetOrAdd(genSign, _ => funcInfo.Method!.MakeGenericMethod(generics!));
        }

        // Call the method
        return (funcInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC
            ? GetCallAsyncFunc(callMethod.ReturnType.GetGenericArguments()[0]).Invoke(null, [callMethod, callArgs])
            : callMethod.Invoke(null, callArgs);
    }
    
    /// <summary>
    /// Call the function asynchronously
    /// </summary>
    public async Task<T?> CallAsync<T, TC>(SchemaContext context, object?[] args, string? rType = null, string? target = null)
        where TC: CompileContext
    {
        object? result;

        // Remote call
        if (IsRemoteCall)
        {
            JsonArray cArgs = [];
            foreach (object? arg in args) 
                cArgs.Add(arg is AnySchemaNode node ? node.ToJsonNode() : arg.ToJsonNode());

            result = SchemaProvider != null
                ? await ((ISchemaProvider)context.GetRequiredService(SchemaProvider)).CallFunctionAsync(Name, cArgs, rType, target)
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
                AnySchemaNode? argNode = argObj as AnySchemaNode;

                // check null or empty
                if (argObj == null || argJson != null && argJson.IsEmpty() || argNode is { IsEmpty: true })
                {
                    if (arg.Nullable) continue;
                    throw new Exception($"The {i + 1} argument must be provided");
                }

                // Parse argument
                var eleType = arg.SchemaType.ToCSharpType();

                if (eleType.IsAssignableTo(typeof(AnySchemaNode)))
                {
                    callArgs[i] = arg.SchemaType.CreateNode(argObj) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                }
                else
                {
                    // AnySchemaNode
                    if (argNode != null)
                    {
                        callArgs[i] = argNode.ToTypeValue(eleType) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                    }
                
                    // JsonNode | object
                    else if (argJson != null)
                    {
                        callArgs[i] = eleType.TryConvert(argJson) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                    }
                    
                    // other
                    else
                    {
                        callArgs[i] = eleType.TryConvert(argObj) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
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

        // Convert the return type
        Type retType = typeof(T);
        if (retType.IsAssignableTo(typeof(JsonNode)))
        {
            result = result is AnySchemaNode node ? node.ToJson() : result is JsonNode jn ? jn : result.ToJsonNode();
            if (retType == typeof(JsonArray))
                return (T)(object)(result as JsonArray ?? []);
            else if (retType == typeof(JsonObject))
                return (T)(object)(result as JsonObject ?? new JsonObject());
            else if (retType == typeof(JsonValue))
                return (T)(object)(result as JsonValue ?? JsonValue.Create((object?)null)!);
            else
                return (T)result!;
        }
        else if (retType.IsAssignableTo(typeof(AnySchemaNode)))
        {
            return (T)(object)(ReturnNode?.CreateNode(result) ?? await context.GetSchemaNodeAsync(result) ?? throw new Exception("The return type can't be resolved"));
        }
        return (T?)typeof(T).TryConvert(result);
    }

    /// <summary>
    /// Call the function asynchronously with default compile context
    /// </summary>
    public Task<T?> CallAsync<T>(SchemaContext context, object?[] args, string? rType = null, string? target = null)
        => CallAsync<T, CompileContext>(context, args, rType, target);

    #endregion

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
        var name = (funcAttr.Name ?? $"{(string.IsNullOrEmpty(ns) ? "" : $"{ns}.")}{method.Name}").ToLowerInvariant();

        // Keep in the same namespace
        if (funcAttr?.Name != null)
            ns = string.Join('.', funcAttr.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));

        NodeSchema funcSchema = new NodeSchema
        {
            Name = name,
            Type = SchemaType.Func,
            Display = funcAttr?.Display ?? method.GetSummaryFromXmlDoc() ?? name,
            Func = new FunctionSchema
            {
                Return = string.Empty,
                Args = new FuncArg[parameters.Length],
                Exps = [],
                Nocache = method.IsDefined(typeof(NoCacheAttribute)),
                Server = method.IsDefined(typeof(ServerOnlyAttribute)),
                SideEffect = method.IsDefined(typeof(SideEffectAttribute)),
                Converter = method.IsDefined(typeof(ConverterAttribute)),
                WorkflowOnly = method.IsDefined(typeof(WorkflowOnlyAttribute)),
                Generic = genInfos.Select(g => g is { AnyArray: false, Number: true } ? NS_SYSTEM_NUMBER : "").ToArray(),
            }
        };

        // Parameter types
        int genericCount = genInfos.Length;
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
                Display = method.GetSummaryFromXmlDoc(p) ?? null,
                Default = p.GetCustomAttribute<DefaultAttribute>()?.Value, // not the default value of the parameter
            };
            funcSchema.Func.Args[i] = arg;
            if ((arg.Nullable ?? false) || new NullabilityInfoContext().Create(p).ReadState == NullabilityState.Nullable)
                pt.Kind |= ParameterTypeKind.Nullable;

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
            else if (Regex.IsMatch(pt.SchemaType, REGEX_GENERIC_TYPE)) // AnySchemaNode | object
            {
                arg.Type = $"T{++genericCount}";
            }
            else
            {
                arg.Type = (arg.Params ?? false) && pt.SchemaType.EndsWith("s") && GetSystemNodeSchema(pt.SchemaType)?.Type == SchemaType.Array 
                    ? pt.SchemaType[..^1] 
                    : pt.SchemaType;
            }
        }

        // Return type
        SchemaParamTypeInfo? retInfo = method.ReturnType.GetSchemaTypeInfo(true, ns);
        if (retInfo == null) return null;
        if (retInfo.Task) sign |= FUNC_SIGN_ASYNC;
        if (retInfo.Nullable) sign |= FUNC_SIGN_NULLABLE_RET;
        else if (new NullabilityInfoContext().Create(method.ReturnParameter).ReadState == NullabilityState.Nullable)
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
            funcSchema.Func.Return = $"T{++genericCount}";
        }
        else
        {
            funcSchema.Func.Return = retInfo.SchemaType;
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

        if (Utility.SystemLocale.HasLocales)
            Utility.SystemLocale.Translate(funcSchema.Display, funcSchema.Name);

        return funcSchema;
    }

    #endregion

    #region Utility

    private void ResizeGeneric(int count)
    {
        if (Generic.Length >= count) return;
        AnySchemaType?[] generic = new AnySchemaType?[count];
        for(int i = 0; i < Math.Min(count, Generic.Length); i++)
            generic[i] = Generic[i];
        Generic = generic;
    }
    
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
    private static readonly ConcurrentDictionary<string, SchemaFuncInfo> StaticMethodMap = new();
    private static readonly ConcurrentDictionary<string, MethodInfo> CallConvertNullableExp = new();

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
                Display = a.Display,
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
            SideEffect = schema.SideEffect,
            WorkflowOnly = schema.WorkflowOnly,
            Converter = schema.Converter,
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
    /// The type node
    /// </summary>
    public AnySchemaType? SchemaType { get; set; }
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
    /// Whether nullable
    /// </summary>
    public bool? Nullable { get; init; }
    
    /// <summary>
    /// The display name
    /// </summary>
    public LocaleString? Display { get; init; }

    /// <summary>
    /// Whether params argument
    /// </summary>
    public bool? Params { get; init; }

    /// <summary>
    /// The default value
    /// </summary>
    public object? Default { get; init; }

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
            Display = arg.Display,
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
    public string Name { get; internal init; } = string.Empty;

    /// <summary>
    /// The function to be called.
    /// </summary>
    public string Func { get; internal init; } = string.Empty;

    /// <summary>
    /// The function used to map array elements
    /// </summary>
    public ExpressionType? Type { get; init; } = ExpressionType.Call;

    /// <summary>
    /// The namespace.
    /// </summary>
    public string Return { get; internal init; } = string.Empty;

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public FuncCallArg[] Args { get; init; } = [];

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
/// The data dict func info
/// </summary>
public sealed class SchemaFuncInfo
{
    /// <summary>
    /// The method name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The method info
    /// </summary>
    public MethodInfo? Method { get; internal set; }

    /// <summary>
    /// The dynamic method generated by expression
    /// </summary>
    public Delegate? DynamicMethod { get; internal set; }

    /// <summary>
    /// The function node
    /// </summary>
    public FunctionType? FunctionNode { get; internal set; }

    /// <summary>
    ///  The sign of the function
    /// </summary>
    public int Sign { get; internal set; }

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

#endregion