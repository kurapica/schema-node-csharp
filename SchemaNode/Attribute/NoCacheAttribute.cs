namespace SchemaNode.Attribute;

/// <summary>
/// Mark the function result should not be cached
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class NoCacheAttribute: System.Attribute
{
}