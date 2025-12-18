using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Schema;
using System;
using System.Linq;
using System.Linq.Expressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

internal class AppPushDataFunctionType(AppFieldType appFieldType): FunctionType
{
    private AppFieldType _appFieldType = appFieldType;
    private bool _useOrigin = appFieldType.SchemaType is not ArrayType arr || arr.Primary == null || arr.Primary.Length == 0 || arr.ElementSchemaType is not StructType;

    public override void Release()
    {
        _appFieldType.FuncNode?.RemoveRef(this);
    }

    public new async Task PreCompileAsync(SchemaContext context)
    {
        if (_appFieldType.FuncNode == null)
        {
            Status = SchemaNodeStatus.ApplicationFieldWrongFunc;
            return;
        }
        await _appFieldType.FuncNode.PreCompileAsync(context);
        if (_appFieldType.FuncNode.Status != SchemaNodeStatus.Ready)
        {
            Status = _appFieldType.FuncNode.Status;
            return;
        }
        _appFieldType.FuncNode?.AddRef(this);
        Status = SchemaNodeStatus.Ready;

        // Analyze the data push function to gather all app fields involved
        FunctionType origin = _appFieldType.FuncNode!;

        // Data
        Return = origin.Return;
        ReturnNode = origin.ReturnNode;
        Args = origin.Args.ToArray(); // we may add more args but won't change the argument itself
        Exps = [];

        // Start from the struct build exp
        StructResultExpNode? structBuildExpNode = origin.ExpTrees.LastOrDefault(e =>  e is StructResultExpNode) as StructResultExpNode;
        if (structBuildExpNode == null)
        {
            Status = SchemaNodeStatus.ApplicationFieldWrongFunc;
            return; // won't hit
        }

        // Check primary key fields only for now
        string[] primarys = (_appFieldType.SchemaType as ArrayType)!.Primary!;
        convArgs.Clear();
        convExps.Clear();
        treeMap.Clear();
        convArgs.Add(Args[0]);
        foreach (var node in structBuildExpNode.LeafNodes)
        {
            if (node is not FunctionNodeExpression exp)
            {
                Status = SchemaNodeStatus.ApplicationFieldWrongFunc;
                return; // why use argument as struct field
            }

            bool isPrimary = primarys.Contains(exp.Name, StringComparer.OrdinalIgnoreCase);
            try
            {
                RegenerateExp(exp, isPrimary);
            }
            catch(Exception ex)
            {
                context.LogError(ex, $"Failed to regenerate expression {exp.Name} for push data to {_appFieldType.Name}");
            }
        }


        // Check all expressions
        foreach (FunctionNodeExpression exp in Exps)
        {
            switch (exp.FuncNode?.Name)
            {
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getdatasource)}":
                {
                    
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabytwokey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdata)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyonekey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabytwokey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabythreekey)}":
                {
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyfourkey)}":
                {
                    break;
                }
                default:
                {
                    convExps.Add(exp);
                    break;
                }
            }
        }

        await base.PreCompileAsync(context);
    }

    void RegenerateExp(FunctionNodeExpTree tree, bool isPrimary)
    {
        switch(tree)
        {
            // App data source access, like Linq
            case AppDataSourceAccessExpNode appData:
            {
                break;
            }
            // Const exp
            case ConstantExpNode constExp:
            {
                break;
            }
            // Argument exp
            case FunctionNodeArgument argExp:
            {
                break;
            }
            // Params exp
            case ParamsExpNode paramsExp:
            {
                break;
            }
            // Common function call
            case FunctionNodeExpression exp:
            {
                break;
            }
            // Generate the field access
            case FieldAccessExpNode access:
            {
                
                break;
            }
            default:
            {
                throw new NotSupportedException($"Unsupported expression node type: {tree.GetType().FullName}");
            }
        }
    }

    

    internal new void ClearFunctionInfo()
    {
        FuncInfo = null;
        ExpTrees.Clear(); // reset
    }

    internal new SchemaFuncInfo? GetSchemaFuncInfo(SchemaContext context)
    {
        if (_useOrigin) return _appFieldType.FuncNode?.GetSchemaFuncInfo(context);
        return base.GetSchemaFuncInfo(context);
    }

    List<FunctionNodeArgument> convArgs = [];
    List<FunctionNodeExpression> convExps = [];
    Dictionary<string, FunctionNodeExpression> treeMap = [];
}