using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Service;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Function;
using SchemaNode.Property.String;
using SchemaNode.Property.Struct;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using Object = SchemaNode.Scalar.Object;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
using SchemaNode.Relation;

namespace SchemaNode.Schema;

/// <summary>
/// The function schema kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_FUNCTION, SCHEMA_KIND_ORDER_FUNC)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_FUNCTION, SCHEMA_KIND_ORDER_FUNC)]
[Meta<NodeType>(typeof(FunctionType))]
[Meta<SchemaGenerator>(typeof(FunctionGenerator))]
[Meta<Append>(typeof(Generics))]
public sealed class FunctionKind;

/// <summary>
/// The function schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.schema")]
[Meta<Attach>(SCHEMA_KIND_FUNCTION)]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(Function.Reflect.Function.getaccessentries)}", $"@{nameof(Args)}", $"@{nameof(Exps)}", NODE_SELF, ENTRY_ROOT)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(Function.Reflect.Function.getaccessvaluetype)}", $"@{nameof(Args)}", $"@{nameof(Exps)}", NODE_SELF)]
public sealed class FunctionSchema: PropertyOwner
{
    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FuncArg[] Args { get; set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FuncExp[] Exps { get; set; } = [];
}

/// <summary>
/// Declare function property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_FUNCTION)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_FUNC}.{SCHEMA_KIND_FUNCTION}")]
[Relation<Visible, Call>(SCHEMA_KIND_FUNCTION, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_FUNCTION)]
public sealed class FuncProperty : Property<FunctionSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not FuncProperty { Value: {} otherSchema })  return false;
        if (Value is not { } schema)
        {
            SetValue(otherSchema);
            return true;
        }

        // Combine argument display
        for (int i = 0; i < schema.Args.Length; i++)
        {
            var arg = schema.Args[i];
            var otherArg = otherSchema.Args.ElementAtOrDefault(i);
            if (otherArg is null || otherArg.Type != arg.Type) continue;
            arg.CombineProperties(otherArg, runtime, SCHEMA_KIND_FUNC_ARG);
        }
        
        schema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_FUNCTION);
        SetValue(schema);
        return true;
    }
}

/// <summary>
/// Represents the function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_FUNCTION)]
public class FuncType: AnyType;

/// <summary>
/// Represents the validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.valid")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, NS_SYSTEM_BOOL)]
public class ValidFuncType: FuncType;

/// <summary>
/// Represents the function return value type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.valuetype")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"{NS_SYSTEM_SCHEMA_NODE}.valuetype")]
public class TypeFuncType : FuncType;

/**
 * The function argument information
 */
[Meta<SchemaKind>(SCHEMA_KIND_FUNC_ARG, SCHEMA_KIND_ORDER_FUNC_ARG)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.arg")]
[Meta<Attach>(SCHEMA_KIND_FUNC_ARG)]
[Meta<Append>(typeof(Relations), typeof(Display), typeof(Default), typeof(Require))]
[Meta<TypeProvider>(nameof(Type))]
public sealed class FuncArg : PropertyOwner
{
    /// <summary>
    /// The argument name
    /// </summary>
    [Meta<PrimaryIndex>]
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    [Meta<PrimaryIndex>]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The argument type, T T1 T2 means the generic type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// The function expressions
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.exp")]
public sealed class FuncExp {
    /// <summary>
    /// The expression name
    /// </summary>
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    [Meta<PrimaryIndex>]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The expression type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The call function settings
    /// </summary>
    [Relation<Default, Call>($"{nameof(Call)}.{nameof(FuncCall.Return)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Return)}")]
    public required FuncCall Call { get; set; }
}

/// <summary>
/// The function call 
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.{nameof(FuncCall)}")]
[Relation<Valid, Assign>(nameof(Func), NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, $"@{nameof(Func)}", $"@{nameof(FuncReturn)}")]
public class FuncCall
{
    /// <summary>
    /// The expression type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The apply mode
    /// </summary>
    [Relation<WhiteList, Call>($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(Function.Reflect.Function.getapplymodes)}", $"@{nameof(Return)}")]
    [Meta<Require>(true)]
    public ApplyMode Mode { get; set; } = ApplyMode.Call;

    /// <summary>
    /// The expected function return type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    [Relation<Default, Call>($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(Function.Reflect.Function.getexpectreturn)}", $"@{nameof(Return)}", $"@{nameof(Mode)}")]
    public string? FuncReturn { get; set;}
     
    /// <summary>
    /// The call function
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Relation<Visible, Call>(nameof(Args), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Mode)}")]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The argument list
    /// </summary>
    [Relation<Visible, Call>(nameof(Args), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Func)}")]
    public CallArg[] Args { get; set; } = [];
}

/// <summary>
/// The function call arguments
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.{nameof(CallArg)}")]
[Relation<AccessEntryConsumer, Assign>(nameof(Source), $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isassignableto)}", $"@{nameof(Type)}")]
public class CallArg: IEquatable<CallArg>
{
    /// <summary>
    /// The argument type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<ReadOnly>(true)]
    public string? Type { get; set; }
    
    /// <summary>
    /// The argument data source, like field access path
    /// </summary>
    //[Meta<AccessEntryConsumer>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isassignableto)}", NODE_SELF, $"@{nameof(Type)}")]
    [Relation<InVisible, Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Value)}")]
    public string? Source { get; set; }
    
    /// <summary>j
    /// The const value, no complex struct value
    /// </summary>
    [Meta<SchemaType>(typeof(Object))]
    [Relation<OverrideType, Call>(NODE_SELF, $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $@"{nameof(Type)}")]
    [Relation<InVisible, Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Source)}")]
    [Relation<Visible, Call>(NODE_SELF, NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, "@type", true, SCHEMA_KIND_INT, SCHEMA_KIND_STRING, SCHEMA_KIND_DATE, SCHEMA_KIND_BOOL, SCHEMA_KIND_ENUM)]
    public JsonNode? Value { get; set; }
    
    /// <summary>
    /// The node type of the call argument
    /// </summary>
    [SchemaIgnore] 
    [JsonIgnore] 
    public Runtime.ValueType? ValueType { get; set; }

    public bool Equals(CallArg? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.IsNullOrWhiteSpace(Source)
            ? string.IsNullOrWhiteSpace(other.Source) && !Value.IsEmpty() && !other.Value.IsEmpty() && Value!.ToJsonString().Equals(other.Value!.ToJsonString())
            : !string.IsNullOrWhiteSpace(other.Source) && Source.Equals(other.Source, StringComparison.OrdinalIgnoreCase);
    }
}