using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components.Property.Constraint;

[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], 
    [ValueSchemaType.String, ValueSchemaType.Number, ValueSchemaType.Date], 
    includeArray: true, optionDepends:[nameof(RequireProperty)], 
    schemaType: NS_SYSTEM_SCHEMA_TYPE_RULE_VALID)]
public class ValidateProperty : SchemaProperty<string>, IConstraintProperty, ITypeRefProperty
{
    public async Task<bool?> ValidateScalarAsync(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null)
    {
        if (node.Value == null) return null;
        FunctionType? validFunc = !string.IsNullOrWhiteSpace(Value) ? await context.GetSchemaTypeAsync<FunctionType>(Value) : null;
        if (validFunc == null) return null;

        try
        {
            bool result = await validFunc.CallAsync<bool>(context, new object?[] { node.Value });
            return result;
        }
        catch (Exception ex)
        {
            context.LogError(ex, $"Error occurred while validating scalar value with function '{Value}'.");
            return false;
        }
    }
}
