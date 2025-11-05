using SchemaNode.Attribute;
using SchemaNode.Components;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when query the target data in the application
/// </summary>
[SchemaType($"{NS_SYSTEM_EVENT}.app.data.visit")]
public class AppDataVisitEvent: ApplicationEvent
{
}