using System;

namespace SchemaNode.App.Http;

/// <summary>
/// Marks a SchemaApi to use the default protocol (no wrapping), bypassing any registered <see cref="ISchemaApiProtocol"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NoProtocolAttribute : System.Attribute
{
}
