using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when update the target field data in the application
/// </summary>
[SchemaType($"{NS_SYSTEM_EVENT}.appdata.update")]
public class AppFieldDataUpdateEvent(AppFieldType field, string target) : ApplicationFieldDataEvent(field.App, target, field.Name)
{
}