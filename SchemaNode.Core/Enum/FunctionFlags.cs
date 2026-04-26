namespace SchemaNode.Enum;

[Flags]
public enum FunctionFlags
{
    None = 0,
    Context = 1,
    Async = 2,
    Generic = 4,
    Immutable = 8,
    Remote = 16,
    NullableRet = 32,
}