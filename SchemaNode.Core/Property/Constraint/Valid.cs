using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The validation property
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRING, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_DATE, SCHEMA_KIND_ENUM)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.valid")]
[Meta<Stackable>]
[RelationAssign<Valid>($"{nameof(Valid)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, NS_SYSTEM_BOOL)]
public class Valid : FuncCallProperty, IConstraintProperty
{
    public async Task<bool?> ValidateAsync(SchemaContext context, DataNode node)
    {
        FunctionType? func = !string.IsNullOrWhiteSpace(Value?.Func)
            ? await context.GetNodeTypeAsync<FunctionType>(Value.Func)
            : null;
        if (func == null) return null;

        object?[] args = new object[Value!.Args.Length];
        int arrayIndex = -1;
        for (int i = 0; i < args.Length; i++)
        {
            CallArg arg = Value.Args[i];
            FunctionNodeArgument? argInfo = func.Args.ElementAtOrDefault(i) ?? 
                                            (func.Args.LastOrDefault() is { Params: true } p  ? p : null);
            if (argInfo == null) return null; // skip if argument info is not found, or args exceed the non-params args but no params defined
            
            if (!string.IsNullOrWhiteSpace(arg.Source))
            {
                DataNode? value = node.GetAccessValue(arg.Source);
                args[i] = value;
                if (value is ArrayNode && argInfo.ValueType is not ArrayType)
                {
                    if (arrayIndex == -1) 
                        arrayIndex = i;
                    else
                    {
                        context.LogError($"Multiple array arguments are not supported in validation function: {func.Name}");
                        return null;
                    }
                }
            }
            else
            {
                args[i] = arg.Value;
            }
        }

        try
        {
            if (arrayIndex >= 0)
            {
                ArrayNode arrayNode = (args[arrayIndex] as ArrayNode)!;
                foreach (DataNode dataNode in arrayNode)
                {
                    args[arrayIndex] = dataNode;
                    if (await func.CallAsync<bool?>(context, args) == false)
                        return false;
                }

                return true;
            }
            return await func.CallAsync<bool?>(context, args);
        }
        catch (Exception e)
        {
            context.LogError(e.Message);
            return null;
        }
    }
}