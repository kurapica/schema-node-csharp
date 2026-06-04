using SchemaNode.Attribute;
using SchemaNode.Property.Record;

namespace SchemaNode.Utility;

internal static class AppErrorCodes
{
    [Meta<ErrorCode>(APP_DUMPLICATE_FIELD)]
    public const string APP_DUMPLICATE_FIELD = "app_duplicate_field";
}
