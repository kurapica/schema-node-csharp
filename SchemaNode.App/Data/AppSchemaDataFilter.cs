using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Data.Sql;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using EnumType = SchemaNode.Runtime.EnumType;
using StructType = SchemaNode.Runtime.StructType;
using ValueType = SchemaNode.Runtime.ValueType;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace SchemaNode.Data;

public abstract record AppSchemaDataFilter;

/// <summary>
/// The app scheme data query result type
/// </summary>
public enum AppSchemaDataResult
{
    List,
    Count,
    Exist,
    First,
    Last,
    Field,
}

public sealed record AppSchemaDataFilterField(string Field): AppSchemaDataFilter;

public sealed record AppSchemaDataFilterUnary(LogicType Type, AppSchemaDataFilter Operand) : AppSchemaDataFilter;

public sealed record AppSchemaDataFilterBinary(LogicType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right) : AppSchemaDataFilter;

public sealed record AppSchemaDataFilterArith(ArithmeticType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right) : AppSchemaDataFilter;

public sealed record AppSchemaDataFilterValue(object Value) : AppSchemaDataFilter;

public sealed record AppSchemaDataOrder(string Field, bool Desc);

public static class AppSchemaDataFilterExtensions
{
    /// <summary>
    /// Combine two filters with AND ALSO
    /// </summary>
    public static AppSchemaDataFilter AndAlso(this AppSchemaDataFilter left, AppSchemaDataFilter right)
        => left is AppSchemaDataFilterValue ? right
                : right is AppSchemaDataFilterValue ? left
                : new AppSchemaDataFilterBinary(LogicType.AndAlso, left, right);
    
    /// <summary>
    /// Combine two filters with OR ELSE
    /// </summary>
    public static AppSchemaDataFilter OrElse(this AppSchemaDataFilter left, AppSchemaDataFilter right)
        => left is AppSchemaDataFilterValue ? right
                : right is AppSchemaDataFilterValue ? left
                : new AppSchemaDataFilterBinary(LogicType.OrElse, left, right);
    
