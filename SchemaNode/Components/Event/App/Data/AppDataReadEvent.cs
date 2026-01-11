using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when query the target data in the application
/// </summary>
[Schema($"{NS_SYSTEM_EVENT}.app.data.read")]
public class AppDataReadEvent(string app, string target) : AppDataEvent(app, target)
{
}