using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Attribute;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming

namespace SchemaNode.Excel;

/// <summary>
/// The Excel template download/upload api
/// </summary>
[Plugin("EXCEL_TEMPLATE")]
public class ExcelTemplateApi: SchemaApi<ExcelTemplateRequest, ExcelTemplateResponse>
{
    const string APP_FIELD_TYPE_NOT_SUPPORT_EXCEL_TEMPLATE = "APP_FIELD_TYPE_NOT_SUPPORT_EXCEL_TEMPLATE";
    
    /// <inheritdoc />
    protected override async Task<ExcelTemplateResponse?> ExecuteAsync(ExcelTemplateRequest request, CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]ExcelTemplate [Request]{request}", request);
        
        AppType app = await SchemaContext.GetAppTypeAsync(request.App) ?? throw new Exception(APP_NOT_FOUND);
        AppFieldType field = app.GetField(request.Field) ?? throw new Exception(APP_FIELD_NOT_FOUND);
        if (field.SchemaType is not ArrayType arrayType) throw new Exception(APP_FIELD_TYPE_NOT_SUPPORT_EXCEL_TEMPLATE);

        IFormFile? file = request.Files?.FirstOrDefault();

        TemplateManager manager = new (SchemaContext, field, file, request.Url, request.Suffix);
        
        // Entries
        if (!request.Entries.IsEmpty())
        {
            foreach (var (k, entries) in request.Entries!)
            {
                if (entries is not JsonArray jsonArray || jsonArray.IsEmpty()) continue;
                
                // Enum values
                if (jsonArray[0] is JsonValue)
                {
                    List<string> enumValues = [];
                    foreach (var item in jsonArray)
                        if (item is JsonValue v && v.TryGetValue<string>(out var s))
                            enumValues.Add(s);
                    if (enumValues.Count > 0)
                        manager.UseEnumForField(k, enumValues);
                }
                // Entries
                else if (jsonArray[0] is JsonObject)
                {
                    List<Entry> entryValues = [];
                    foreach (var item in jsonArray)
                    {
                        if (item is not JsonObject e) continue;
                        try
                        {
                            Entry? entry = e.FromJson<Entry>();
                            if (entry != null)
                                entryValues.Add(entry);
                        }
                        catch
                        {
                            // skip invalid entry
                        }
                    }
                    if (entryValues.Count > 0)
                        manager.UseEnumForField(k, entryValues);
                }
            }
        }
        
        // Dynamic Types
        if (!request.DynamicTypes.IsEmpty())
        {
            foreach (var (k, fields) in request.DynamicTypes!)
            {
                if (fields is not JsonArray dynamicFields) continue;
                try
                {
                    List<StructFieldConfig>? types = dynamicFields.FromJson<List<StructFieldConfig>>();
                    if (types is { Count: > 0 })
                    {
                        foreach (StructFieldConfig type in types)
                        {
                            type.SchemeType = !string.IsNullOrWhiteSpace(type.Type) 
                                ? await SchemaContext.GetSchemaTypeAsync(type.Type) 
                                : null;
                        }
                        manager.UseStructFieldsForJsonField(k, types);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
        
        // download template
        if (file is null && string.IsNullOrEmpty(request.Url))
        {
            return new ExcelTemplateResponse
            {
                Output = await manager.DownloadTemplateAsync(request.InputCount ?? 100)
            };
        }
        
        // upload data
        else
        {
            // temp use struct index for require
            List<string> requireFields = [];
            if (arrayType.Primary is { Length: > 0 } primaryKeys)
            {
                requireFields.AddRange(primaryKeys);
            }

            if (arrayType.Indexes is { Length: > 0 } indexes)
            {
                foreach (DataIndex dataIndex in indexes)
                {
                    requireFields.AddRange(dataIndex.Fields);
                }
            }
            
            JsonArray uploads = await manager.ReadUploadsAsync(requireFields.Distinct().ToArray());
            if (uploads.IsEmpty()) throw new Exception("EXCEL_TEMPLATE_NO_VALID_DATA");
            if (request.Save == true && !string.IsNullOrEmpty(request.Target))
            {
                Dictionary<string, AppDataFieldPushQuery> pushData = [];
                pushData[request.Field] = new AppDataFieldPushQuery { Data = uploads };
                
                (bool result, JsonNode? error) = await SchemaContext.PushAppDataAsync(request.App, request.Target, pushData);
                return new ExcelTemplateResponse
                {
                    Uploads = uploads,
                    Result = result,
                    Error = error
                };
            }
            
            return new ExcelTemplateResponse
            {
                Uploads = uploads
            };
        }
    }
}

/// <summary>
/// The Excel template request
/// </summary>
public class ExcelTemplateRequest : SchemaApiRequest
{
    /// <summary>
    /// The application
    /// </summary>
    [Required]
    public required string App { get; set; }
    
    /// <summary>
    /// The application field
    /// </summary>
    [Required]
    public required string Field { get; set; }
    
    /// <summary>
    /// The application target
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The field entries
    /// </summary>
    public JsonObject? Entries { get; set; }
    
    /// <summary>
    /// The dynamic types for JSON fields
    /// </summary>
    public JsonObject? DynamicTypes { get; set; }
    
    /// <summary>
    /// The input row count
    /// </summary>
    public int? InputCount { get; set; }
    
    /// <summary>
    /// Whether to save the uploaded data
    /// </summary>
    public bool? Save { get; set; }
    
    /// <summary>
    /// The upload file url
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// The file suffix
    /// </summary>
    public string? Suffix { get; set; }
}

/// <summary>
/// The Excel template response
/// </summary>
public class ExcelTemplateResponse : SchemaApiResponse
{
    /// <summary>
    /// The upload data
    /// </summary>
    public JsonArray? Uploads { get; set; }
    
    /// <summary>
    /// The auto save result
    /// </summary>
    public bool? Result { get; set; }
    
    /// <summary>
    /// The error data
    /// </summary>
    public JsonNode? Error { get; set; }
}