    /// <summary>
    /// Validate and transform the filter
    /// </summary>
    public static bool Transform(this AppSchemaDataFilter filter, out AppSchemaDataFilter? result)
    {
        result = filter;
        switch (filter)
        {
            case AppSchemaDataFilterValue:
            case AppSchemaDataFilterField:
                return true;
            
            case AppSchemaDataFilterUnary unary:
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        if (!unary.Operand.Transform(out AppSchemaDataFilter? transformedOperand) ||
                            transformedOperand is not AppSchemaDataFilterField) return false;
                        result = new AppSchemaDataFilterUnary(unary.Type, transformedOperand);
                        return true;
                }
                break;
            case AppSchemaDataFilterBinary binary:
                if (!binary.Left.Transform(out AppSchemaDataFilter? leftTransformed)) return false;
                if (!binary.Right.Transform(out AppSchemaDataFilter? rightTransformed)) return false;

                // Logic simplification
                switch (binary.Type)
                {
                    case LogicType.AndAlso:
                    {
                        if (leftTransformed is AppSchemaDataFilterValue leftVal)
                        {
                            switch (leftVal.Value)
                            {
                                case BoolNode scalar:
                                    result = scalar.GetValue<bool>() ? rightTransformed : leftVal;
                                    return true; // Don't return false, maybe OrElse on the root
                                case bool left:
                                    result = left ? rightTransformed : leftVal;
                                    return true;
                                default:
                                    // type not supported
                                    return false;
                            }
                        }

                        if (rightTransformed is AppSchemaDataFilterValue rightVal)
                        {
                            switch (rightVal.Value)
                            {
                                case BoolNode scalar:
                                    result = scalar.GetValue<bool>() ? leftTransformed : rightVal;
                                    return true; // Don't return false, maybe OrElse on the root
                                case bool right:
                                    result = right ? leftTransformed : rightVal;
                                    return true;
                                default:
                                    // type not supported
                                    return false;
                            }
                        }

                        result = new AppSchemaDataFilterBinary(LogicType.AndAlso, leftTransformed!, rightTransformed!);
                        return true;
                    }
                    case LogicType.OrElse:
                    {
                        if (leftTransformed is AppSchemaDataFilterValue leftVal)
                        {
                            switch (leftVal.Value)
                            {
                                case BoolNode scalar:
                                    result = scalar.GetValue<bool>() ? leftVal : rightTransformed;
                                    return true;
                                case bool left:
                                    result = left ? leftVal : rightTransformed;
                                    return true;
                                default:
                                    // type not supported
                                    return false;
                            }
                        }

                        if (rightTransformed is AppSchemaDataFilterValue rightVal)
                        {
                            switch (rightVal.Value)
                            {
                                case BoolNode scalar:
                                    result = scalar.GetValue<bool>() ? rightVal : leftTransformed;
                                    return true; // Don't return false, maybe OrElse on the root
                                case bool right:
                                    result = right ? rightVal : leftTransformed;
                                    return true;
                                default:
                                    // type not supported
                                    return false;
                            }
                        }

                        result = new AppSchemaDataFilterBinary(LogicType.OrElse, leftTransformed!, rightTransformed!);
                        return true;
                    }
                }

                // if a OP null, should use isnull(a) instead, so return false here
                if (leftTransformed is AppSchemaDataFilterValue leftOp)
                {
                    if (leftOp.Value is null || (leftOp.Value is DataNode leftNode ? leftNode.IsEmpty : leftOp.Value.GetType().IsArrayType()
                        ? SystemCollection.length(leftOp.Value) == 0
                        : string.IsNullOrWhiteSpace(leftOp.Value.ToString())))
                    {
                        result = new AppSchemaDataFilterValue(false);
                        return true;
                    }
                }
                if (rightTransformed is AppSchemaDataFilterValue rightOp)
                {
                    if (rightOp.Value is null || 
                        (rightOp.Value is DataNode rightNode ? rightNode.IsEmpty : rightOp.Value.GetType().IsArrayType()
                            ? SystemCollection.length(rightOp.Value) == 0
                            : string.IsNullOrWhiteSpace(rightOp.Value.ToString())))
                    {
                        result = new AppSchemaDataFilterValue(false);
                        return true;
                    }
                }
                
                // Compare simplification
                switch (binary.Type)
                {
                    case LogicType.Equal:
                    case LogicType.NotEqual:
                    case LogicType.GreaterThan:
                    case LogicType.GreaterEqual:
                    case LogicType.LessThan:
                    case LogicType.LessEqual:
                    case LogicType.Contains:
                    case LogicType.NotContains:
                    case LogicType.StartsWith:
                    case LogicType.NotStartsWith:
                    case LogicType.EndsWith:
                    case LogicType.NotEndsWith:
                    case LogicType.Match:
                    case LogicType.NotMatch:
                    {
                        result = new AppSchemaDataFilterBinary(binary.Type, leftTransformed!, rightTransformed!);
                        return true;
                    }
                }
                break;
            case AppSchemaDataFilterArith arith:
                if (!arith.Left.Transform(out AppSchemaDataFilter? leftArith)) return false;
                if (!arith.Right.Transform(out AppSchemaDataFilter? rightArith)) return false;
                result = new AppSchemaDataFilterArith(arith.Type, leftArith!, rightArith!);
                return true;
        }

