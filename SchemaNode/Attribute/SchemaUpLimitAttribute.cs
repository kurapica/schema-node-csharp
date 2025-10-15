namespace SchemaNode.Attribute;

/// <summary>
/// The upLimit for struct member
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SchemaUpLimitAttribute: System.Attribute
{
    public int UpLimit { get; }
    
    public SchemaUpLimitAttribute(int upLimit)
    {
        UpLimit = upLimit;
    }
}