using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory scalar schema representation
/// </summary>
public sealed class ScalarType: AnySchemaType
{
    #region Data
     
    /// <summary>
    /// The base type of the scalar
    /// </summary>
    public string? Base { get; private set; }

    #endregion
     
    #region Status
     
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Scalar;

    /// <summary>
    /// Is value type
    /// </summary>
    public override bool IsValueType => true;

    /// <summary>
    /// The scalar value type
    /// </summary>
    public ScalarValueType ValueType { get; private set; } = ScalarValueType.None;

    /// <summary>
    /// The type is number
    /// </summary>
    public bool IsNumber => (ValueType & ScalarValueType.Number) > 0;
     
    /// <summary>
    /// the type is int
    /// </summary>
    public bool IsInt  => (ValueType & ScalarValueType.Integer) > 0;
     
    /// <summary>
    /// the type is single
    /// </summary>
    public bool IsSingle => (ValueType & ScalarValueType.Single) > 0;
     
    /// <summary>
    /// The type is number
    /// </summary>
    public bool IsDouble => (ValueType & ScalarValueType.Double) > 0;
     
    /// <summary>
    /// The type is bool
    /// </summary>
    public bool IsBool => (ValueType & ScalarValueType.Boolean) > 0;

    /// <summary>
    /// The type is char
    /// </summary>
    public bool IsChar => (ValueType & ScalarValueType.Char) > 0;

    /// <summary>
    /// The type is string
    /// </summary>
    public bool IsString => (ValueType & ScalarValueType.String) > 0;
     
    /// <summary>
    /// The type is date
    /// </summary>
    public bool IsDate => (ValueType & ScalarValueType.Date) > 0;
     
    /// <summary>
    /// The type is year
    /// </summary>
    public bool IsYear => (ValueType & ScalarValueType.Year) > 0;
     
    /// <summary>
    /// The type is year month
    /// </summary>
    public bool IsYearMonth => (ValueType & ScalarValueType.YearMonth) > 0;
     
    /// <summary>
    /// The type is full date
    /// </summary>
    public bool IsFullDate => (ValueType & ScalarValueType.FullDate) > 0;

    #endregion

    #region Property

    /// <summary>
    /// The default unit of the scalar value
    /// </summary>
    public LocaleString? Unit => Properties?.FirstOrDefault(p => p is UnitProperty) is UnitProperty unit ? unit.Value : null;

    /// <summary>
    /// The up limit
    /// </summary>
    public object? UpLimit { get; private set; }

    /// <summary>
    /// The low limit
    /// </summary>
    public object? LowLimit { get; private set; }

    /// <summary>
    /// The pattern
    /// </summary>
    public Pattern[]? Pattern { get; private set; }

    #endregion

    #region Ref

    /// <summary>
    /// The base node
    /// </summary>
    public ScalarType? BaseNode { get; private set; }
     
    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
     {
        ScalarSchema? scalar = schema.Scalar;

        // Data
        Base = scalar?.Base;

        // Status
        if (scalar == null) Status = SchemaNodeStatus.NoDefinition;

        // Base
        if (!string.IsNullOrWhiteSpace(Base))
        {
            AnySchemaType? node = await context.GetSchemaTypeAsync(Base, preload: preload);
            if (node is ScalarType snode)
            {
                BaseNode = snode;
                snode.AddRef(this);
            }
            else
            {
                BaseNode = null;
                Status = SchemaNodeStatus.ScalarHasWrongBase;
            }
        }

        // Value Type
        ValueType = schema.Name.ToLowerInvariant() switch
        {
            NS_SYSTEM_CHAR => ScalarValueType.Char | ScalarValueType.String,
            NS_SYSTEM_BOOL => ScalarValueType.Boolean,
            NS_SYSTEM_DATE => ScalarValueType.Date,
            NS_SYSTEM_NUMBER => ScalarValueType.Number,
            NS_SYSTEM_DOUBLE => ScalarValueType.Double | ScalarValueType.Number,
            NS_SYSTEM_FLOAT => ScalarValueType.Single | ScalarValueType.Number,
            NS_SYSTEM_PERCENT => ScalarValueType.Single | ScalarValueType.Number,
            NS_SYSTEM_INT => ScalarValueType.Integer | ScalarValueType.Number,
            NS_SYSTEM_FULL_DATE => ScalarValueType.FullDate | ScalarValueType.Date,
            NS_SYSTEM_STRING => ScalarValueType.String,
            NS_SYSTEM_YEAR => ScalarValueType.Year | ScalarValueType.Integer | ScalarValueType.Number,
            NS_SYSTEM_YEARMONTH => ScalarValueType.YearMonth | ScalarValueType.Date,
            NS_SYSTEM_GUID => ScalarValueType.Guid | ScalarValueType.String,
            _ => BaseNode?.ValueType ?? ScalarValueType.None
        };

        // Properties
        UpLimit = Properties?.FirstOrDefault(p => p.Name.Equals(PROPERTY_UPLIMIT, StringComparison.OrdinalIgnoreCase)) is IConstraintProperty up ? up.GetValue<object>() : null;
        LowLimit = Properties?.FirstOrDefault(p => p.Name.Equals(PROPERTY_LOWLIMIT, StringComparison.OrdinalIgnoreCase)) is IConstraintProperty low ? low.GetValue<object>() : null;
        Pattern = Constraints?.FirstOrDefault(p => p is PatternProperty) is PatternProperty pattern ? pattern.Value : null;
    }

