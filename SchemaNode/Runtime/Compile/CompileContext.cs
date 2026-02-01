using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable AccessToModifiedClosure

namespace SchemaNode.Runtime;

/// <summary>
/// The function compile context
/// </summary>
public class CompileContext(SchemaContext context, FunctionType function)
{
    #region Private Fields

    // The expression visitors
    private readonly IExpVisitor[] _visitors = context.ServiceProvider.GetServices<IExpVisitor>()
        .OrderByDescending(v => v.Priority)
        .ToArray();
    
    // the argument parameter expression map
    private readonly Dictionary<string, ParameterExpression> _paramExpMap = new ();
    
    // the variable parameter expression map
    private readonly Dictionary<string, ParameterExpression> _variableExpMap = new ();

    // The compile exp cache for pre-set
    private readonly Dictionary<SchemaExp, Expression> _compiledExpCache = new ();

    // the return label
    private LabelTarget? _returnLabel;
    
    /// <summary>
    /// The context exp
    /// </summary>
    private ParameterExpression? _contextExp;
    
    private const string StructResultExpName = "_structResult";

    #endregion

    #region Properties

    /// <summary>
    /// The schema context
    /// </summary>
    public readonly SchemaContext Context = context;
    
    /// <summary>
    /// The function type
    /// </summary>
    public readonly FunctionType Function = function;

    #endregion
    
    #region Methods
    
    /// <summary>
    /// Generate Semantic Analysis Schema Expressions for the given function type
    /// </summary>
    public virtual async Task<FunctionTypeSchema> VisitFunctionType()
    {
        #region Pre-checks

        if (Function.TryGetRuntimeFuncCache(GetType(), out FunctionTypeSchema? cache))
            return cache!;

        // Require exps
        if (Function is { IsSystemCall: false, Exps.Length: 0 })
        {
            Function.Status = SchemaNodeStatus.FunctionNoExps;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionNoExps);
        }

