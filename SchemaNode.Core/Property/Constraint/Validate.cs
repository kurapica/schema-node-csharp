using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Runtime.NodeType;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRING, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_INT, SCHEMA_KIND_DATE, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForValueTypes>(NS_SYSTEM_STRING, NS_SYSTEM_NUMBER, NS_SYSTEM_DATE)]
[Meta<OptionDepends>(typeof(Require))]
public class Validate : Property<ValidFuncCall>, IConstraintProperty, ITypeRefProperty
{
    public async Task<bool?> ValidateScalarAsync(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        if (node.Value == null || string.IsNullOrWhiteSpace(Value?.Func)) return null;
        FunctionType? validFunc = !string.IsNullOrWhiteSpace(Value?.Func) ? await context.GetNodeTypeAsync<FunctionType>(Value.Func) : null;
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