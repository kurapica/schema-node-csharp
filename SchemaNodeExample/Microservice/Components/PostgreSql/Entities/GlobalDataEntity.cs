using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchemaNode.Example
{
    /// <summary>
    /// 系统全局设定
    /// </summary>
    [Table("GlobalData")]
    [Description("保存系统全局设定")]
    public class GlobalDataEntity
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        [DefaultValue(PostgreSql.DEFAULT_PK_VALUE)]
        public Guid Gd_Pk { get; set; }

        /// <summary>
        /// 全局设定名
        /// </summary>
        [Required]
        [UniqueKey]
        [MaxLength(256)]
        [Description("全局设定名")]
        public string Gd_Name { get; set; }

        /// <summary>
        /// 全局设定值
        /// </summary>
        [Required]
        [Description("全局设定值")]
        public string Gd_Value { get; set; }

        /// <summary>
        /// 全局设定描述
        /// </summary>
        [Description("全局设定描述")]
        public string Gd_Description { get; set; }
    }
}