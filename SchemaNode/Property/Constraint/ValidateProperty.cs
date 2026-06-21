using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.String, ValueSchemaType.Number, ValueSchemaType.Date], 
    includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class ValidateProperty : SchemaProperty<ValidFuncCall>, IConstraintProperty, ITypeRefProperty
{
    public async Task<bool?> ValidateScalarAsync(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        if (node.Value == null || string.IsNullOrWhiteSpace(Value?.Func)) return null;
        FunctionType? validFunc = !string.IsNullOrWhiteSpace(Value?.Func) ? await context.GetSchemaTypeAsync<FunctionType>(Value.Func) : null;
        if (validFunc == null || validFunc.ReturnNode == null || validFunc.ReturnNode is not ScalarType { IsBool: true }) return null;

        try
        {
            bool result = await validFunc.CallAsync<bool>(context, Value!.Args.Select(arg => {
                if (NODE_SELF.Equals(arg.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return node.Value;
                }
                else
                {
                    return arg.Value;
                }
            }).ToArray());
            return result;
        }
        catch (Exception ex)
        {
            context.LogError(ex, $"Error occurred while validating scalar value with function '{Value}'.");
            return false;
        }
    }
}

/// <summary>
/// The function expressions
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_FUNC}.valid")]
public sealed class ValidFuncCall
{
    /// <summary>
    /// The validation function
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALID)]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The argument list
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [] ;
}