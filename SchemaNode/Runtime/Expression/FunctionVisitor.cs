using Microsoft.Win32.SafeHandles;
using SchemaNode.Context;
using SchemaNode.Enum;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
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

        var getExpression = (string name, out SchemaExpression? value) =>
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
        };

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
            if (argTypeNode == null || !argTypeNode.IsValueType)
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

            // valdiate func
            FunctionType? funcType = (exp.FuncNode ?? (!string.IsNullOrWhiteSpace(exp.Func) ? await context.GetSchemaTypeAsync(exp.Func) : null)) as FunctionType;
            if (funcType == null)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
            }
            exp.FuncNode = funcType;

            // generic types
            AnySchemeType?[] genericTypes = funcType.Generic.ToArray();
            var setGenericType = (AnySchemeType? origin, AnySchemeType? genType) =>
            {
                if (origin is not GenericTypeNode generic || genType == null || genType is GenericTypeNode) return origin ?? genType;
                if (genericTypes[generic.GenericIndex - 1] == null)
                    genericTypes[generic.GenericIndex - 1] = genType;
                return genericTypes[generic.GenericIndex - 1];
            };
            var getGenericType = (AnySchemeType? origin) =>
            {
                if (origin is not GenericTypeNode generic) return origin;
                return genericTypes[generic.GenericIndex - 1] ?? generic;
            };

            // validate return value
            AnySchemeType? returnType = !string.IsNullOrWhiteSpace(exp.Return) ? await context.GetSchemaTypeAsync(exp.Return) : null;
            if (returnType == null || !returnType.IsValueType)
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
            }
            exp.SchemaType = returnType;
            setGenericType(funcType.ReturnNode, returnType);

            // build call arguments
            // check exp call first, get schema type for generic type
            SchemaExpression[] args = new SchemaExpression[funcType.Args.Length];
            var setArgExp = async (int index, SchemaExpression? argExp = null) =>
            {
                var argDef = funcType.Args.ElementAtOrDefault(index);
                if (argDef == null)
                {
                    argDef = funcType.Args.Last();
                    if (argDef.Params != true) return;
                }
                var argType = setGenericType(argDef.SchemaType, argExp.SchemaType);

                // build argument expression
                if (argDef.Params ?? false)
                {
                    var old = args[funcType.Args.Length - 1] as ParamsExpression;
                    args[funcType.Args.Length - 1] = new ParamsExpression(
                        old?.Exps?.Append(argExp)?.ToArray() ?? [argExp], 
                        old?.SchemaType ?? (await context.GetArraySchemaTypeAsync(argType))!);
                }
                else
                {
                    args[index] = argExp;
                }
            };

            // check exp access first for generic types
            for (int j = 0; j < funcType.Args.Length; j++)
            {
                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
                if (string.IsNullOrWhiteSpace(arg?.Name)) continue;
                if (!getExpression(arg.Name, out SchemaExpression? argExp))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                arg.TypeNode ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : argExp!.SchemaType;
                await setArgExp(j, argExp);
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
                    await setArgExp(j, null);
                    continue;
                }
                arg.TypeNode ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : getGenericType(argDef.SchemaType);
                if (arg.TypeNode == null || !arg.TypeNode.IsValueType)
                {
                    arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
                }
                await setArgExp(j, new ConstantExpression(arg.TypeNode.CreateNode(arg.Value)));
            }

            for (int j = 0; j < exp.Args.Length; j++)
            {
                var arg = exp.Args[i];
                if (string.IsNullOrWhiteSpace(arg.Name)) continue;                
                if (!getExpression(arg.Name, out SchemaExpression? argExp))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                await setArgExp(j, argExp!);
            }

            // check missing arguments
            for (int j = exp.Args.Length; j < funcType.Args.Length; j++)
            {
                var argDef = funcType.Args[j];
                if (!(argDef.Nullable ?? false))
                {
                    argDef.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH);
                }
                
                // build argument expression
                if (argDef.Default != null)
                {

                }
                else
                {
                    args[j] = new NullExpression();
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
