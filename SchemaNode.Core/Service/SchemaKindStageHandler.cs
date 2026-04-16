using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Service;

/// <summary>
/// The handler to load schema kinds
/// </summary>
public class SchemaKindStageHandler: IStageHandler
{
    /// <inheritdoc/>
    public void OnSchemaKindLoading(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        ISchemaRunTime runTime = context.RunTime;
        
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ISchema))))
            {
                string kind = type.GetMetaProperty<NodeSchemaKind>()?.Value ?? type.Name.GetSchemaKind();
                
            }
        }
    }
}