        return false;
    }

    internal static async Task<AppSchemaDataFilter?> ToAppSchemaDataFilterAsync(this JsonObject filter, SchemaContext context, StructType structType, FieldFilter[]? fieldFilters = null)
    {
        if (filter.IsEmpty()) return null;

        AppSchemaDataFilter? accessExp = null;
        foreach ((string key, JsonNode? value) in filter)
        {
            if (value == null || value.IsEmpty()) continue;
            
            // filter
            var filterMode = fieldFilters?.FirstOrDefault(f => f.Filter.Equals(key, StringComparison.OrdinalIgnoreCase))?.Mode 
                             ?? FieldFilterMode.Exactly;

            if (filterMode == FieldFilterMode.Filter)
            {
                // get the function
                FunctionType? funcType = await context.GetNodeTypeAsync<FunctionType>(key);
                if (funcType != null && value is JsonArray { Count: > 0 } arr)
                {
                    // Call filter func with policy filter compile context
                    AppSchemaDataFilter? f = await funcType.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, arr.Select(a => (object)a!).ToArray());
                    if (f != null)
                    {
                        accessExp = accessExp != null
                            ? new AppSchemaDataFilterBinary(LogicType.AndAlso, accessExp, f)
                            : f;
                    }
                }
                continue;
            }
            
            // get the field
            var field = structType.GetField(key);
            
            // only support scalar or locale string type
            if (field is not { Type: ScalarType or EnumType } && !NS_SYSTEM_LOCALE_STRING.Equals(field?.Type?.Name)) continue;

            var valueType = field.Type is StructType ? context.System.String : field.Type;
            
            var filterExp = value switch
            {
                JsonArray arr => new AppSchemaDataFilterBinary(LogicType.Contains,
                        new AppSchemaDataFilterValue(new ArrayNode(field.Type, arr)),
                        new AppSchemaDataFilterField(key)),
                JsonValue val => new AppSchemaDataFilterBinary(filterMode switch
                        {
                            FieldFilterMode.Exactly => LogicType.Equal,
                            FieldFilterMode.Prefix => LogicType.StartsWith,
                            FieldFilterMode.Suffix => LogicType.EndsWith,
                            FieldFilterMode.Contains => LogicType.Match,
                            _ => LogicType.Equal
                        }, 
                        new AppSchemaDataFilterField(key),
                        new AppSchemaDataFilterValue(valueType.From(val))),
                _ => null
            };
            
            accessExp = accessExp != null && filterExp != null
                ? new AppSchemaDataFilterBinary(LogicType.AndAlso, accessExp, filterExp)
                : filterExp;
        }

        return accessExp;
    }

    /// <summary>
    /// Try to convert the access exp to filter json object
    /// </summary>
    public static JsonObject? ToFilter(this AppSchemaDataFilter accessExp)
    {
        if (accessExp is not AppSchemaDataFilterBinary binaryAccessExp) return null;
        if (binaryAccessExp.Type == LogicType.AndAlso)
        {
            JsonObject? leftFilter = binaryAccessExp.Left.ToFilter();
            JsonObject? rightFilter = binaryAccessExp.Right.ToFilter();
            if (leftFilter == null) return null;
            if (rightFilter == null) return null;

            // merge
            foreach ((string key, JsonNode? value) in rightFilter)
                leftFilter[key] = value?.DeepClone();
            return leftFilter;
        }

        AppSchemaDataFilterField? accessNode = binaryAccessExp.Left as AppSchemaDataFilterField ??
                                               binaryAccessExp.Right as AppSchemaDataFilterField;
        if (accessNode == null) return null;
        AppSchemaDataFilterValue? valueAccess = (binaryAccessExp.Left == accessNode
            ? binaryAccessExp.Right
            : binaryAccessExp.Left) as AppSchemaDataFilterValue;
        if (valueAccess == null) return null;

        switch (binaryAccessExp.Type)
        {
            case LogicType.Equal:
                return new JsonObject
                {
                    [accessNode.Field] = valueAccess.Value.ToString()
                };
            case LogicType.Contains:
                if (valueAccess.Value is ArrayNode { Count: 1 } arrayNode)
                {
                    return new JsonObject
                    {
                        [accessNode.Field] = arrayNode[0]!.ToString()
                    };
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Convert the exp tree to SQL
    /// </summary>
    public static string ToSql(this AppSchemaDataFilter accessExp, ISqlProvider sqlProvider, DynamicTableSchema tableSchema, string prefix = "", Dictionary<string, string>? fieldMaps = null)
        => ToSql(sqlProvider, accessExp, tableSchema, string.IsNullOrWhiteSpace(prefix) ? "" : prefix.EndsWith('.') ? prefix : $"{prefix}.", fieldMaps);

    // To sql
    static string ToSql(ISqlProvider sqlProvider, AppSchemaDataFilter accessExp, DynamicTableSchema tableSchema, string prefix, Dictionary<string, string>? fieldMaps = null)
    {
        switch (accessExp)
        {
            case AppSchemaDataFilterField access:
            {
                // Check if the field is complex field
                string fieldName = "";
                foreach (DynamicTableField field in  tableSchema.GetDynamicTableFields(access.Field))
                {
                    // Works for locale string key
                    if (field is { Complex: not null, ValueType: not ScalarType }) continue;
                    fieldName = field.Name;
                    break;
                }
                if (string.IsNullOrWhiteSpace(fieldName))
                    throw new NotSupportedException($"The field not found in table schema: {access.Field}");
                return fieldMaps != null && fieldMaps.TryGetValue(fieldName, out string? name) ? name : $"{prefix}{sqlProvider.QuoteField(fieldName)}";
            }
            case AppSchemaDataFilterUnary unary:
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                        return sqlProvider.IsNull(ToSql(sqlProvider, unary.Operand, tableSchema, prefix, fieldMaps));
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        return sqlProvider.IsNotNull(ToSql(sqlProvider, unary.Operand, tableSchema, prefix, fieldMaps));
                    default:
                        throw new NotSupportedException($"The unary expression type not supported: {unary.Type}");
                }
            case AppSchemaDataFilterBinary binary:
                switch (binary.Type)
                {
                    case LogicType.AndAlso:
                    case LogicType.OrElse:
                    case LogicType.Equal:
                    case LogicType.NotEqual:
                    case LogicType.GreaterThan:
                    case LogicType.GreaterEqual:
                    case LogicType.LessThan:
                    case LogicType.LessEqual:
                        return sqlProvider.Binary(binary.Type,
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix, fieldMaps));
                    case LogicType.Contains:
                        return sqlProvider.In(
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix, fieldMaps),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicType.NotContains:
                        return sqlProvider.NotIn(
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix, fieldMaps),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicType.StartsWith:
                        return sqlProvider.LikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).Convert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The startsWith right value must be string"))!);
                    case LogicType.NotStartsWith:
                        return sqlProvider.NotLikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).Convert((binary.Right as AppSchemaDataFilterValue)?.Value
                                                           ?? throw new NotSupportedException("The notStartsWith right value must be string"))!);
                    case LogicType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).Convert((binary.Right as AppSchemaDataFilterValue)?.Value
                                                           ?? throw new NotSupportedException("The endsWith right value must be string"))!);
                    case LogicType.NotEndsWith:
                        return sqlProvider.NotLikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).Convert((binary.Right as AppSchemaDataFilterValue)?.Value
                                                           ?? throw new NotSupportedException("The notEndsWith right value must be string"))!);
                    case LogicType.Match:
                        return sqlProvider.LikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).Convert((binary.Right as AppSchemaDataFilterValue)?.Value
                                                           ?? throw new NotSupportedException("The contains right value must be string"))!);
                    case LogicType.NotMatch:
                        return sqlProvider.NotLikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).Convert((binary.Right as AppSchemaDataFilterValue)?.Value
                                                           ?? throw new NotSupportedException("The notMatch right value must be string"))!);
                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            case AppSchemaDataFilterArith arith:
                return sqlProvider.Arithmetic(arith.Type,
                    ToSql(sqlProvider, arith.Left, tableSchema, prefix, fieldMaps),
                    ToSql(sqlProvider, arith.Right, tableSchema, prefix, fieldMaps));
            case AppSchemaDataFilterValue value:
                return sqlProvider.Literal(value.Value);
        }

        throw new NotSupportedException("The expression type not supported");
    }

    /// <summary>
    /// Check if the struct node contains the filter
    /// </summary>
    public static DataNode Test(this AppSchemaDataFilter filter, SchemaContext context, StructNode structNode, ValueType? expectType = null)
    {
        switch (filter)
        {
            case AppSchemaDataFilterField access:
            {
                // Check if the field is complex field
                return structNode.GetAccessValue(access.Field) ?? throw new NotSupportedException($"The field not found in struct node: {access.Field}");
            }
            case AppSchemaDataFilterUnary unary:
            {
                DataNode result = unary.Operand.Test(context, structNode);
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                        return context.System.Bool.From(result.IsEmpty);
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        return context.System.Bool.From(!result.IsEmpty);
                    default:
                        throw new NotSupportedException($"The unary expression type not supported: {unary.Type}");
                }
            }
            case AppSchemaDataFilterBinary binary:
            {
                switch (binary.Type)
                {
                    // bool
                    case LogicType.AndAlso:
                    case LogicType.OrElse:
                    {
                        DataNode left = binary.Left.Test(context, structNode, context.System.Bool);
                        DataNode right = binary.Right.Test(context, structNode, context.System.Bool);
                        return binary.Type switch
                        {
                            LogicType.AndAlso => context.System.Bool.From(left.GetValue<bool>() && right.GetValue<bool>()),
                            _ => context.System.Bool.From(left.GetValue<bool>() || right.GetValue<bool>())
                        };
                    }

                    // compare
                    case LogicType.Equal:
                    case LogicType.NotEqual:
                    case LogicType.GreaterThan:
                    case LogicType.GreaterEqual:
                    case LogicType.LessThan:
                    case LogicType.LessEqual:
                    {
                        DataNode? left;
                        DataNode? right;

                        if (binary.Right is not AppSchemaDataFilterValue)
                        {
                            right = binary.Right.Test(context, structNode);
                            left = binary.Left.Test(context, structNode, right.Type);
                        }
                        else
                        {
                            left = binary.Left.Test(context, structNode, context.System.Int); // use int as default
                            right = binary.Right.Test(context, structNode, left.Type);
                        }

                        return context.System.Bool.From(left.Type switch
                        {
                            IntType => Compare<long>(binary.Type, left, right),
                            DecimalType => Compare<decimal>(binary.Type, left, right),
                            StringType => Compare<string>(binary.Type, left, right),
                            DateType => Compare<DateTimeOffset>(binary.Type, left, right),
                            _ => throw new NotSupportedException($"The binary expression type not supported: {binary.Type}")
                        });
                    }

                    // collection
                    case LogicType.Contains:
                    case LogicType.NotContains:
                    {
                        DataNode val = binary.Right.Test(context, structNode);
                        AppSchemaDataFilterValue? container = binary.Left as AppSchemaDataFilterValue;
                        if (container == null || container.Value is not IEnumerable<object> enums || val.IsEmpty || !val.GetType().IsSubclassOfGenericType(typeof(ScalarNode<>))) return context.System.Bool.From(false);
                        
                        bool exist = enums.Any(e => e.ToString() == val.ToString());
                        return binary.Type switch
                        {
                            LogicType.Contains => context.System.Bool.From(exist),
                            _ => context.System.Bool.From(!exist),
                        };
                    }

                    // string
                    case LogicType.StartsWith:
                    case LogicType.NotStartsWith:
                    case LogicType.EndsWith:
                    case LogicType.NotEndsWith:
                    case LogicType.Match:
                    case LogicType.NotMatch:
                    {
                        string? left = binary.Left.Test(context, structNode, context.System.String).GetValue<string>();
                        string? right = binary.Right.Test(context, structNode, context.System.String).GetValue<string>();

                        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return context.System.Bool.From(false);
                        return context.System.Bool.From(binary.Type switch
                        {
                            LogicType.StartsWith => left.StartsWith(right),
                            LogicType.NotStartsWith => !left.StartsWith(right),
                            LogicType.EndsWith => left.EndsWith(right),
                            LogicType.NotEndsWith => !left.EndsWith(right),
                            LogicType.Match => left.Contains(right),
                            LogicType.NotMatch => !left.Contains(right),
                            _ => throw new NotSupportedException($"The logic type {binary.Type} can't be used as string compare")
                        });
                    }

                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            }
            case AppSchemaDataFilterArith arith:
            {
                DataNode? left;
                DataNode? right;
                
                if(arith.Right is not AppSchemaDataFilterValue)
                {
                    right = arith.Right.Test(context, structNode);
                    left = arith.Left.Test(context, structNode, right.Type);
                }
                else
                {
                    left = arith.Left.Test(context, structNode, context.System.Int); // use int as default
                    right = arith.Right.Test(context, structNode, left.Type);
                }

                switch (left.Type)
                {
                    case IntType:
                        return CalcArith<long>(arith.Type, left, right);
                    case DecimalType:
                        return CalcArith<decimal>(arith.Type, left, right);
                }
                break;
            }

            case AppSchemaDataFilterValue value:
            {
                if (value.Value is DataNode n) return n;
                if (expectType != null) return expectType.From(value.Value) ?? throw new NotSupportedException($"The filter value is not valid as {expectType.Name}");
                return context.System.String.From(value.Value.ToString() ?? "");
            }
        }

        throw new NotSupportedException("The expression type not supported");
    }

    static DataNode CalcArith<T>(ArithmeticType type, DataNode left, DataNode right) where T : INumber<T>
    {
        T leftVal = (left.IsEmpty ? default(T) : left.GetValue<T>())!;
        T rightVal = (right.IsEmpty ? default(T) : right.GetValue<T>())!;
        return left.Type.From(type switch
        {
            ArithmeticType.Add => leftVal + rightVal,
            ArithmeticType.Subtract => leftVal - rightVal,
            ArithmeticType.Multiply => leftVal * rightVal,
            _ => throw new NotSupportedException($"The arithmetic type {type} not supported")
        });
    }

    static bool Compare<T>(LogicType type, DataNode left, DataNode right) where T : IComparable<T>
    {
        T leftVal = (left.IsEmpty ? default(T) : left.GetValue<T>())!;
        T rightVal = (right.IsEmpty ? default(T) : right.GetValue<T>())!;
        return type switch
        {
            LogicType.Equal => leftVal.CompareTo(rightVal) == 0,
            LogicType.NotEqual => leftVal.CompareTo(rightVal) != 0,
            LogicType.GreaterThan => leftVal.CompareTo(rightVal) > 0,
            LogicType.GreaterEqual => leftVal.CompareTo(rightVal) >= 0,
            LogicType.LessThan => leftVal.CompareTo(rightVal) < 0,
            LogicType.LessEqual => leftVal.CompareTo(rightVal) <= 0,
            _ => throw new NotSupportedException($"The logic type {type} can't be used as value compare")
        };
    }
}