using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The DeleteEnumSubList api
/// </summary>
public class DeleteEnumSubListApi : SchemaApi<DeleteEnumSubListRequest, DeleteEnumSubListResponse>
{
    /// <inheritdoc />
    protected override async Task<DeleteEnumSubListResponse?> ExecuteAsync(DeleteEnumSubListRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]DeleteEnumSubList [Request]{request}", request);

        return new DeleteEnumSubListResponse
        {
            Result = await SchemaContext.DeleteEnumSubListAsync(request.Name, request.Value)
        };
    }
}

/// <summary>
/// The DeleteEnumSubList request
/// </summary>
public class DeleteEnumSubListRequest : SchemaApiRequest
{
    /// <summary>
    /// The enum schema name
    /// </summary>
    [Required]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The enum value to remove the sub list
    /// </summary>
    [Required]
    public string Value { get; set; } = null!;
}

/// <summary>
/// The DeleteEnumSubList response
/// </summary>
public class DeleteEnumSubListResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}