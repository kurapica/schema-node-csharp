using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

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
                        result = new AppSchemaDataFilterUnary(unary.Type, transformedOperand!);
                        return true;
                    default:
                        return false;
                }
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
                                case ScalarTypeNode { SchemaType: ScalarType { IsBool: true } } scalar:
                                    result = scalar.ToValue<bool>() ? rightTransformed : leftVal;
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
                                case ScalarTypeNode { SchemaType: ScalarType { IsBool: true } } scalar:
                                    result = scalar.ToValue<bool>() ? leftTransformed : rightVal;
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
                                case ScalarTypeNode { SchemaType: ScalarType { IsBool: true } } scalar:
                                    result = scalar.ToValue<bool>() ? leftVal : rightTransformed;
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
                                case ScalarTypeNode { SchemaType: ScalarType { IsBool: true } } scalar:
                                    result = scalar.ToValue<bool>() ? rightVal : leftTransformed;
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
                    if (leftOp.Value is null || (leftOp.Value is AnySchemaNode leftNode ? leftNode.IsEmpty : leftOp.Value.GetType().IsArrayType()
                        ? SystemCollection.arrlen(leftOp.Value) == 0
                        : string.IsNullOrWhiteSpace(leftOp.Value.ToString())))
                    {
                        result = new AppSchemaDataFilterValue(false);
                        return true;
                    }
                }
                if (rightTransformed is AppSchemaDataFilterValue rightOp)
                {
                    if (rightOp.Value is null || 
                        (rightOp.Value is AnySchemaNode rightNode ? rightNode.IsEmpty : rightOp.Value.GetType().IsArrayType()
                            ? SystemCollection.arrlen(rightOp.Value) == 0
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
                    default:
                        return false;
                }
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
                FunctionType? funcType = await context.GetSchemaTypeAsync<FunctionType>(key);
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
            StructFieldSchema? field = structType.GetField(key);
            
            // only support scalar or locale string type
            if (field is not { SchemeType: ScalarType or EnumType } && !NS_SYSTEM_LOCALE_STRING.Equals(field?.SchemeType?.Name)) continue;

            AnySchemaType valueType = field.SchemeType is StructType ? SchemaContext.SystemString : field.SchemeType;
            
            var filterExp = value switch
            {
                JsonArray arr => new AppSchemaDataFilterBinary(LogicType.Contains,
                        new AppSchemaDataFilterValue(new ArrayTypeNode(field.SchemeType, arr)),
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
                        new AppSchemaDataFilterValue(valueType.CreateNode(val)!)),
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
                if (valueAccess.Value is ArrayTypeNode { Count: 1 } arrayNode)
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
                    if (field is { Complex: not null, SchemaType: not ScalarType }) continue;
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
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The startsWith right value must be string"))!);
                    case LogicType.NotStartsWith:
                        return sqlProvider.NotLikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notStartsWith right value must be string"))!);
                    case LogicType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The endsWith right value must be string"))!);
                    case LogicType.NotEndsWith:
                        return sqlProvider.NotLikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notEndsWith right value must be string"))!);
                    case LogicType.Match:
                        return sqlProvider.LikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The match right value must be string"))!);
                    case LogicType.NotMatch:
                        return sqlProvider.NotLikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix, fieldMaps),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
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
    /// Check if the struct node match the filter
    /// </summary>
    public static AnySchemaNode Test(this AppSchemaDataFilter filter, StructTypeNode structNode, AnySchemaType? expectType = null)
    {
        switch (filter)
        {
            case AppSchemaDataFilterField access:
            {
                // Check if the field is complex field
                return structNode.GetValueByPaths(access.Field) ?? throw new NotSupportedException($"The field not found in struct node: {access.Field}");
            }
            case AppSchemaDataFilterUnary unary:
            {
                AnySchemaNode result = unary.Operand.Test(structNode);
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                        return SchemaContext.SystemBool.CreateNode(result.IsEmpty)!;
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        return SchemaContext.SystemBool.CreateNode(!result.IsEmpty)!;
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
                        AnySchemaNode left = binary.Left.Test(structNode, SchemaContext.SystemBool);
                        AnySchemaNode right = binary.Right.Test(structNode, SchemaContext.SystemBool);
                        return binary.Type switch
                        {
                            LogicType.AndAlso => SchemaContext.SystemBool.CreateNode(left.ToValue<bool>() && right.ToValue<bool>())!,
                            _ => SchemaContext.SystemBool.CreateNode(left.ToValue<bool>() || right.ToValue<bool>())!
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
                        AnySchemaNode? left = null;
                        AnySchemaNode? right = null;

                        if (binary.Right is not AppSchemaDataFilterValue)
                        {
                            right = binary.Right.Test(structNode);
                            left = binary.Left.Test(structNode, right.SchemaType);
                        }
                        else
                        {
                            left = binary.Left.Test(structNode, SchemaContext.SystemInt); // use int as default
                            right = binary.Right.Test(structNode, left.SchemaType);
                        }
                        switch (left.SchemaType)
                        {
                            case ScalarType { IsInt: true }:
                                return Compare<int>(binary.Type, left, right);
                            case ScalarType { IsSingle: true }:
                                return Compare<float>(binary.Type, left, right);
                            case ScalarType { IsDouble: true }:
                                return Compare<double>(binary.Type, left, right);
                            case ScalarType { IsNumber: true }:
                                return Compare<decimal>(binary.Type, left, right);
                            case ScalarType { IsString: true } :
                                return Compare<string>(binary.Type, left, right);
                            case ScalarType { IsDate: true }:
                                return Compare<DateTime>(binary.Type, left, right);
                        }
                        break;
                    }

                    // collection
                    case LogicType.Contains:
                    case LogicType.NotContains:
                    {
                        AnySchemaNode val = binary.Right.Test(structNode);
                        AppSchemaDataFilterValue? container = binary.Left as AppSchemaDataFilterValue;
                        if (container == null || container.Value is not IEnumerable<object> enums || val.IsEmpty || val is not ScalarTypeNode) return SchemaContext.SystemBool.CreateNode(false)!;
                        
                        bool exist = enums.Any(e => e.ToString() == val.ToString());
                        return binary.Type switch
                        {
                            LogicType.Contains => SchemaContext.SystemBool.CreateNode(exist)!,
                            _ => SchemaContext.SystemBool.CreateNode(!exist)!,
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
                        string? left = binary.Left.Test(structNode, SchemaContext.SystemString).ToValue<string>();
                        string? right = binary.Right.Test(structNode, SchemaContext.SystemString).ToValue<string>();

                        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return SchemaContext.SystemBool.CreateNode(false)!;
                        return SchemaContext.SystemBool.CreateNode(binary.Type switch
                        {
                            LogicType.StartsWith => left.StartsWith(right),
                            LogicType.NotStartsWith => !left.StartsWith(right),
                            LogicType.EndsWith => left.EndsWith(right),
                            LogicType.NotEndsWith => !left.EndsWith(right),
                            LogicType.Match => left.Contains(right),
                            LogicType.NotMatch => !left.Contains(right),
                            _ => throw new NotSupportedException($"The logic type {binary.Type} can't be used as string compare")
                        })!;
                    }

                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }

                break;
            }
            case AppSchemaDataFilterArith arith:
            {
                AnySchemaNode? left = null;
                AnySchemaNode? right = null;
                
                if(arith.Right is not AppSchemaDataFilterValue)
                {
                    right = arith.Right.Test(structNode);
                    left = arith.Left.Test(structNode, right.SchemaType);
                }
                else
                {
                    left = arith.Left.Test(structNode, SchemaContext.SystemInt); // use int as default
                    right = arith.Right.Test(structNode, left.SchemaType);
                }

                switch (left.SchemaType)
                {
                    case ScalarType { IsInt: true }:
                        return CalcArith<int>(arith.Type, left, right);
                    case ScalarType { IsSingle: true }:
                        return CalcArith<float>(arith.Type, left, right);
                    case ScalarType { IsDouble: true }:
                        return CalcArith<double>(arith.Type, left, right);
                    case ScalarType { IsNumber: true }:
                        return CalcArith<decimal>(arith.Type, left, right);
                }
                break;
            }

            case AppSchemaDataFilterValue value:
            {
                if (value.Value is AnySchemaNode n) return n;
                if (expectType != null) return expectType.CreateNode(value.Value) ?? throw new NotSupportedException($"The filter value is not valid as {expectType.Name}");
                return SchemaContext.SystemString.CreateNode(value.Value.ToString() ?? "")!;
            }
        }

        throw new NotSupportedException("The expression type not supported");
    }

    static AnySchemaNode CalcArith<T>(ArithmeticType type, AnySchemaNode left, AnySchemaNode right) where T : INumber<T>
    {
        T leftVal = (left.IsEmpty ? default : left.ToValue<T>())!;
        T rightVal = (right.IsEmpty ? default : right.ToValue<T>())!;
        return left.SchemaType.CreateNode(type switch
        {
            ArithmeticType.Add => leftVal + rightVal,
            ArithmeticType.Subtract => leftVal - rightVal,
            ArithmeticType.Multiply => leftVal * rightVal,
            _ => throw new NotSupportedException($"The arithmetic type {type} not supported")
        })!;
    }

    static AnySchemaNode Compare<T>(LogicType type, AnySchemaNode left, AnySchemaNode right) where T : IComparable<T>
    {
        T leftVal = (left.IsEmpty ? default : left.ToValue<T>())!;
        T rightVal = (right.IsEmpty ? default : right.ToValue<T>())!;
        return SchemaContext.SystemBool.CreateNode(type switch
        {
            LogicType.Equal => leftVal.CompareTo(rightVal) == 0,
            LogicType.NotEqual => leftVal.CompareTo(rightVal) != 0,
            LogicType.GreaterThan => leftVal.CompareTo(rightVal) > 0,
            LogicType.GreaterEqual => leftVal.CompareTo(rightVal) >= 0,
            LogicType.LessThan => leftVal.CompareTo(rightVal) < 0,
            LogicType.LessEqual => leftVal.CompareTo(rightVal) <= 0,
            _ => throw new NotSupportedException($"The logic type {type} can't be used as value compare")
        })!;
    }
}