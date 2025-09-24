using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory scalar schema representation
/// </summary>
public class ScalarNode: NamespaceNode
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
     public ScalarNode? BaseNode { get; private set; }
     
     /// <summary>
     /// The post validation function node
     /// </summary>
     public FunctionNode? PostValidNode { get; private set; }

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

        // Status
        if (scalar == null) Status = SchemaNodeStatus.NoDefinition;

        // Relationship
        if (!string.IsNullOrWhiteSpace(Base))
        {
            NamespaceNode? node = await context.GetSchemaNodeAsync(Base, preload: preload);
            if (node != null && node is ScalarNode snode)
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
            NamespaceNode? node = await context.GetSchemaNodeAsync(PostValid, preload: preload);
            if (node != null && node is FunctionNode fnode)
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
            NS_SYSTEM_FULLDATE => ScalarValueType.FullDate | ScalarValueType.Date,
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
     public override async Task<(JsonNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
     {
          await Task.Yield();
          if (value is not JsonValue val || val.IsEmpty())
               return (value, TYPE_VALUE_NOT_VALID);
          
          // validate the scalar value
          string strval = val.ToString();
          
          // check with type
          try
          {
               if (IsYear)
               {
                    if (long.TryParse(strval, out long year))
                    {
                         // pass
                    }
                    else if (DateTime.TryParse(strval, out DateTime dateTime))
                    {
                         year = dateTime.GetYear();
                    }
                    else
                    {
                         return (value, TYPE_VALUE_NOT_VALID);
                    }

                    if (LowLimit > year || UpLimit < year)
                         return (value, TYPE_VALUE_NOT_VALID);
                    return (year, null);
               }
               
               else if (IsInt)
               {
                    if (!long.TryParse(strval, out long lval) || (LowLimit > lval || UpLimit < lval))
                         return (value, TYPE_VALUE_NOT_VALID);
                    return (lval, null);
               }
               
               else if (IsSingle)
               {
                    if (!float.TryParse(strval, out float fval) || (LowLimit > (decimal?)fval || UpLimit < (decimal?)fval))
                         return (value, TYPE_VALUE_NOT_VALID);
                    return (fval, null);
               }
               
               else if (IsDouble)
               {
                    if (!double.TryParse(strval, out double dval) || (LowLimit > (decimal?)dval || UpLimit < (decimal?)dval))
                         return (value, TYPE_VALUE_NOT_VALID);
                    return (dval, null);
               }
               
               else if (IsNumber)
               {
                    if (!decimal.TryParse(strval, out decimal mval) || (LowLimit > mval || UpLimit < mval))
                         return (value, TYPE_VALUE_NOT_VALID);
                    return (mval, null);
               }
               
               else if (IsBool)
               {
                    return Utility.Schema.TryParseBoolValue(strval, out bool bval) ? (bval, null) : (value, TYPE_VALUE_NOT_VALID);
               }
               
               else if (IsString)
               {
                    if (LowLimit > strval.Length || UpLimit < strval.Length)
                         return (value, TYPE_VALUE_NOT_VALID);
                    return (strval, null);
               }
               
               else if (IsDate)
               {
                    if (DateTime.TryParse(strval, out DateTime date))
                    {
                         if (IsYearMonth)
                         {
                              return (date.GetFirstTimeOfMonth(), null);
                         }
                         else if (IsFullDate)
                         {
                              return (date, null);
                         }
                         else
                         {
                              return (date.GetFirstTimeOfDay(), null);
                         }
                    }
                    else
                    {
                         return (value, TYPE_VALUE_NOT_VALID);
                    }
               }
          }
          catch
          {
               // pass
          }
          return (value, TYPE_VALUE_NOT_VALID);
     }

     /// <inheritdoc />
     public override bool CanBeUseAs(NamespaceNode other) =>
          base.CanBeUseAs(other) ||
          other switch
          {
               ScalarNode scalar => scalar.IsString || 
                                    scalar.IsInt 
                                        ? IsInt
                                        : (scalar.IsNumber 
                                             ? IsNumber 
                                             : scalar.ValueType == ValueType),
               EnumNode @enum => @enum.ValueType switch
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

     #endregion
     
     #region Conversion
     
     /// <summary>
     /// Convert the node to schema
     /// </summary>
     public static implicit operator NodeSchema?(ScalarNode? schema)
     {
          if (schema == null) return null;
          return new NodeSchema
          {
               Name = schema.Name,
               Type = schema.Type,
               Display = schema.Display,
               LoadState = schema.LoadState,
               Scalar = new ScalarSchema
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
               }
          };
     }
     
     #endregion
}