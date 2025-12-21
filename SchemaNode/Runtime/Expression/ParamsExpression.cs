namespace SchemaNode.Runtime;

/// <summary>
/// The params expression type
/// </summary>
/// <param name="Exps"></param>
/// <param name="SchemaType"></param>
public record ParamsExpression(SchemaExpression[] Exps, AnySchemeType SchemaType): SchemaExpression(SchemaType);