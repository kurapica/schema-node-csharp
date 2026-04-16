using System.Collections.Concurrent;
using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;

namespace SchemaNode.Service;

/// <summary>
/// The stage to load schema properties
/// </summary>
public class SchemaPropertyStageHandler: IStageHandler
{
    private static readonly ConcurrentBag<Type> SchemaPropertyTypes = [];
    private static readonly ConcurrentBag<Type> EnumPropertyTypes = [];
    
    /// <inhericdoc />
    public void OnPropertyLoading(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        ISchemaRunTime runTime = context.RunTime;
        
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(IProperty))))
            {
                if (type.GetMetaProperty<ForSchema>() != null)
                    SchemaPropertyTypes.Add(type);
                else if (type.GetMetaProperty<SchemaType>() != null)
                    EnumPropertyTypes.Add(type);
            }
        }
    }

    /// <inhericdoc />
    public void OnSchemaKindLoaded(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        throw new NotImplementedException();
    }

    /// <inhericdoc />
    public void OnPreSystemSchemaLoad(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        
    }
}