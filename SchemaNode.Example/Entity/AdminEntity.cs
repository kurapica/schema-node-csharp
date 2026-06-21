using System.ComponentModel.DataAnnotations;
using SchemaNode.Attribute;

namespace byt.srv.schema.application.Entity;

/// <summary>
/// Admin
/// </summary>
[SchemaApp]
public class AdminEntity
{
    /// <summary>
    /// id
    /// </summary>
    [Index]
    public required Guid Id { get; set; }
    
    /// <summary>
    /// enable
    /// </summary>
    public bool Enabled { get; set; }
}