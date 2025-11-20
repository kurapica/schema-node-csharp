using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Utility;

/// <summary>
/// The function node expression tree visitor
/// only support (arg: struct) => bool
/// Generate the struct access where clause
/// </summary>
public class FunctionNodeExpTreeVisitor
{
    /// <summary>
    /// Visit the function type
    /// </summary>
    public async Task Visit(SchemaContext context, FunctionType func)
    {
        if (func.Args.Length != 1)
            throw new NotSupportedException("Only support one parameter function");

        StructType structNode = func.Args[0].TypeNode as StructType
            ?? throw new NotSupportedException("Only support struct type parameter");

        if (func.ReturnNode is not ScalarType retType || !retType.IsBool)
            throw new NotSupportedException("The function return type must be bool");

        // scan the exp trees
        foreach(FunctionNodeExpTree tree in func.ExpTrees)
        {
            if (tree is not FunctionNodeExpression exp) continue;
            

        }
    }

    /// <summary>
    /// Visit the exp tree
    /// </summary>
    public void Visit(FunctionNodeExpression expTree)
    {
        switch (expTree.Func)
        {
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.andalso)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.cond)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.equal)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greateequal)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greatethan)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isnull)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notnull)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isempty)}":
                break
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessequal)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessthan)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notequal)}":
                break;
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.orelse)}":
                break;
        }
    }
}
