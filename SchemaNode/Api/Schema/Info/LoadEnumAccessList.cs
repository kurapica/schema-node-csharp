using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The LoadEnumAccessList api
/// </summary>
public class LoadEnumAccessListApi : SchemaApi<LoadEnumAccessListRequest, LoadEnumAccessListResponse>
{
    /// <inheritdoc />
    protected override async Task<LoadEnumAccessListResponse?> ExecuteAsync(LoadEnumAccessListRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadEnumAccessList [Request]{request}", request);

        NamespaceNode? node = await SchemaContext.GetSchemaNodeAsync(request.Name);

        return new LoadEnumAccessListResponse
        {
            Access = node is EnumNode @enum ? await @enum.LoadEnumAccessListAsync(SchemaContext, request.Value, request.NoSubList, request.WithSubList) : []
        };
    }
}

/// <summary>
/// The LoadEnumAccessList request
/// </summary>
public class LoadEnumAccessListRequest : SchemaApiRequest
{
    /// <summary>
    /// The eum schema name
    /// </summary>
    [Required]
    public required string Name { get; set; }
    
    /// <summary>
    /// The access enum value
    /// </summary>
    public required string Value { get; set; }
    
    /// <summary>
    /// Don't load sub list
    /// </summary>
    public bool? NoSubList { get; set; }
    
    /// <summary>
    /// Also load the sub list of the given value
    /// </summary>
    public bool? WithSubList { get; set; }
}

/// <summary>
/// The LoadEnumAccessList response
/// </summary>
public class LoadEnumAccessListResponse : SchemaApiResponse
{
    /// <summary>
    /// The enum value access list
    /// </summary>
    public EnumValueAccess[] Access { get; set; }
}