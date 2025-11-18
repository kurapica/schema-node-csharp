using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory scalar schema representation
/// </summary>
public class ScalarType: AnySchemeType
{
    #region Data
     
    /// <summary>
    /// The base type of the scalar
    /// </summary>
    public string? Base { get; private set; }
     
    /// <summary>
    /// The default unit of the scalar value
    /// </summary>
    public LocaleString? Unit { get; private set; }
     
    /// <summary>
    /// The default low limit of the scalar value
    /// </summary>
    public decimal? LowLimit { get; private set; }
     
    /// <summary>
    /// The default up limit of the scalar value
    /// </summary>
    public decimal? UpLimit { get; private set; }
     
    /// <summary>
    /// The default error message
    /// </summary>
    public LocaleString? Error { get; private set; }
     
    /// <summary>
    /// The regex of the scalar value
    /// </summary>
    public string? Regex { get; private set; }
     
    /// <summary>
    /// The white list function
    /// </summary>
    public string? WhiteList { get; set; }
    
    /// <summary>
    /// As suggest
    /// </summary>
    public bool? AsSuggest { get; set; }
     
    /// <summary>
    /// The function to validate the scalar value in frontend
    /// </summary>
    public string? PreValid { get; private set; }
     
    /// <summary>
    /// The function to validate the scalar value in backend
    /// </summary>
    public string? PostValid { get; private set; }
     
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
     
    #endregion
     
    #region Status
     
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Scalar;

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
     
    #region Ref
     
    /// <summary>
    /// The base node
    /// </summary>
    public ScalarType? BaseNode { get; private set; }
     
    /// <summary>
    /// The post validation function node
    /// </summary>
    public FunctionType? PostValidNode { get; private set; }

    /// <summary>
    /// The pre validation function node
    /// </summary>
    public FunctionType? PreValidNode { get; private set; }

