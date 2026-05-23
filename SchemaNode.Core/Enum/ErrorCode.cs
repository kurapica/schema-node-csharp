using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// Represents the error codes for schema loading diagnostics.
/// This is an empty marker enum — actual values are dynamically collected
/// from [Meta&lt;AsErrorCode&gt;] declarations on runtime types via RecordProperty.
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_ERROR)]
[Meta<Record>(typeof(Property.Record.ErrorCode))]
public enum ErrorCode;
