
namespace SchemaNode.Enum;

/// <summary>
/// The policy scope
/// </summary>
public enum PolicyScope
{
    SchemaCreate = 1,
    SchemaRead,
    SchemaUpdate,
    SchemaDelete,
    DataCreate,
    DataRead,
    DataUpdate,
    DataDelete,
    FuncExecute,
}
