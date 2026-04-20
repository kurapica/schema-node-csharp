using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory function schema representation
/// </summary>
[Meta<ErrorCode>("func_wrong_return", SCHEMA_KIND_ORDER_FUNC * 100 + 1)]
[Meta<ErrorCode>("func_wrong_arg", SCHEMA_KIND_ORDER_FUNC * 100 + 2)]
public sealed class FunctionType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The return type name
    /// </summary>
    public string? Return { get; private set; }

    /// <summary>
    /// The function arguments schema
    /// </summary>
    public FuncArg[] Args { get; private set; } = [];

    /// <summary>
    /// The function expression tree
    /// </summary>
    public FuncExp[] Exps { get; private set; } = [];

    /// <summary>
    /// The generic type bases
    /// </summary>
    public string[]? Generic { get; private set; }

    /// <summary>
    /// The return type
    /// </summary>
    public AnySchemaType? ReturnNode  { get; private set; }
    
    /// <summary>
    /// The function is a converter
    /// </summary>
    public bool? Converter { get; private set; }

    #endregion

    #region Ref

    /// <summary>
    /// The resolved return schema type
    /// </summary>
    public AnySchemaType? ReturnSchemaType { get; private set; }

    /// <summary>
    /// The resolved argument schema types (parallel to Args)
    /// </summary>
    public AnySchemaType?[]? ArgSchemaTypes { get; private set; }

    #endregion

    #region Loading

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        FunctionSchema? func = schema.GetProperty<FuncProperty>()?.Value;

        // Data
        Return = func?.Return;
        Args = func?.Args ?? [];
        Exps = func?.Exps ?? [];
        Generic = func?.Generic;

        // Status
        if (func == null) Error = "no_definition";

        ISchemaRuntime runtime = context.Runtime;

        // Resolve return type
        if (!string.IsNullOrWhiteSpace(Return) && !IsGenericRef(Return))
        {
            AnySchemaType? retType = await runtime.GetSchemaTypeAsync(context, Return, preload: preload);
            if (retType != null)
            {
                ReturnSchemaType = retType;
                retType.AddRef(this);
            }
            else
            {
                ReturnSchemaType = null;
                Error = "func_wrong_return";
            }
        }

        // Resolve argument types
        if (Args.Length > 0)
        {
            ArgSchemaTypes = new AnySchemaType?[Args.Length];
            for (int i = 0; i < Args.Length; i++)
            {
                FuncArg arg = Args[i];
                if (string.IsNullOrWhiteSpace(arg.Type) || IsGenericRef(arg.Type)) continue;

                AnySchemaType? argType = await runtime.GetSchemaTypeAsync(context, arg.Type, preload: preload);
                if (argType != null)
                {
                    ArgSchemaTypes[i] = argType;
                    argType.AddRef(this);
                }
                else
                {
                    arg.Error = $"Argument type '{arg.Type}' not found";
                    Error = "func_wrong_arg";
                }
            }
        }
    }

    /// <inheritdoc />
    public override void ReleaseType()
    {
        ReturnSchemaType?.RemoveRef(this);
        ReturnSchemaType = null;

        if (ArgSchemaTypes != null)
        {
            foreach (AnySchemaType? argType in ArgSchemaTypes)
                argType?.RemoveRef(this);
            ArgSchemaTypes = null;
        }

        Args = [];
        Exps = [];
        base.ReleaseType();
    }

    #endregion

    #region Utility

    /// <summary>
    /// Check if a type name is a generic placeholder (T, T1, T2...)
    /// </summary>
    static bool IsGenericRef(string typeName)
    {
        if (typeName.Length == 1 && typeName[0] == 'T') return true;
        return typeName.Length >= 2 && typeName[0] == 'T' && char.IsDigit(typeName[1]);
    }

    #endregion
}
