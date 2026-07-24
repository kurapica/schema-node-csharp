using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The LoadEnumAccessList api
/// </summary>
public class GetEnumEntryAccessApi : SchemaApi<GetEnumEntryAccessRequest, GetEnumEntryAccessResponse>
{
    /// <inheritdoc />
    protected override async Task<GetEnumEntryAccessResponse?> ExecuteAsync(GetEnumEntryAccessRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadEnumAccessList [Request]{request}", request);

        NodeType? node = await SchemaContext.GetNodeTypeAsync(request.Name);
        if (node is not Runtime.EnumType @enum) return new GetEnumEntryAccessResponse();
        
        // authorize
        await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead);

        return new GetEnumEntryAccessResponse
        {
            Access = await @enum.GetEnumEntryAccessAsync(SchemaContext, request.Value, request.Start)
        };
    }
}

/// <summary>
/// The LoadEnumAccessList request
/// </summary>
public class GetEnumEntryAccessRequest : SchemaApiRequest
{
    /// <summary>
    /// The eum schema name
    /// </summary>
    [Required]
    public required string Name { get; set; }
    
    /// <summary>
    /// The access enum value
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// The path start value
    /// </summary>
    public string? Start {get; set;}
}

/// <summary>
/// The LoadEnumAccessList response
/// </summary>
public class GetEnumEntryAccessResponse : SchemaApiResponse
{
    /// <summary>
    /// The enum value access list
    /// </summary>
    public EntryAccess<string>[] Access { get; set; } = [];
}