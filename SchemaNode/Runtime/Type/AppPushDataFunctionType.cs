using SchemaNode.Function;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

internal class AppPushDataFunctionType(AppFieldType appFieldType): FunctionType
{
    private AppFieldType _appFieldType = appFieldType;
    
    /// <summary>
    /// Analyze the data push func to gather all app fields involved for tracking
    /// </summary>
    bool AnalyzeDataPush()
    {
        if (_appFieldType.FuncNode == null || _appFieldType.FuncNode.Exps.Length == 0) return false;

        List<FunctionNodeArgument> newArgs = [_appFieldType.FuncNode.Args[0]];
        List<FunctionNodeExpression> newFuncExps = [];
        
        // Check the other fields used in the function
        foreach (FunctionNodeExpression exp in _appFieldType.FuncNode.Exps)
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
            }
        }
    }
    
    internal new void ClearFunctionInfo()
    {
        AnalyzeDataPush();
    }
}