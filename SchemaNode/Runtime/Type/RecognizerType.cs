using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using System.Text.Json;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory recognizer schema representation
/// </summary>
public sealed class RecognizerType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The state schema type after recognition
    /// </summary>
    public string? Result { get; set; } = string.Empty;

    /// <summary>
    /// The parts of recognizer
    /// </summary>
    public RecognizerPart[] Parts { get; set; } = [];

    /// <summary>
    /// The additional data
    /// </summary>
    public Dictionary<string, JsonElement>? Additional { get; set; }

    #endregion

    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Recognizer;

    #endregion

    #region Ref

    /// <summary>
    /// The result type
    /// </summary>
    public AnySchemaType? ResultType { get; private set; }

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        RecognizerSchema? recognizer = schema.Recognizer;

        // Data
        Result = recognizer?.Result;
        Parts = recognizer?.Parts ?? [];
        Additional = recognizer?.Additional;

        if (recognizer == null)
        {
            Status = SchemaNodeStatus.NoDefinition;
            return;
        }

        ResultType = !string.IsNullOrWhiteSpace(Result) ? await context.GetSchemaTypeAsync(Result) : null;
        if (ResultType == null || !ResultType.IsValueType)
        {
            Status = SchemaNodeStatus.RecognizerWrongResult;
            return;
        }

        // Parts
        foreach (var part in Parts)
        {

        }


        // Add ref
        ResultType.AddRef(this);
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
    {
        return null;
    }

    /// <inheritdoc />
    public override void Release()
    {
        ResultType?.RemoveRef(this);
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(RecognizerType? schema)
    {
        return schema?.ToSchema().With(new RecognizerSchema
        {
            Result = schema.Result ?? string.Empty,
            Parts = schema.Parts ?? [],
            Additional = schema.Additional
        });
    }
    #endregion
}