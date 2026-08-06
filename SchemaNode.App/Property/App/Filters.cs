using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Property.Common;
using SchemaNode.Relation;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructType = SchemaNode.Runtime.StructType;
using SchemaNode.Property.Constraint;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Property.App;

/// <summary>
/// The app field filters
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(Filters)}")]
public class Filters : Property<FieldFilter[]>, ILoadableProperty, IErrorProvider
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
                filter.FilterFunction = string.IsNullOrWhiteSpace(filter.FilterFunc) ? null : await context.GetNodeTypeAsync<FunctionType>(filter.FilterFunc);
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
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<PrimaryIndex>(0)]
    [Meta<Cascade>(1)]
    [Meta<AccessEntryConsumer>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, false, SCHEMA_KIND_ENUM, SCHEMA_KIND_STRING, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_DATE, SCHEMA_KIND_BOOL)]
    [Relation<InVisible, Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"{nameof(Mode)}", FieldFilterMode.Filter)]
    public string Filter { get; set; } = string.Empty;

    /// <summary>
    /// The filter function name
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Meta<PrimaryIndex>(1)]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, NS_SYSTEM_BOOL)]
    [Relation<Visible, Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"{nameof(Mode)}", FieldFilterMode.Filter)]
    public string? FilterFunc { get; set; }

    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    [Meta<AccessValueTypeResolver>($"{nameof(Filter)}")]
    public string? FilterType { get; set;}
    
    /// <summary>
    /// The field filter resolve type, which defines how to resolve the filter when no contains found
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.hascascade", $"{nameof(FilterType)}", true)]
    public FieldFilterResolve? Resolve { get; set; }
    
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? FilterFunction { get; set; }
}