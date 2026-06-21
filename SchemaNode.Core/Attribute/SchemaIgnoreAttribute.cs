namespace SchemaNode.Attribute;

/// <summary>
/// Declare the property/field should be ignored when generating schema.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method)]
public sealed class SchemaIgnoreAttribute: System.Attribute;