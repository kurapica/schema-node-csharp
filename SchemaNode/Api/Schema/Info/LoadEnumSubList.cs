using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The LoadEnumSubList api
/// </summary>
public class LoadEnumSubListApi : SchemaApi<LoadEnumSubListRequest, LoadEnumSubListResponse>
{
    /// <inheritdoc />
    protected override async Task<LoadEnumSubListResponse?> ExecuteAsync(LoadEnumSubListRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadEnumSubList [Request]{request}", request);

        AnySchemeType? node = await SchemaContext.GetSchemaNodeAsync(request.Name);

        return new LoadEnumSubListResponse
        {
            Values = node is EnumType @enum ? await @enum.LoadEnumSubListAsync(SchemaContext, request.Value, request.FullList) : []
        };
    }
}

/// <summary>
/// The LoadEnumSubList request
/// </summary>
public class LoadEnumSubListRequest : SchemaApiRequest
{
    /// <summary>
    /// The enum schema name
    /// </summary>
    [Required]
    public required string Name { get; set; }
    
    /// <summary>
    /// The enum value to be queried
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// Whether load full list
    /// </summary>
    public bool? FullList { get; set; }
}

/// <summary>
/// The LoadEnumSubList response
/// </summary>
public class LoadEnumSubListResponse : SchemaApiResponse
{
    /// <summary>
    /// The enum values
    /// </summary>
    public required EnumValueInfo[] Values { get; set; }
}