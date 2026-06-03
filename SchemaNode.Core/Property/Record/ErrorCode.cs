namespace SchemaNode.Property.Record;

/// <summary>
/// Declares an error code value for the ErrorCode enum.
/// Use [Meta&lt;AsErrorCode&gt;("code_name", order)] on runtime type classes to register error codes.
/// Order follows schema kind order * 100 + x pattern.
/// </summary>
public class ErrorCode : RecordProperty<string>;
