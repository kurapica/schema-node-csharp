using SchemaNode.Context;

namespace SchemaNode.Runtime;

/// <summary>
/// The function extensions for compile services
/// </summary>
public static class FunctionCompileExtensions
{
    /// <summary>
    /// Visit the function type with the given compile context to generate the function schema
    /// </summary>
    public static async Task<FunctionTypeSchema> VisitFunctionTypeAsync(this CompileContext context, FunctionType funcType)
        => funcType.TryGetRuntimeFuncCache(context.GetType(), out FunctionTypeSchema? funcSchema) ? funcSchema! 
            : funcType.SetRuntimeFuncCache(context.GetType(), await (Activator.CreateInstance(context.GetType(), context.Context, funcType) as CompileContext)!.VisitFunctionType())!;
    
    /// <summary>
    /// Visit the function type to generate the function schema
    /// </summary>
    public static async Task<FunctionTypeSchema> VisitFunctionTypeAsync<T>(this SchemaContext context, FunctionType funcType) where T : CompileContext
        => funcType.TryGetRuntimeFuncCache<T, FunctionTypeSchema>(out FunctionTypeSchema? funcSchema) ? funcSchema! 
            : funcType.SetRuntimeFuncCache<T, FunctionTypeSchema>(await (Activator.CreateInstance(typeof(T), context, funcType) as CompileContext)!.VisitFunctionType())!;
    
    /// <summary>
    /// Visit the function type to generate the function schema with default compile context
    /// </summary>
    public static Task<FunctionTypeSchema> VisitFunctionTypeAsync(this SchemaContext context, FunctionType funcType)
        => context.VisitFunctionTypeAsync<CompileContext>(funcType);

    /// <summary>
    /// Compile the function with the given compile context
    /// </summary>
    public static async Task<Delegate?> CompileFunctionTypeAsync(this SchemaContext context, FunctionType funcType, CompileContext compileContext)
        => funcType.TryGetRuntimeFuncCache(compileContext.GetType(), out Delegate? del) ? del
            : funcType.SetRuntimeFuncCache(compileContext.GetType(), await compileContext.CompileAsync());

    /// <summary>
    /// Compile the function with compile context type
    /// </summary>
    public static async Task<Delegate?> CompileFunctionTypeAsync<T>(this SchemaContext context, FunctionType funcType) where T: CompileContext
        => funcType.TryGetRuntimeFuncCache<T, Delegate>(out Delegate? del) ? del
            : funcType.SetRuntimeFuncCache<T, Delegate>(await  (Activator.CreateInstance(typeof(T), context, funcType) as CompileContext)!.CompileAsync());
    
    /// <summary>
    /// Compile the function with the default compile context
    /// </summary>
    public static Task<Delegate?> CompileFunctionTypeAsync(this SchemaContext context, FunctionType funcType)
        => context.CompileFunctionTypeAsync<CompileContext>(funcType);
}
