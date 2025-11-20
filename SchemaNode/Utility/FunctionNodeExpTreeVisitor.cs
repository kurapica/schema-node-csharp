using SchemaNode.Context;
using SchemaNode.Runtime;

namespace SchemaNode.Utility;

/// <summary>
/// The function node expression tree visitor
/// only support (arg: struct) => bool
/// Generate the struct access where clause
/// </summary>
public class FunctionNodeExpTreeVisitor
{
    /// <summary>
    /// Visite the function type and generate the expression tree
    /// </summary>
    public async Task Visit(SchemaContext context, FunctionType func)
    {
        if (func.Args.Length != 1)
            throw new NotSupportedException("Only support one parameter function");

        StructType structNode = func.Args[0].TypeNode as StructType
            ?? throw new NotSupportedException("Only support struct type parameter");

        
    }
}