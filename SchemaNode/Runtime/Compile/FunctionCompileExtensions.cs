using SchemaNode.Context;

namespace SchemaNode.Runtime;

/// <summary>
/// The function extensions for compile services
/// </summary>
public static class FunctionCompileExtensions
{
    /// <summary>
    /// Visit the function type to generate the function schema
    /// </summary>
    public static async Task<FunctionTypeSchema> VisitFunctionTypeAsync<T>(this SchemaContext context, FunctionType funcType) where T : CompileContext
    {
        if (funcType.TryGetRuntimeFuncCache<T, FunctionTypeSchema>(out FunctionTypeSchema? funcSchema))
            return funcSchema!;
        
        CompileContext compileCtx = (Activator.CreateInstance(typeof(T), context, funcType) as CompileContext)!;
        funcSchema = await compileCtx.VisitFunctionType();
        return funcType.SetRuntimeFuncCache<T, FunctionTypeSchema>(funcSchema)!;
    }
    
    /// <summary>
    /// Visit the function type to generate the function schema with default compile context
    /// </summary>
    public static Task<FunctionTypeSchema> VisitFunctionTypeAsync(this SchemaContext context, FunctionType funcType)
        => context.VisitFunctionTypeAsync<CompileContext>(funcType);
    
    /// <summary>
    /// Compile the function with compile context
    /// </summary>
    public static async Task<Delegate?> CompileFunctionTypeAsync<T>(this SchemaContext context, FunctionType funcType) where T: CompileContext
    {
        if (funcType.TryGetRuntimeFuncCache<T, Delegate>(out Delegate? del)) return del;

        CompileContext compileCtx = (Activator.CreateInstance(typeof(T), context, funcType) as CompileContext)!;
        del = await compileCtx.CompileAsync();
        return funcType.SetRuntimeFuncCache<T, Delegate>(del);
    }
    
    /// <summary>
    /// Compile the function with the default compile context
    /// </summary>
    public static Task<Delegate?> CompileFunctionTypeAsync(this SchemaContext context, FunctionType funcType)
        => context.CompileFunctionTypeAsync<CompileContext>(funcType);
}
