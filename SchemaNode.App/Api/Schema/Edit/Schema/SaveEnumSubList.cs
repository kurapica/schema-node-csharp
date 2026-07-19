using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema.Provider;
using SchemaNode.Struct;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveEnumSubList api
/// </summary>
public class SaveEnumEntriesApi : SchemaApi<SaveEnumSubListRequest, SaveEnumSubListResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveEnumSubListResponse?> ExecuteAsync(SaveEnumSubListRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveEnumSubList [Request]{request}", request);

        return new SaveEnumSubListResponse
        {
            Result = await SchemaContext.SaveEnumEntriesAsync(request.Name, request.Value, request.Values, request.Append ?? false)
        };
    }
}

/// <summary>
/// The SaveEnumSubList request
/// </summary>
public class SaveEnumSubListRequest : SchemaApiRequest
{
    /// <summary>
    /// The enum schema name
    /// </summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The enum value
    /// </summary>
    [Required]
    public string Value { get; set; } = null!;

    /// <summary>
    /// The sub enum values
    /// </summary>
    public Entry<string>[] Values { get; set; } = [];
    
    /// <summary>
    /// Append not override
    /// </summary>
    public bool? Append { get; set; }
}

/// <summary>
/// The SaveEnumSubList response
/// </summary>
public class SaveEnumSubListResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}