    /// <summary>
    /// The whitelist function node
    /// </summary>
    public FunctionType? WhiteListNode { get; private set; }

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
     {
        ScalarSchema? scalar = schema.Scalar;

        // Data
        Base = scalar?.Base;
        Unit = scalar?.Unit;
        LowLimit = scalar?.LowLimit;
        UpLimit = scalar?.UpLimit;
        Error = scalar?.Error;
        Regex = scalar?.Regex;
        WhiteList = scalar?.WhiteList;
        AsSuggest = scalar?.AsSuggest;
        PreValid = scalar?.PreValid;
        PostValid = scalar?.PostValid;
        Additional = scalar?.Additional;

        // Status
        if (scalar == null) Status = SchemaNodeStatus.NoDefinition;

        // Relationship
        if (!string.IsNullOrWhiteSpace(Base))
        {
            AnySchemeType? node = await context.GetSchemaTypeAsync(Base, preload: preload);
            if (node != null && node is ScalarType snode)
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

        if (!string.IsNullOrWhiteSpace(PostValid))
        {
            AnySchemeType? node = await context.GetSchemaTypeAsync(PostValid, preload: preload);
            if (node != null && node is FunctionType fnode)
            {
                PostValidNode = fnode;
                fnode.AddRef(this);
            }
            else
            {
                PostValidNode = null;
                Status = SchemaNodeStatus.ScalarHasWrongPostValid;
            }
        }

        if (!string.IsNullOrWhiteSpace(PreValid))
        {
            AnySchemeType? node = await context.GetSchemaTypeAsync(PreValid, preload: preload);
            if (node != null && node is FunctionType fnode)
            {
                PreValidNode = fnode;
                fnode.AddRef(this);
            }
            else
            {
                PreValidNode = null;
                Status = SchemaNodeStatus.ScalarHasWrongPreValid;
            }
        }

        if (!string.IsNullOrWhiteSpace(WhiteList))
        {
            AnySchemeType? node = await context.GetSchemaTypeAsync(WhiteList, preload: preload);
            if (node != null && node is FunctionType fnode)
            {
                WhiteListNode = fnode;
                fnode.AddRef(this);
            }
            else
            {
                WhiteListNode = null;
                Status = SchemaNodeStatus.ScalarHasWrongWhiteList;
            }
        }

        // Value Type
        ValueType = schema.Name.ToLowerInvariant() switch
        {
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
    }

    /// <inheritdoc />
    public override void Release()
     {
          BaseNode?.RemoveRef(this);
          PostValidNode?.RemoveRef(this);
     }

    /// <inheritdoc />
    public override async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
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
                else if (DateTime.TryParse(strVal, out DateTime dateTime))
                {
                    year = SystemDate.GetYear(dateTime);
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }

                if (LowLimit > year || UpLimit < year)
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = year;
                return (result, null);
            }
               
            else if (IsInt)
            {
                if (!long.TryParse(strVal, out long lval) || (LowLimit > lval || UpLimit < lval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = lval;
                return (result, null);
            }
               
            else if (IsSingle)
            {
                if (!float.TryParse(strVal, out float fval) || (LowLimit > (decimal?)fval || UpLimit < (decimal?)fval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = fval;
                return (result, null);
            }
               
            else if (IsDouble)
            {
                if (!double.TryParse(strVal, out double dval) || (LowLimit > (decimal?)dval || UpLimit < (decimal?)dval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = dval;
                return (result, null);
            }
               
            else if (IsNumber)
            {
                if (!decimal.TryParse(strVal, out decimal mval) || (LowLimit > mval || UpLimit < mval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = mval;
                return (result, null);
            }
               
            else if (IsBool)
            {
                if (TryParseBoolValue(strVal, out bool bval))
                {
                    result.Value = bval;
                    return (result, null);
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }
            }
               
            else if (IsString)
            {
                if (LowLimit > strVal.Length || UpLimit < strVal.Length)
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = strVal;
                return (result, null);
            }
               
            else if (IsDate)
            {
                if (DateTime.TryParse(strVal, out DateTime date))
                {
                    result.Value = date;
                    return (result, null);
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.GetInnermostException().Message);
        }
        return (null, TYPE_VALUE_NOT_VALID);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemeType other) =>
          base.CanBeUseAs(other) ||
          other switch
          {
               ScalarType scalar =>
                scalar.IsString || 
                (scalar.IsInt 
                    ? IsInt
                    : (scalar.IsNumber 
                            ? IsNumber 
                            : scalar.ValueType == ValueType)),
               EnumType @enum => @enum.ValueType switch
               {
                    EnumValueType.String => IsSingle,
                    EnumValueType.Int => IsInt,
                    EnumValueType.Flags => IsInt,
                    _ => false
               },
               _ => false
          };

    /// <inheritdoc />
    public override bool IsIndexable => ((ValueType & ScalarValueType.Indexable) > 0) 
                                         || (ValueType & ScalarValueType.String) > 0 && UpLimit is <= 128;

    public override IEnumerable<AnySchemeType> GetDependNodes()
    {
        if (BaseNode != null)
            yield return BaseNode;

        if (PostValidNode != null)
            yield return PostValidNode;

        if (PreValidNode != null)
            yield return PreValidNode;

        if (WhiteListNode != null)
            yield return WhiteListNode;
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
          if (schema == null) return null;
          NodeSchema nodeSchema = schema.ToSchema();
          nodeSchema.Scalar = new ScalarSchema
          {
              Base = schema.Base,
              Unit = schema.Unit,
              LowLimit = schema.LowLimit,
              UpLimit = schema.UpLimit,
              Error = schema.Error,
              Regex = schema.Regex,
              WhiteList = schema.WhiteList,
              AsSuggest = schema.AsSuggest,
              PreValid = schema.PreValid,
              PostValid = schema.PostValid,
              Additional = schema.Additional,
          };
          return nodeSchema;
     }
     
     #endregion
}