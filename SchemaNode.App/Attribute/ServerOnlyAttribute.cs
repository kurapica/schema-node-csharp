namespace SchemaNode.Attribute;

/// <summary>
/// Mark the function can only be executed on server side
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ServerOnlyAttribute: System.Attribute
{
}