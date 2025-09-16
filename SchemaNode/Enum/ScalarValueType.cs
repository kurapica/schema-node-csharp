namespace SchemaNode.Enum;

/// <summary>
/// The scalar value type
/// </summary>
[Flags]
public enum ScalarValueType
{
    None = 0,
    String = 1,
    Number = 2,
    Single = 4,
    Double = 8,
    Integer = 16,
    Boolean = 32,
    Date = 64,
    Year = 128,
    FullDate = 256,
    YearMonth = 512,
    
    Indexable = Integer | Boolean | Date | Year | FullDate | YearMonth
}