using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The scalar value type
/// </summary>
[Flags]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_SCALAR}.valuetype")]
public enum ScalarValueType
{
    None = 0,
    String = 1,
    Number = 2,
    Char = 4,
    Single = 8,
    Double = 16,
    Integer = 32,
    Boolean = 64,
    Date = 128,
    Year = 256,
    FullDate = 512,
    YearMonth = 1024,
    Guid = 2048,
    
    Indexable = Char | Integer | Boolean | Date | Year | FullDate | YearMonth | Guid,
}