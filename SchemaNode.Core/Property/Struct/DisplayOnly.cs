using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Struct;

/// <summary>
/// The struct field is display only, which means it will not be editable in the editor and will not be included in the node data when saving. It can be used for displaying calculated values or values from other sources.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRUCT_FIELD)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRUCT}.{nameof(DisplayOnly)}")]
[Meta<Static>(true)]
public class DisplayOnly: Property<bool>
{
    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        if (Value == true) // static property only effect once
            target.SetPropertyValue<ReadOnly, bool>(true, target, null);
    }
}