    /// <summary>
    /// Gets the up limit
    /// </summary>
    public T? GetUplimit<T>() where T : struct
    {
        if (UpLimit == null) return null;
        object? uplimit = Utility.Extension.TryConvert(typeof(T), UpLimit);
        if (uplimit == null) return null;
        return (T)uplimit;
    }

    /// <summary>
    /// Gets the low limit
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetLowlimit<T>()
    {
        if (LowLimit == null) return default;
        object? lowlimit = Utility.Extension.TryConvert(typeof(T), LowLimit);
        if (lowlimit == null) return default;
        return (T)lowlimit;
    }

    /// <inheritdoc />
    public override void Release()
    {
        BaseNode?.RemoveRef(this);
    }

    /// <inheritdoc />
    public override async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        await Task.Yield();
        if (value is not JsonValue val || val.IsEmpty())
            return (null, TYPE_VALUE_NOT_VALID);

        // validate the scalar value
        string strVal = value.ToString();
        AnySchemaNode result = new ScalarTypeNode(this);

        // check with type
        try
        {
            if (IsYear)
            {
                if (long.TryParse(strVal, out long year))
                {
                    // pass
                }
                else if (TryParseDateTimeOffset(strVal, out DateTimeOffset? dateTime))
                {
                    year = SystemCalendar.getyear(context, dateTime!.Value);
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }

                result.Value = year;
            }
               
            else if (IsInt)
            {
                if (!long.TryParse(strVal, out long lval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = lval;
            }
               
            else if (IsSingle)
            {
                if (!float.TryParse(strVal, out float fval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = fval;
            }
               
            else if (IsDouble)
            {
                if (!double.TryParse(strVal, out double dval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = dval;
            }
               
            else if (IsNumber)
            {
                if (!decimal.TryParse(strVal, out decimal mval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = mval;
            }
               
            else if (IsBool)
            {
                if (TryParseBoolValue(strVal, out bool bval))
                {
                    result.Value = bval;
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }
            }
               
            else if (IsString)
            {
                result.Value = strVal;
            }
               
            else if (IsDate)
            {
                if (TryParseDateTimeOffset(strVal, out DateTimeOffset? date))
                {
                    result.Value = date!.Value;
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }
            }

            // Constraint validation
            if (Constraints is { Length: > 0 })
            {
                foreach (IConstraintProperty constraint in Constraints)
                {
                    if (constraints != null && constraints.FirstOrDefault(c => c.GetType() == constraint.GetType()) is IConstraintProperty cst && cst.HasValue)
                    {
                        if (await cst.ValidateScalarAsync(context, (ScalarTypeNode)result) == false)
                            return (null, TYPE_VALUE_NOT_VALID);
                    }
                    else if (await constraint.ValidateScalarAsync(context, (ScalarTypeNode)result) == false)
                        return (null, TYPE_VALUE_NOT_VALID);
                }
            }

            return (result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.GetInnermostException().Message);
        }
        return (null, TYPE_VALUE_NOT_VALID);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemaType other, bool exactly = false) =>
        base.CanBeUseAs(other, exactly) 
        || other switch
        {
            ScalarType scalar =>
            scalar.IsString || 
            (scalar.IsInt 
                ? IsInt
                : (scalar.IsNumber 
                        ? IsNumber 
                        : (scalar.ValueType & ValueType) > 0)),
            EnumType @enum => @enum.ValueType switch
            {
                EnumValueType.String => IsString,
                EnumValueType.Int => IsInt,
                EnumValueType.Flags => IsInt,
                _ => false
            },
            _ => false
        };

    /// <inheritdoc />
    public override bool IsIndexable => ((ValueType & ScalarValueType.Indexable) > 0) 
                                         || (ValueType & ScalarValueType.String) > 0 && UpLimit is <= ENTITY_PRIMARY_KEY_MAX_LEN;

    public override IEnumerable<AnySchemaType> GetDependNodes()
    {
        if (BaseNode != null)
            yield return BaseNode;
    }

    /// <summary>
    /// Try parse bool value from string
    /// </summary>
    static bool TryParseBoolValue(string value, out bool ret)
    {
        ret = false;
        if (string.IsNullOrEmpty(value))
            return false;
        value = value.ToLower();
        switch (value)
        {
            case "true":
                ret = true;
                return true;
            case "false":
                ret = false;
                return true;
            default:
                {
                    if (!int.TryParse(value, out int val) || val is < 0 or > 1)
                        return false;
                    ret = val == 1;
                    return true;
                }
        }
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(ScalarType? schema)
    {
        return schema?.ToSchema().With(new ScalarSchema
        {
            Base = schema.Base
        });
    }
     
     #endregion
}