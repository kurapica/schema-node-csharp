namespace SchemaNode.Data;

/// <summary>
/// The dynamic table field type
/// </summary>
public enum DynamicTableFieldType
{
    /// <summary>
    /// The bool field type
    /// </summary>
    Bool,

    /// <summary>
    /// The small int field type, (-32768 ~ 32767)
    /// </summary>
    Smallint,

    /// <summary>
    /// The unsigned small int field type, (0 ~ 65535)
    /// </summary>
    USmallint,

    /// <summary>
    /// The medium int field type, 	(-8388608 ~ 8388607)
    /// </summary>
    Mediumint,

    /// <summary>
    /// The unsigned medium int field type, (0 ~ 16777215)
    /// </summary>
    UMediumint,

    /// <summary>
    /// The int field type, (-2147483648 ~ 2147483647)
    /// </summary>
    Int,

    /// <summary>
    /// The unsigned int field type, (0 ~ 4294967295)
    /// </summary>
    UInt,

    /// <summary>
    /// The big int field type, (-9,223,372,036,854,775,808 ~ 9223372036854775807)
    /// </summary>
    BigInt,

    /// <summary>
    /// The unsigned big int field type, (0 ~ 18446744073709551615)
    /// </summary>
    UBigInt,

    /// <summary>
    /// The float field type
    /// </summary>
    Float,

    /// <summary>
    /// The float field type
    /// </summary>
    Double,

    /// <summary>
    /// The date time type
    /// </summary>
    DateTime,

    /// <summary>
    /// The tiny binary string (0, 255)
    /// </summary>
    TinyBlob,

    /// <summary>
    /// The binary string (0, 65535)
    /// </summary>
    Blob,

    /// <summary>
    /// The medium blob string (0, 16777215)
    /// </summary>
    MediumBlob,

    /// <summary>
    /// The long blob string (0, 4294967295)
    /// </summary>
    LongBlob,

    /// <summary>
    /// The fix-length string (0, 255)
    /// </summary>
    Char,

    /// <summary>
    /// The variable-length string (0, 65535)
    /// </summary>
    VarChar,

    /// <summary>
    /// The short text (0, 255)
    /// </summary>
    TinyText,

    /// <summary>
    /// The text string (0, 65535)
    /// </summary>
    Text,

    /// <summary>
    /// The medium text (0, 16777215)
    /// </summary>
    MediumText,

    /// <summary>
    /// The long text (0, 4294967295)
    /// </summary>
    LongText,

    /// <summary>
    /// The json field type
    /// </summary>
    Json,
}