        // Validate return type
        AnySchemaType? returnType = Function.ReturnNode ?? (!string.IsNullOrWhiteSpace(Function.Return) ? await Context.GetSchemaTypeAsync(Function.Return) : null);
        if (!Function.IsSystemCall && returnType is not { IsValueType: true })
        {
            Function.Status = SchemaNodeStatus.FunctionWrongReturnType;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType);
        }
        Function.ReturnNode = returnType;

        // Expression cache
        ArgumentExp[] argExps = new ArgumentExp[Function.Args.Length];
        List<VariableExp> results = [];
        Dictionary<string, VariableExp> expMaps = [];
        Dictionary<string, int> accessCount = [];

        #endregion

        #region Arguments

        // Process arguments
        for (int argIdx = 0; argIdx < Function.Args.Length; argIdx++)
        {
            FunctionNodeArgument arg = Function.Args[argIdx];

            // Require argument name
            if (!Function.IsSystemCall) // skip system function check
            {
                if (string.IsNullOrWhiteSpace(arg.Name))
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentNoName;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentNoName);
                }
                // No duplicate name
                else if (expMaps.ContainsKey(arg.Name))
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentDuplicateName;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentDuplicateName);
                }
                // Require type, only system function support generic type like T1, T2...
                else if (string.IsNullOrEmpty(arg.Type) || Regex.IsMatch(arg.Type, @"^[tT]\d*$"))
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentNoType;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentNoType);
                }
            }

            // Validate the argument type
            AnySchemaType? argTypeNode = arg.SchemaType ?? await Context.GetSchemaTypeAsync(arg.Type);
            if (!Function.IsSystemCall && argTypeNode is not { IsValueType: true })
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType);
            }
            arg.SchemaType = argTypeNode;

            // Create argument expression
            argExps[argIdx] = new ArgumentExp(arg.Name, argIdx, arg.Nullable ?? false, argTypeNode ?? GenericType.Instance); // for safe
            expMaps[arg.Name] = new VariableExp(arg.Name, argExps[argIdx]);
        }

        #endregion

        #region System Call Direct Return
        
        if (Function.IsSystemCall)
        {
            cache = new FunctionTypeSchema(argExps, [], Function.GetSystemSchemaFuncInfo()!.Method!.ReturnType);
            Function.SetRuntimeFuncCache<CompileContext, FunctionTypeSchema>(cache);
            return cache;
        }

        #endregion

        #region Expression

        // Share by helpers
        CollectionRootExp? colSource = null;
        CollectionItemExp? iteratorExp = null;
        HashSet<string> currAccessCount = [];
        foreach (FunctionNodeExpression exp in Function.Exps)
        {
            colSource = null; // share by helpers
            iteratorExp = null;
            currAccessCount.Clear();
            await GenFuncCallExpression(exp);
            
            // Merge access count
            foreach (var kv in currAccessCount)
                accessCount[kv] = accessCount.GetValueOrDefault(kv, 0) + 1;
        }

        #endregion

        #region Struct Result Build
        
        // struct build
        if (!results.Last().SchemaType.CanBeUseAs(returnType!))
        {
            if (returnType is StructType { Fields: {  Length: > 0 } } @struct)
            {
                List<StructFieldExp> fields = [];
                foreach (var f in @struct.Fields.Where(f => !(f.DisplayOnly ?? false)))
                {
                    if (GetExpression(f.Name, out SchemaExp? fieldExp))
                    {
                        if (!fieldExp!.SchemaType.CanBeUseAs(f.SchemeType!))
                        {
                            Function.Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionReturnMemberNotValid);
                        }
                        fields.Add(new StructFieldExp(f.Name, fieldExp));

                        string acc = f.Name.Split('.').First();
                        accessCount[acc] = accessCount.GetValueOrDefault(acc, 0) + 1;
                    }
                    else if (f.Require ?? false)
                    {
                        Function.Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionReturnMemberNotValid);
                    }
                }
                results.Add(new VariableExp(StructResultExpName, new StructResultExp(fields.ToArray(), @struct)));
            }
            else
            {
                Function.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType);
            }
        }

        #endregion

        #region Semantic analysis

        // Convert single access variable expression to inline expression
        List<VariableExp> final = [];
        foreach (VariableExp t in results)
        {
            VariableExp varExp = t;
            SchemaExp inner = varExp.Value;

            switch (inner)
            {
                // Function analysis and inline conversion
                case FuncCallExp callExp:
                {
                    varExp = new VariableExp(t.Name, await VisitSchemaExpAsync(
                        new FuncCallExp(callExp.Function, callExp.Args.Select(Inline).ToArray(), 
                            callExp.SchemaType, callExp.ExpType)));
                    expMaps[varExp.Name] = varExp;
                    break;
                }
                
                // Struct result expression
                case StructResultExp resultExp:
                {
                    varExp = new VariableExp(t.Name, new StructResultExp(resultExp.Fields.Select(f 
                        => new StructFieldExp(f.Name, Inline(f.Expression))).ToArray(), resultExp.SchemaType));
                    break;
                }
            }

            // Remove the one time access variables
            if (!accessCount.TryGetValue(varExp.Name, out int count) || count == 0 || count > 1)
                final.Add(varExp);
        }

        #endregion

        // Done
        return Function.SetRuntimeFuncCache(GetType(), new FunctionTypeSchema(argExps, final.ToArray(), returnType!.ToCSharpType()))!;

        #region Helper
        
        // Combine Collection Root Exp
        CollectionRootExp UnpackCollectionRootExp(CollectionRootExp c)
        {
            var exp = Inline(c.Collection);
            return exp is CollectionRootExp r ? r : new CollectionRootExp(exp, c.SchemaType);
        }
        
        // Inline function
        SchemaExp Inline(SchemaExp exp) => exp switch 
        {
            // Inline single access variable expression
            VariableExp v => accessCount.TryGetValue(v.Name, out int vc) && vc == 1 ? expMaps[v.Name].Value : expMaps[v.Name],
            
            // Inline field access expression
            FieldAccessExp f => new FieldAccessExp(Inline(f.Owner), f.FieldName, f.SchemaType),
            
            // Collection expression
            CollectionRootExp c => UnpackCollectionRootExp(c),
            
            // Iterator expression
            CollectionItemExp i => new CollectionItemExp(UnpackCollectionRootExp(i.Root), i.SchemaType),
            
            // Default expression
            DefaultExp de => new DefaultExp(Inline(de.Inner), de.Default),
            
            // Inline params expression
            ParamsExp pe => new ParamsExp(pe.Exps.Select(Inline).ToArray(), pe.SchemaType),
            
            // Already inline
            _ => exp
        };
        
        // Parse field access expression, special for display only field
        SchemaExp? ParseFieldAccess(VariableExp varExp, string[] paths)
        {
            // replace with field access expression
            AnySchemaType? type = varExp.SchemaType;
            SchemaExp owner = varExp;
            if (type is ArrayType arrayType)
            {
                type = arrayType.ElementSchemaType ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                
                // Only allow one collection root in one function call
                if (colSource != null && colSource.Collection != varExp) throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongCollection);
                colSource ??= new CollectionRootExp(varExp, varExp.SchemaType);
                iteratorExp ??= new CollectionItemExp(colSource, type);
                owner = iteratorExp;
            }

            foreach (var fieldName in paths)
            {
                if (type is StructType structType && structType.Fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)) is { } field)
                {
                    type = field.SchemeType;
                }
                else
                {
                    return null;
                }
            }
            
            return new FieldAccessExp(owner, string.Join(".", paths), type!);
        }

        // Get expression with visit count++ & Field Access support
        bool GetExpression(string name, out SchemaExp? value)
        {
            string[] access = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (expMaps.TryGetValue(access[0], out VariableExp? varExp))
            {
                SchemaExp exp = varExp;
                currAccessCount.Add(access[0]);
                
                if (access.Length > 1)
                {
                    if (ParseFieldAccess(varExp, access.Skip(1).ToArray()) is not { } fieldExp)
                    {
                        value = null;
                        return false;
                    }
                    exp = fieldExp;
                }

                value = exp;
                return true;
            }
            value = null;
            return false;
        }
        
        // Gen function call expression
        async Task GenFuncCallExpression(FunctionNodeExpression exp) {
            
            // reset status
            exp.Status = null;
            
            #region Name
            
            // Require name
            if (string.IsNullOrWhiteSpace(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpNoName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpNoName);
            }
            // No duplicate name
            else if (expMaps.ContainsKey(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpDuplicateName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpDuplicateName);
            }
            
            #endregion

            #region Func
            
            // Validate func
            if ((exp.FuncNode ?? (!string.IsNullOrWhiteSpace(exp.Func) ? await Context.GetSchemaTypeAsync(exp.Func) : null)) is not FunctionType expFuncType)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc);
            }
            exp.FuncNode = expFuncType;
            SchemaFuncInfo? expFuncInfo = await expFuncType.GetSchemaFuncInfoAsync(Context);
            if (expFuncInfo is null)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc);
            }

            #endregion
            
            #region Result Type & Generic
            
            // Generic types
            AnySchemaType?[] genericTypes = expFuncType.Generic.ToArray();

            // Validate return value
            exp.SchemaType ??= (!string.IsNullOrWhiteSpace(exp.Return) ? await Context.GetSchemaTypeAsync(exp.Return) : null);
            if (exp.SchemaType  is not { IsValueType: true })
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType);
            }

            // Match types
            AnySchemaType funcRetType = exp.SchemaType; // func return type may not match exp return type, require exp type check
            bool isColExp = (exp.Type ?? ExpressionType.Call) != ExpressionType.Call;

            // Check call type for return value, can't do it in visitor since we need generic type info
            switch (exp.Type)
            {
                case ExpressionType.Map:
                {
                    if (exp.SchemaType is ArrayType { ElementSchemaType: not null } arrayType)
                    {
                        funcRetType = arrayType.ElementSchemaType;
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn);
                    }
                    break;
                }
                case ExpressionType.Reduce:
                {
                    if (expFuncType.Args.Length is 0 or > 2)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc);
                    }
                    break;
                }
                case ExpressionType.First:
                case ExpressionType.Last:
                {
                    if (exp.SchemaType is ArrayType)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn);
                    }
                    funcRetType = SchemaContext.SystemBool;
                    break;
                }
                case ExpressionType.Filter:
                {
                    if (exp.SchemaType is ArrayType { ElementSchemaType: not null })
                    {
                        // pass
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn);
                    }
                    funcRetType = SchemaContext.SystemBool;
                    break;
                }
                case ExpressionType.Count:
                {
                    if (exp.SchemaType is not ScalarType { IsInt: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn);
                    }
                    funcRetType = SchemaContext.SystemBool;
                    break;
                }
                case ExpressionType.All:
                case ExpressionType.Any:
                {
                    if (exp.SchemaType is not ScalarType { IsBool: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn);
                    }
                    funcRetType = SchemaContext.SystemBool;
                    break;
                }
            }

            // Parse generic return type
            ParseGenericType(expFuncInfo.Return, expFuncType.ReturnNode, funcRetType);

            #endregion

            #region Args
            
            // build call arguments
            // check exp call first, get schema type for generic type
            SchemaExp[] args = new SchemaExp[expFuncType.Args.Length];

            // Check exp access first for generic types
            for (int eArgIdx = 0; eArgIdx < expFuncType.Args.Length; eArgIdx++)
            {
                FuncCallArg? arg = exp.Args.ElementAtOrDefault(eArgIdx);
                if (string.IsNullOrWhiteSpace(arg?.Name)) continue;
                if (!GetExpression(arg.Name, out SchemaExp? argExp))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                }
                arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await Context.GetSchemaTypeAsync(arg.Type) : argExp!.SchemaType;
                await SetArgExp(eArgIdx, argExp);
            }
            
            // Check const value
            for (int eArgIdx = 0; eArgIdx < expFuncType.Args.Length; eArgIdx++)
            {
                FunctionNodeArgument argDef = expFuncType.Args[eArgIdx];
                FuncCallArg? arg = exp.Args.ElementAtOrDefault(eArgIdx);
                if (!string.IsNullOrWhiteSpace(arg?.Name)) continue;

                // Validate type
                if (arg != null)
                {
                    arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await Context.GetSchemaTypeAsync(arg.Type) : ParseGenericType(expFuncInfo.Args[eArgIdx], argDef.SchemaType);
                    if (arg.SchemeType is not { IsValueType: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType);
                    }
                }

                // Const expression
                await SetArgExp(eArgIdx, arg?.Value == null ? null : new ConstantExp(arg.SchemeType!.CreateNode(arg.Value)!));
            }

            // For params
            {
                var argDef = expFuncType.Args.LastOrDefault();
                if (argDef?.Params == true)
                {
                    var paramType = ParseGenericType(expFuncInfo.Args.Last(), argDef.SchemaType);
                    if (paramType is ArrayType arrayType)
                        paramType = arrayType.ElementSchemaType;

                    for (int j = expFuncType.Args.Length; j < exp.Args.Length; j++)
                    {
                        var arg = exp.Args[j];

                        if (!string.IsNullOrWhiteSpace(arg.Name))
                        {
                            if (!GetExpression(arg.Name, out SchemaExp? argExp))
                            {
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                            }
                            arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await Context.GetSchemaTypeAsync(arg.Type) : paramType;
                            await SetArgExp(j, argExp);
                        }
                        else
                        {
                            if (arg.Value == null) continue;

                            arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await Context.GetSchemaTypeAsync(arg.Type) : paramType;
                            if (arg.SchemeType is not { IsValueType: true })
                            {
                                exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType);
                            }
                            await SetArgExp(j, new ConstantExp(arg.SchemeType.CreateNode(arg.Value)!));
                        }
                    }
                }
            }

            #endregion

            #region Variable

            // Validate collection exp
            if (isColExp && colSource == null)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
            }

            // build function call expression
            VariableExp callVarExp = new VariableExp(exp.Name, new FuncCallExp(expFuncType, args, exp.SchemaType!, exp.Type ?? ExpressionType.Call));

            // Add to maps
            expMaps[exp.Name] = callVarExp;
            results.Add(callVarExp); // reduce later

            return;

            #endregion

            #region Helper
            
            // Set argument expression
            async Task SetArgExp(int index, SchemaExp? chkArgExp = null)
            {
                // Gets the argument definition
                FunctionNodeArgument? argDef = expFuncType.Args.ElementAtOrDefault(index);
                Utility.Schema.SchemaParamTypeInfo? argInfo = expFuncInfo.Args.ElementAtOrDefault(index);
                if (argDef == null)
                {
                    argDef = expFuncType.Args.LastOrDefault();
                    if (argDef?.Params != true) return;
                    argInfo = expFuncInfo.Args.LastOrDefault();
                }

                SchemaExp? argExp = chkArgExp;
                
                // Params type check
                AnySchemaType? argType = argDef.SchemaType;
                if (argDef.Params == true && argType is ArrayType arrayType)
                    argType = arrayType.ElementSchemaType;
                
                // Collection expression check
                if (isColExp && argType is not ArrayType && (
                        argExp?.SchemaType is ArrayType { ElementSchemaType: not null } || 
                        argExp is FieldAccessExp { Owner: CollectionItemExp }))
                {
                    if (argDef.Params == true)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    }

                    // If has CollectionItemExp exp, the colSource must exist
                    if (colSource == null)
                    {
                        colSource = new CollectionRootExp(argExp, argExp.SchemaType!);
                        iteratorExp = new CollectionItemExp(colSource, argExp.SchemaType is ArrayType at ? at.ElementSchemaType! : argExp.SchemaType!);
                        argExp = iteratorExp;
                    }
                    else if (colSource.Collection is VariableExp && ( colSource.Collection == argExp ||
                                 argExp is FieldAccessExp{ Owner: CollectionItemExp iterExp } && iterExp.Root == colSource))
                    {
                        if (argExp is not FieldAccessExp) argExp = iteratorExp; // direct use iterator exp
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    }
                }

                // Gets the argument type
                argType = ParseGenericType(argInfo!, argDef.SchemaType, argExp?.SchemaType);
                if (argType is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                }
                if (argDef.Params == true && argExp == null) return;

                // Default expression & not iterator exp
                if (argDef.Default != null && argExp is not CollectionRootExp && argExp is not FieldAccessExp { Owner: CollectionItemExp })
                {
                    if (argExp == null)
                    {
                        argExp = new ConstantExp(argType.CreateNode(argDef.Default)!);
                    }
                    else
                    {
                        argExp = new DefaultExp(argExp, argType.CreateNode(argDef.Default)!);
                    }
                }

                // Combine params
                if (argDef.Params ?? false)
                {
                    if (argExp == null) return;
                    var old = args[expFuncType.Args.Length - 1] as ParamsExp;
                    args[expFuncType.Args.Length - 1] = new ParamsExp(old?.Exps.Append(argExp).ToArray() ?? [argExp], old?.SchemaType ?? (await Context.GetArraySchemaTypeAsync(argType))!);
                }

                // Nullable check
                else if (argExp == null && !(argDef.Nullable ?? false))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                }

                // Record argument expression
                else
                {
                    args[index] = argExp ?? new NullExp(argType);
                }
            }

            // Sets generic type
            AnySchemaType? ParseGenericType(Utility.Schema.SchemaParamTypeInfo typeInfo, AnySchemaType? origin = null, AnySchemaType? genType = null, bool isReturn = false)
            {
                if (typeInfo.Generic == null && origin is not GenericType)
                {
                    if (origin == null || genType == null || genType.CanBeUseAs(origin)) return origin ?? genType;
                    if (isReturn)
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType);
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    }
                }
                else
                {
                    int idx = typeInfo.Generic != null
                        ? Array.FindIndex(expFuncInfo.Generics, g => typeInfo.Generic == g.Generic)
                        : (((origin as GenericType)?.Index ?? 1) - 1);
                    if (idx < 0 || genericTypes[idx] != null && genType != null && genType is not GenericType && !genType.CanBeUseAs(genericTypes[idx]!))
                    {
                        if (isReturn)
                        {
                            exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType);
                        }
                        else
                        {
                            exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                        }
                    }
                    if (genType != null && genType is not GenericType)
                        genericTypes[idx] ??= genType;
                    return genericTypes[idx] ?? genType;
                }
            }
            
            #endregion
        }

        #endregion
        }

    /// <summary>
    /// Visit schema expression with all visitors
    /// </summary>
    public virtual async Task<SchemaExp> VisitSchemaExpAsync(SchemaExp exp)
    {
        foreach (IExpVisitor visitor in _visitors)
            exp = await visitor.VisitExpAsync(this, exp) ?? exp;
        return exp;
    }
    
    /// <summary>
    /// Compile the function to dynamic method
    /// </summary>
    public async Task<Delegate?> CompileAsync()
    {
        if (Function.TryGetRuntimeFuncCache<Delegate?>(GetType(), out Delegate? del))
            return del!;
        
        // Prepare
        try
        {
            FunctionTypeSchema funcSchema = await VisitFunctionType();
            
            // Prepare
            var paramExps = new ParameterExpression[funcSchema.Args.Length + 1];

            // Build the parameters, no generic type for custom methods
            // Always add SchemaContext as the first parameters for inner call
            _contextExp = Expression.Parameter(typeof(SchemaContext));
            paramExps[0] = _contextExp; 
            for (int i = 0; i < funcSchema.Args.Length; i++)
            {
                ArgumentExp arg = funcSchema.Args[i];
                ParameterExpression paramExp = Expression.Parameter(arg.SchemaType.ToCSharpType(arg.Nullable) 
                    ?? throw new Exception($"The {Function.Name} can't be compiled - expression compile failed"));
                paramExps[i + 1] = paramExp;
                _paramExpMap[arg.Name] = paramExp;
            }

            // Expression Tree -> Function Body
            List<Expression> expBlocks = [];
            _returnLabel = funcSchema.Exps.Any(e => e.Value is BreakExp) ? Expression.Label(funcSchema.Return) : null;
            
            ParameterExpression? finalVar = null;
            foreach (VariableExp exp in funcSchema.Exps)
            {
                Expression? result = await CompileSchemaExpAsync(exp.Value);
                if (result == null) throw new Exception($"The {Function.Name} can't be compiled - expression {exp.Name} compile failed");

                // exp = result
                ParameterExpression expRes = Expression.Parameter(result.Type);
                _variableExpMap.Add(exp.Name, expRes);
                
                // Logger @TODO
                //if (exp is FunctionNodeExpression callExp)
                //    expBlocks.Add(Expression.Call(paramExps[0], typeof(SchemaContext).GetMethod(nameof(SchemaContext.LogInformation))!, Expression.Constant($"Calling expression {callExp.Name}")));
                expBlocks.Add(Expression.Assign(expRes, result));
                finalVar = expRes;
            }
            
            if (finalVar == null) throw new Exception($"The {Function.Name} can't be compiled - no expression found");

            // Conversion last type
            Type lastType = funcSchema.Return;
            if (lastType != finalVar.Type)
            {
                ParameterExpression convExp = Expression.Variable(lastType, "_final");
                _variableExpMap.Add("_final", convExp);
                expBlocks.Add(Expression.Assign(convExp, ConvertExp(lastType, finalVar)));
                finalVar = convExp;
            }

            // Handle return label if existed
            if (_returnLabel != null)
            {
                expBlocks.Add(Expression.Return(_returnLabel, finalVar));
                expBlocks.Add(Expression.Label(_returnLabel, Expression.Default(_returnLabel.Type)));
            }

            // Build block
            BlockExpression blockExpr = Expression.Block(_variableExpMap.Values.ToArray(), expBlocks);

            // Build the dynamic method
            return Function.SetRuntimeFuncCache(GetType(), CompileMethod(lastType, paramExps, blockExpr));
        }
        catch (Exception ex)
        {
            Context.LogError(ex, "Failed to compile function {FunctionName}", Function.Name);
        }

        return null;
    }
    
    public Task<AnySchemaType?> GetSchemaTypeAsync(string schemaName) => Context.GetSchemaTypeAsync(schemaName);
    
    /// <summary>
    /// Gets the schema node of specific type
    /// </summary>
    public Task<T?> GetSchemaTypeAsync<T>(string schemaName) where T : AnySchemaType => Context.GetSchemaTypeAsync<T>(schemaName);
    
    /// <summary>
    /// Gets the app type
    /// </summary>
    public async Task<AppType?> GetAppTypeAsync(string name) => await Context.GetAppTypeAsync(name);
    
    /// <summary>
    /// Gets the array type for the element type
    /// </summary>
    /// <param name="elementType"></param>
    /// <returns></returns>
    public Task<ArrayType?> GetArrayType(AnySchemaType elementType) => Context.GetArraySchemaTypeAsync(elementType);
    
    /// <summary>
    /// Compile the schema expression to Expression
    /// </summary>
    public virtual async Task<Expression> CompileSchemaExpAsync(SchemaExp exp, Type? expectedType = null)
    {
        expectedType ??= exp.SchemaType.ToCSharpType();
        if (_compiledExpCache.TryGetValue(exp, out Expression? cachedExp))
            return ConvertExp(expectedType, cachedExp);
        
        #region Apply visitors
        
        for (int i = _visitors.Length - 1; i >= 0; i--)
        {
            Expression? resExp = await _visitors[i].CompileExpAsync(this, exp, expectedType);
            if (resExp != null) 
                return ConvertExp(expectedType, resExp);
        }
        
        #endregion 
        
        #region specific expressions
        
        switch (exp)
        {
            // Argument
            case ArgumentExp ae:
                return GetParameterExpression(ae.Name);
            
            // Variable
            case VariableExp ve:
                return GetParameterExpression(ve.Name);
            
            // Params
            case ParamsExp ps:
            {
                expectedType = expectedType.GetElementType() ?? expectedType; // make sure it's element type
                Expression[] arrayInits = new Expression[ps.Exps.Length];
                for (int k = 0; k < ps.Exps.Length; k++)
                {
                    arrayInits[k] = ConvertExp(expectedType, await CompileSchemaExpAsync(ps.Exps[k], expectedType));
                }
                return Expression.NewArrayInit(expectedType, arrayInits);
            }
            
            // Iterator
            case CollectionRootExp:
                throw new NotImplementedException("IteratorExpression compilation must be handled in visitor.");
                
            // { a, b, c }
            case StructResultExp structExp:
            {
                var resultVar = Expression.Variable(typeof(StructTypeNode));
                List<Expression> blockExps = [
                    Expression.Assign(resultVar, Expression.New(typeof(StructTypeNode).GetConstructors()[0], Expression.Constant(structExp.SchemaType), Expression.Constant(null)))
                ];
                MethodInfo objectAdd = typeof(StructTypeNode).GetMethod(nameof(StructTypeNode.SetField))!;
                foreach (var fieldExp in structExp.Fields)
                    blockExps.Add(Expression.Call(resultVar, objectAdd, Expression.Constant(fieldExp.Name, typeof(string)), ConvertExp(typeof(Object), await CompileSchemaExpAsync(fieldExp.Expression))));
                blockExps.Add(resultVar); // as the result
                
                return Expression.Block([resultVar], blockExps);
            }
        }
        
        #endregion

        #region Function Arguments

        // Default Function Call
        if (exp is not FuncCallExp funcCallExp) return Expression.Empty();

        // function validate
        SchemaFuncInfo callFuncInfo = await funcCallExp.Function.GetSchemaFuncInfoAsync(Context) ?? throw new Exception($"The function call {funcCallExp.Function.Name} can't be compiled");
        int useContext = (callFuncInfo.Sign & FUNC_SIGN_CONTEXT) == FUNC_SIGN_CONTEXT ? 1 : 0;

        // Prepare the call arguments
        Expression[] callArgs = new Expression[funcCallExp.Args.Length + useContext];
        if (useContext > 0) callArgs[0] = _contextExp!;

        // Prepare the call arguments
        for (int i = 0; i < funcCallExp.Args.Length; i++)
        {
            SchemaExp leaf = funcCallExp.Args[i];
            Type callType = (funcCallExp.Function.Args[i].SchemaType is GenericType ? funcCallExp.Args[i].SchemaType : funcCallExp.Function.Args[i].SchemaType)
                            ?.ToCSharpType(funcCallExp.Function.Args[i].Nullable) ?? throw new Exception($"The expression {i} argument type not valid.");

            if (leaf is CollectionRootExp)
                throw new Exception("IteratorExpression must be used in non-call exp.");
            
            // default
            callArgs[useContext + i] = ConvertExp(callType, await CompileSchemaExpAsync(leaf, callType));
        }

        // Prepare the function
        MethodInfo callMethod = callFuncInfo.Method!;
        // bool hasClosure = callFuncInfo.DynamicMethod != null && callFuncInfo.DynamicMethod.HasClosure();
        Type expReturnType = exp.SchemaType.ToCSharpType((callFuncInfo.Sign & FUNC_SIGN_NULLABLE_RET) > 0);
        Type expRetElement = funcCallExp.ExpType is ExpressionType.Map && exp.SchemaType is ArrayType arr
            ? arr.ElementSchemaType!.ToCSharpType()
            : expReturnType;

        // Make generic method for system defined methods
        Type?[] genTypes = callFuncInfo.Generics.Select(p =>
        {
            Utility.Schema.SchemaParamTypeInfo? info = null;
            Type? type = null;
            if (callFuncInfo.Return.Generic == p.Generic)
            {
                info = callFuncInfo.Return;
                type = expRetElement.GetNotNullType();
            }
            else
            {
                for (int j = 0; j < callFuncInfo.Args.Length; j++)
                {
                    Utility.Schema.SchemaParamTypeInfo sInfo = callFuncInfo.Args[j];
                    if (sInfo.Generic != p.Generic) continue;
                    info = sInfo;
                    type = callArgs[j + useContext].Type.GetNotNullType();
                    break;
                }
            }

            if (info == null || type == null) throw new InvalidOperationException($"The function call {funcCallExp.Function.Name} can't be compiled");
            if (info.Array && type.IsSZArray) return type.GetElementType();
            if ((info.List || info.Enumerable) && type.GetGenericArguments() is { Length: > 0} args) return args[0];
            return type;
        }).ToArray();
        
        // Generate generic method
        if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE && (callFuncInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
        {
            // Generate the generic method
            string genSign = string.Join('|', genTypes.Select(p => (Nullable.GetUnderlyingType(p!) ?? p!).FullName));
            callMethod = callFuncInfo.GenericMethods.GetOrAdd(genSign, _ => callFuncInfo.Method!.MakeGenericMethod(genTypes!));
        }
        
        // Use remote call
        else if ((callFuncInfo.Sign & FUNC_SIGN_REMOTE_CALL) == FUNC_SIGN_REMOTE_CALL)
        {
            // Generate the call
            var convCallArgs = new Expression[callArgs.Length + (useContext > 0 ? 2 : 3)];
            convCallArgs[0] = _contextExp!;
            convCallArgs[1] = Expression.Constant(callFuncInfo.Name);
            convCallArgs[2] = Expression.Constant(genTypes);
            if (useContext > 0)
            {
                for (int i = 1; i < callArgs.Length; i++)
                    convCallArgs[i + 2] = callArgs[i];
            }
            else
            {
                for (int i = 0; i < callArgs.Length; i++)
                    convCallArgs[i + 3] = callArgs[i];
            }

            callArgs = convCallArgs;
            int count = callArgs.Length - 3;
            callMethod = typeof(CompileContext).GetMethod($"CallRemoteFunction{count}", BindingFlags.Static | BindingFlags.NonPublic)!;

            // Make generic type
            callMethod = count > 0 ? callMethod.MakeGenericMethod(callArgs.Skip(3).Select(e => e.Type).Prepend(expRetElement).ToArray()) : callMethod;
        }
        
        #endregion

        // Direct call
        return funcCallExp.ExpType == ExpressionType.Call 
            ? GenMethodCallExp(callFuncInfo, callMethod, callArgs, expRetElement) 
            : Expression.Empty();
    }

    /// <summary>
    /// Gets the parameter expression by name
    /// </summary>
    public ParameterExpression GetParameterExpression(string name)
    {
        if (_paramExpMap.TryGetValue(name, out ParameterExpression? paramExp) || _variableExpMap.TryGetValue(name, out paramExp))
            return paramExp;
        throw new KeyNotFoundException($"Parameter '{name}' not found in function.");
    }

    /// <summary>
    /// Gets the return label
    /// </summary>
    public LabelTarget? GetReturnLabel() => _returnLabel;

    /// <summary>
    /// Gets the schema context parameter expression
    /// </summary>
    public ParameterExpression GetContext() => _contextExp!;

    /// <summary>
    /// Set compiled expression to cache
    /// </summary>
    public void SetCompiledExpression(SchemaExp exp, Expression compiledExp) => _compiledExpCache[exp] = compiledExp;

    #endregion
    
    #region Utility Methods

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
        return (Delegate)typeof(CompileContext)
            .GetMethod(nameof(CompileDynamicMethod), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(lambdaType)
            .Invoke(null, [blockExpr, paramExps])!;
    }

    // Convert expression
    public Expression ConvertExp(Type type, Expression exp)
    {
        if (type == exp.Type) return exp;
        if (type.IsAssignableFrom(exp.Type) || exp.Type == typeof(object)) return Expression.Convert(exp, type);

        Expression notNullExp = exp.Type.IsNullable() ? Expression.Call(exp, exp.Type.GetMethod("GetValueOrDefault", Type.EmptyTypes)!) : exp;
        Expression? resExp = null;
        Type notNullType = type.GetNotNullType();
        
        // convert csharp type to schema type node
        if (type.IsAssignableTo(typeof(AnySchemaNode)))
        {
            string schema = exp.Type.GetSchemaType(true) ?? throw new Exception($"The type {exp.Type.FullName} can't be converted to schema type node");
            AnySchemaType schemaType = Context.GetSchemaTypeAsync(schema).GetAwaiter().GetResult() ?? throw new Exception($"The schema type node {schema} not found");
            MethodInfo method = typeof(AnySchemaType).GetMethod(nameof(AnySchemaType.CreateNode))!;
            return Expression.Convert(Expression.Call(Expression.Constant(schemaType), method, notNullExp), type);
        }

        // simple type conversion
        if (!notNullExp.Type.IsAssignableTo(typeof(AnySchemaNode)))
        {
            resExp = Type.GetTypeCode(notNullType) switch
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
            resExp = Expression.Convert(Expression.Call(null, method, Expression.Constant(type), notNullExp), type);
        }

        // nullable result
        return notNullType == type
            ? resExp
            : Expression.Condition(Expression.NotEqual(exp, Expression.Constant(null, exp.Type)), resExp, Expression.Constant(null, type));
    }

    // Gen method call
    Expression GenMethodCallExp(SchemaFuncInfo callFuncInfo, MethodInfo callMethod, Expression[] callArgs, Type? returnType = null)
    {
        // Call the method
        Expression result;
        if ((callFuncInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC)
        {
            // Gets the task result
            MethodCallExpression callExp = Expression.Call(null, callMethod, callArgs);
            callExp = Expression.Call(callExp, callExp.Type.GetMethod(nameof(Task.GetAwaiter), Type.EmptyTypes)!);
            result = Expression.Call(callExp, callExp.Type.GetMethod(nameof(TaskAwaiter.GetResult), Type.EmptyTypes)!);
        }
        else if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result = Expression.Call(null, callMethod, callArgs);
        }
        else
        {
            result = GenDynamicCallExp(callMethod.ReturnType, callFuncInfo.DynamicMethod!, callArgs);
        }

        if (returnType != null && returnType != result.Type)
        {
            result = ConvertExp(returnType, result);
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

    // Compile lambda to method
    static T CompileDynamicMethod<T>(Expression block, params ParameterExpression[] inputs)
        => Expression.Lambda<T>(block, inputs).Compile();


    // Gets the call dynamic func
    static MethodInfo GetCallDynamicFunc(Type ret, params Type[] inputs)
    {
        MethodInfo method = typeof(CompileContext).GetMethod($"CallDynamicFunc{inputs.Length}", BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.MakeGenericMethod(inputs.Prepend(ret).ToArray());
    }
    
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
            // If static method expects a Closure as first parameter, and we do have a closure target,
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
    static async Task<TR?> CallRemoteFunction0<TR>(SchemaContext context, string name, string? rType) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray(), rType));
    static async Task<TR?> CallRemoteFunction1<TR, T1>(SchemaContext context, string name, string? rType, T1 v1) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1 }, rType));
    static async Task<TR?> CallRemoteFunction2<TR, T1, T2>(SchemaContext context, string name, string? rType, T1 v1, T2 v2) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2 }, rType));
    static async Task<TR?> CallRemoteFunction3<TR, T1, T2, T3>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3 }, rType));
    static async Task<TR?> CallRemoteFunction4<TR, T1, T2, T3, T4>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4 }, rType));
    static async Task<TR?> CallRemoteFunction5<TR, T1, T2, T3, T4, T5>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5 }, rType));
    static async Task<TR?> CallRemoteFunction6<TR, T1, T2, T3, T4, T5, T6>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6 }, rType));
    static async Task<TR?> CallRemoteFunction7<TR, T1, T2, T3, T4, T5, T6, T7>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7 }, rType));
    static async Task<TR?> CallRemoteFunction8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8 }, rType));
    static async Task<TR?> CallRemoteFunction9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9 }, rType));
    static async Task<TR?> CallRemoteFunction10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10 }, rType));
    static async Task<TR?> CallRemoteFunction11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11 }, rType));
    static async Task<TR?> CallRemoteFunction12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12 }, rType));
    static async Task<TR?> CallRemoteFunction13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13 }, rType));
    static async Task<TR?> CallRemoteFunction14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14 }, rType));
    static async Task<TR?> CallRemoteFunction15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15 }, rType));
    static async Task<TR?> CallRemoteFunction16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16 }, rType));
    static async Task<TR?> CallRemoteFunction17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(SchemaContext context, string name, string? rType, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16, T17 v17) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16, v17 }, rType));

    #endregion

    #endregion
}

/// <summary>
/// The function visit result schema
/// </summary>
public record FunctionTypeSchema(ArgumentExp[] Args, VariableExp[] Exps, Type Return);

/// <summary>
/// The function visit exception
/// </summary>
public class FunctionVisitException(SchemaNodeStatus status) : Exception(status.ToString())
{
    public SchemaNodeStatus Status { get; } = status;
}
