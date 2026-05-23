using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The field storage topology, which defines how the field data is stored in the database.
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP_FIELD}.topology")]
public enum FieldStorageTopology
{
    /// <summary>
    /// All data saved in the same table, which is the default topology.
    /// </summary>
    CoLocated = 0,
    
    /// <summary>
    /// The dynamic type field data will be saved as key-value pairs in a separate table,
    /// which is more flexible and can support more complex data structures.
    /// </summary>
    AttributeBased  = 1,
}