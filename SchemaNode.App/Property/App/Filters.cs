using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Property.App;

/// <summary>
/// The app field filters
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(Filters)}")]
public class Filters : Property<FieldFilter[]>, ILoadableProperty, INodeError
{
    public string? Error { get; set; }

    public async Task LoadAsync(SchemaContext context, Runtime.ValueType? ownerType = null)
    {
        if (Value == null) return;
        StructType? structType = ((ownerType as ArrayType)?.Element ?? ownerType) as StructType;
        if (ownerType != null && structType == null)
        {
            Error = AppErrorCodes.APP_FIELD_FILTER_NOT_VALID;
            return;
        }
        
        foreach (FieldFilter filter in Value)
        {
            if (filter.Mode == FieldFilterMode.Filter)
            {
                filter.FilterFunction = await context.GetNodeTypeAsync<FunctionType>(filter.Filter);
                if (filter.FilterFunction == null ||
                    filter.FilterFunction.Args.Length < 2 ||
                    filter.FilterFunction.Args[0].ValueType == null ||
                    structType != null && !filter.FilterFunction.Args[0].ValueType!.IsAssignableTo(structType))
                {
                    Error = AppErrorCodes.APP_FIELD_FILTER_NOT_VALID;
                    break;
                }
            }
            else
            {
                if (structType?.GetField(filter.Filter) == null)
                {
                    Error = AppErrorCodes.APP_FIELD_FILTER_NOT_VALID;
                    break;
                }
            }
        }
    }
}

/// <summary>
/// The field filter
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.filter")]
public sealed class FieldFilter
{
    /// <summary>
    /// The filter mode
    /// </summary>
    public FieldFilterMode Mode { get; set; } = FieldFilterMode.Exactly;

    /// <summary>
    /// The field name or filter function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Filter { get; set; } = string.Empty;
    
    /// <summary>h
    /// The field filter resolve type, which defines how to resolve the filter when no contains found
    /// </summary>
    public FieldFilterResolve? Resolve { get; set; }
    
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? FilterFunction { get; set; }
}