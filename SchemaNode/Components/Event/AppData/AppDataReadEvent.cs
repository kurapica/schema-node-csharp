using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when query the target data in the application
/// </summary>
[SchemaType($"{NS_SYSTEM_EVENT}.appdata.read")]
public class AppDataReadEvent(string app, string target) : ApplicationDataEvent(app, target)
{
}