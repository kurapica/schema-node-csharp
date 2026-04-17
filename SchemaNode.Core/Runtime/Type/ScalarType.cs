using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory scalar schema representation
/// </summary>
[Meta<AsErrorCode>("scalar_wrong_base", SCHEMA_KIND_ORDER_SCALAR * 100 + 1)]
public sealed class ScalarType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The base type name of the scalar
    /// </summary>
    public string? Base { get; private set; }

    #endregion

    #region Status

    /// <summary>
    /// The scalar value type flags
    /// </summary>
    public ScalarValueType ValueType { get; private set; } = ScalarValueType.None;

    /// <summary>Is a number type</summary>
    public bool IsNumber => (ValueType & ScalarValueType.Number) > 0;

    /// <summary>Is an integer type</summary>
    public bool IsInt => (ValueType & ScalarValueType.Integer) > 0;

    /// <summary>Is a float type</summary>
    public bool IsSingle => (ValueType & ScalarValueType.Single) > 0;

    /// <summary>Is a double type</summary>
    public bool IsDouble => (ValueType & ScalarValueType.Double) > 0;

    /// <summary>Is a bool type</summary>
    public bool IsBool => (ValueType & ScalarValueType.Boolean) > 0;

    /// <summary>Is a char type</summary>
    public bool IsChar => (ValueType & ScalarValueType.Char) > 0;

    /// <summary>Is a string type</summary>
    public bool IsString => (ValueType & ScalarValueType.String) > 0;

    /// <summary>Is a date type</summary>
    public bool IsDate => (ValueType & ScalarValueType.Date) > 0;

    /// <summary>Is a year type</summary>
    public bool IsYear => (ValueType & ScalarValueType.Year) > 0;

    /// <summary>Is a year-month type</summary>
    public bool IsYearMonth => (ValueType & ScalarValueType.YearMonth) > 0;

    /// <summary>Is a full date type</summary>
    public bool IsFullDate => (ValueType & ScalarValueType.FullDate) > 0;

    /// <summary>Is a GUID type</summary>
    public bool IsGuid => (ValueType & ScalarValueType.Guid) > 0;

    #endregion

    #region Property

    /// <summary>The up limit constraint value</summary>
    public object? UpLimit { get; private set; }

    /// <summary>The low limit constraint value</summary>
    public object? LowLimit { get; private set; }

    /// <summary>The pattern constraint</summary>
    public PatternProperty? Pattern { get; private set; }

    #endregion

    #region Ref

    /// <summary>The resolved base scalar type</summary>
    public ScalarType? BaseNode { get; private set; }

    #endregion

    #region Loading

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        // Load scalar-specific schema data
        ScalarSchema? scalar = schema.GetProperty<ScalarProperty>()?.Value;

        Base = scalar?.Base;

        if (scalar == null) Error = "no_definition";

        // Resolve base type
        if (!string.IsNullOrWhiteSpace(Base))
        {
            AnySchemaType? node = await context.Runtime.GetSchemaTypeAsync(context, Base, preload: preload);
            if (node is ScalarType baseScalar)
            {
                BaseNode = baseScalar;
                baseScalar.AddRef(this);
            }
            else
            {
                BaseNode = null;
                Error = "scalar_wrong_base";
            }
        }

        // Determine value type from name
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
            NS_SYSTEM_IDENTIFIER => ScalarValueType.String,
            _ => BaseNode?.ValueType ?? ScalarValueType.None
        };

        // Extract constraints
        UpLimit = Constraints?.OfType<UplimitNumberProperty>().FirstOrDefault()?.GetValue<object>()
                  ?? Constraints?.OfType<UplimitStringProperty>().FirstOrDefault()?.GetValue<object>();
        LowLimit = Constraints?.OfType<LowLimitNumberProperty>().FirstOrDefault()?.GetValue<object>()
                   ?? Constraints?.OfType<LowLimitStringProperty>().FirstOrDefault()?.GetValue<object>();
        Pattern = Constraints?.OfType<PatternProperty>().FirstOrDefault();
    }

    /// <inheritdoc />
    public override void ReleaseType()
    {
        BaseNode?.RemoveRef(this);
        BaseNode = null;
        base.ReleaseType();
    }

    #endregion
}

/// <summary>
/// Scalar value type classification flags
/// </summary>
[Flags]
public enum ScalarValueType
{
    None = 0,
    Boolean = 1,
    String = 1 << 1,
    Char = 1 << 2,
    Number = 1 << 3,
    Integer = 1 << 4,
    Single = 1 << 5,
    Double = 1 << 6,
    Date = 1 << 7,
    FullDate = 1 << 8,
    Year = 1 << 9,
    YearMonth = 1 << 10,
    Guid = 1 << 11,
}
