namespace SchemaNode.Components;

/// <summary>
/// The operation to modify the dynamic table values
/// </summary>
public enum TransactionChangeOperation
{
    Create,
    Modify,
    Delete,
    DropAll
}