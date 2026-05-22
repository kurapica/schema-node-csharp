namespace SchemaNode.App.Utility;

internal static class AppUtilityExtension
{
    /// <summary>Traverses the inner exception chain and returns the deepest exception.</summary>
    internal static Exception GetInnermostException(this Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return exception;
    }
}
