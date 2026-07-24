using SchemaNode.Attribute;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Data.Entity;

[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<EnableStorage>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.entity.appworkflow")]
internal class AppWorkflowEntity
{

    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<UniqueIndex>("seqno",0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The seqno
    /// </summary>
    [Meta<UniqueIndex>("seqno", 1)]
    public int Seqno { get; set; }


    #region Conversion

    public static implicit operator AppWorkflowEntity?(AppWorkflowSchema? appWorkflowSchema)
    {
        if (appWorkflowSchema == null) return null;
        
        return new AppWorkflowEntity
        {
            App = appWorkflowSchema.App,
            Name = appWorkflowSchema.Name,
            Seqno = appWorkflowSchema.Seqno
        };
    }
    
    public static implicit operator AppWorkflowSchema?(AppWorkflowEntity? appWorkflowEntity)
    {
        if (appWorkflowEntity == null) return null;
        
        return new AppWorkflowSchema
        {
            App = appWorkflowEntity.App,
            Name = appWorkflowEntity.Name,
            Seqno = appWorkflowEntity.Seqno
        };
    }

    #endregion
}
