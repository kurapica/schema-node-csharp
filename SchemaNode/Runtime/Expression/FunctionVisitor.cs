using SchemaNode.Context;
using SchemaNode.Enum;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The function expression visitor to generate schema expression trees
/// </summary>
public static class FunctionVisitor
{
    /// <summary>
    /// Generate schema expressions for the given function type
    /// <remarks>The schema expression provide the structure of the function expressions, can be used for compiling, sql-conversion and etc.</remarks>
    /// </summary>
    public static async Task<List<SchemaExpression>> GenerateSchemaExpressions(this SchemaContext context, FunctionType func)
    {
        // C# function, no need to generate expression trees
        if (func.IsSystemCall) return [];

        List<SchemaExpression> results = [];
        Dictionary<string, SchemaExpression> expMaps = [];
        Dictionary<string, int> expAccessCount = [];
        IExpressionVisitor[] visitors = context.GetServices<IExpressionVisitor>().OrderByDescending(p => p.Priorty).ToArray();

        bool GetExpression(string name, out SchemaExpression? value)
        {
            if (expMaps.TryGetValue(name, out SchemaExpression? exp))
            {
                if (expAccessCount.TryGetValue(name, out int count))
                    expAccessCount[name] = count + 1;
                else
                    expAccessCount[name] = 1;
                value = exp;
                return true;
            }

            value = null;
            return false;
        }

        // Process arguments
        for (int i = 0; i < func.Args.Length; i++)
        {
            var arg = func.Args[i];

            // Check argument name
            if (string.IsNullOrWhiteSpace(arg.Name))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentNoName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentNoName, TYPE_FUNC_ARG_NAME_REQUIRED);
            }
            else if (expMaps.ContainsKey(arg.Name))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentDuplicateName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentDuplicateName, TYPE_FUNC_ARG_NAME_DUPLICATE);
            }
            else if(string.IsNullOrEmpty(arg.Type) || Regex.IsMatch(arg.Type, @"^[tT]\d*$"))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentNoType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentNoType, TYPE_FUNC_ARG_NO_TYPE);
            }

            AnySchemeType? argTypeNode = arg.SchemaType ?? await context.GetSchemaTypeAsync(arg.Type);
            if (argTypeNode is not { IsValueType: true })
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
            }
            arg.SchemaType = argTypeNode;
            arg.Type = argTypeNode.Name; // adjust name

            // Create argument expression
            expMaps[arg.Name] = new ArgumentExpression(arg.Name, i, argTypeNode);
        }

        // Process exps
        for (int i = 0; i < func.Exps.Length; i++)
        {
            // build function call expression first
            var exp = func.Exps[i];

            // validate name
            if (string.IsNullOrWhiteSpace(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpNoName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpNoName, TYPE_FUNC_EXP_NAME_REQUIRED);
            }
            else if (expMaps.ContainsKey(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpDuplicateName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpDuplicateName, TYPE_FUNC_EXP_NAME_CONFLICT_ARG);
            }

            // validate func
            FunctionType? funcType = (exp.FuncNode ?? (!string.IsNullOrWhiteSpace(exp.Func) ? await context.GetSchemaTypeAsync(exp.Func) : null)) as FunctionType;
            if (funcType == null)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
            }
            exp.FuncNode = funcType;

            // generic types
            AnySchemeType?[] genericTypes = funcType.Generic.ToArray();
            AnySchemeType? SetGenericType(AnySchemeType? origin, AnySchemeType? genType)
            {
                if (origin is not GenericTypeNode generic || genType == null || genType is GenericTypeNode) return origin ?? genType;
                genericTypes[generic.GenericIndex - 1] ??= genType;
                return genericTypes[generic.GenericIndex - 1];
            }
            AnySchemeType? GetGenericType(AnySchemeType? origin)
            {
                if (origin is not GenericTypeNode generic) return origin;
                return genericTypes[generic.GenericIndex - 1] ?? generic;
            }

            // validate return value
            AnySchemeType? returnType = !string.IsNullOrWhiteSpace(exp.Return) ? await context.GetSchemaTypeAsync(exp.Return) : null;
            if (returnType is not { IsValueType: true })
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
            }
            exp.SchemaType = returnType;
            SetGenericType(funcType.ReturnNode, returnType);

            // build call arguments
            // check exp call first, get schema type for generic type
            SchemaExpression[] args = new SchemaExpression[funcType.Args.Length];
            async Task SetArgExp(int index, SchemaExpression? argExp = null)
            {
                try
                {
                    var argDef = funcType.Args.ElementAtOrDefault(index);
                    if (argDef == null)
                    {
                        argDef = funcType.Args.Last();
                        if (argDef.Params != true) return;
                    }

                    var argType = SetGenericType(argDef.SchemaType, argExp?.SchemaType);

                    // build argument expression
                    if (argDef.Params ?? false)
                    {
                        if (argExp == null) return;
                        var old = args[funcType.Args.Length - 1] as ParamsExpression;
                        args[funcType.Args.Length - 1] = new ParamsExpression(old?.Exps?.Append(argExp)?.ToArray() ?? [argExp], old?.SchemaType ?? (await context.GetArraySchemaTypeAsync(argType))!);
                    }
                    else
                    {
                        args[index] = argExp ?? new NullExpression(argType!);
                    }
                }
                catch (Exception e)
                {
                    context.LogError(e, $"FunctionVisitor.SetArgExp {func.Name} - {exp.Name} - ArgIndex:{index}");
                }
            }

            // check exp access first for generic types
            for (int j = 0; j < funcType.Args.Length; j++)
            {
                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
                if (string.IsNullOrWhiteSpace(arg?.Name)) continue;
                if (!GetExpression(arg.Name, out SchemaExpression? argExp))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : argExp!.SchemaType;
                await SetArgExp(j, argExp);
            }

            // check const value
            for (int j = 0; j < funcType.Args.Length; j++)
            {
                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
                if (!string.IsNullOrWhiteSpace(arg?.Name)) continue;
                if (arg?.Value == null)
                {
                    if (!(argDef.Nullable ?? false))
                    {
                        argDef.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                    }
                    await SetArgExp(j, null);
                    continue;
                }
                arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : GetGenericType(argDef.SchemaType);
                if (arg.SchemeType is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
                }
                await SetArgExp(j, new ConstantExpression(arg.SchemeType.CreateNode(arg.Value)!));
            }

            for (int j = funcType.Args.Length; j < exp.Args.Length; j++)
            {

                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
                if (!string.IsNullOrWhiteSpace(arg?.Name))
                {
                    if (!GetExpression(arg.Name, out SchemaExpression? argExp))
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                    }
                    arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : argExp!.SchemaType;
                    await SetArgExp(j, argExp);
                }
                else
                {
                    if (arg?.Value == null)
                    {
                        if (!(argDef.Nullable ?? false))
                        {
                            argDef.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs,
                                TYPE_FUNC_EXP_ARGS_NOT_VALID);
                        }

                        await SetArgExp(j, null);
                        continue;
                    }

                    arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type)
                        ? await context.GetSchemaTypeAsync(arg.Type)
                        : GetGenericType(argDef.SchemaType);
                    if (arg.SchemeType is not { IsValueType: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType,
                            TYPE_FUNC_ARG_TYPE_NOT_VALID);
                    }

                    await SetArgExp(j, new ConstantExpression(arg.SchemeType.CreateNode(arg.Value)!));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// The function visit exception
    /// </summary>
    public class FunctionVisitException(SchemaNodeStatus status, string message) : Exception(message)
    {
        public SchemaNodeStatus Status { get; } = status;
    }
}
