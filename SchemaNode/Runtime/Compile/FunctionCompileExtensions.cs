using SchemaNode.Context;

namespace SchemaNode.Runtime;

/// <summary>
/// The function extensions for compile services
/// </summary>
public static class FunctionCompileExtensions
{
    /// <summary>
    /// Compile the function with the default compile context
    /// </summary>
    public static async Task<Delegate?> CompileAsync(this FunctionType funcType, SchemaContext context)
    {
        if (funcType.TryGetRuntimeFuncCache<CompileContext, Delegate>(out Delegate? del)) return del;

        CompileContext compileCtx = new CompileContext(context, funcType);
        del = await compileCtx.CompileAsync();
        funcType.SetRuntimeFuncCache<CompileContext, Delegate>(del!);
        return del;
    }

    /// <summary>
    /// Compile the function with other compile context
    /// </summary>
    public static async Task<Delegate?> CompileAsync<T>(this FunctionType funcType, SchemaContext context) where T: CompileContext
    {
        if (funcType.TryGetRuntimeFuncCache<T, Delegate>(out Delegate? del)) return del;

        CompileContext compileCtx = (Activator.CreateInstance(typeof(T), context, funcType) as CompileContext)!;
        del = await compileCtx.CompileAsync();
        funcType.SetRuntimeFuncCache<T, Delegate>(del);
        return del;
    }
}
