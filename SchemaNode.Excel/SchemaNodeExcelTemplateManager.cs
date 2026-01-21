using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
public class ExcelTemplateApi: SchemaApi<ExcelTemplateRequest, ExcelTemplateResponse>
{
    const string APP_FIELD_TYPE_NOT_SUPPORT_EXCEL_TEMPLATE = "APP_FIELD_TYPE_NOT_SUPPORT_EXCEL_TEMPLATE";
    
    /// <inheritdoc />
    protected override async Task<ExcelTemplateResponse?> ExecuteAsync(ExcelTemplateRequest request, CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]ExcelTemplate [Request]{request}", request);
        
        AppType app = await SchemaContext.GetAppTypeAsync(request.App) ?? throw new Exception(APP_NOT_FOUND);
        AppFieldType field = app.GetField(request.Field) ?? throw new Exception(APP_FIELD_NOT_FOUND);
        if (field.SchemaType is not ArrayType) throw new Exception(APP_FIELD_TYPE_NOT_SUPPORT_EXCEL_TEMPLATE);

        IFormFile? file = request.Files?.FirstOrDefault();
        TemplateManager manager = new (SchemaContext, field, file);
        
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
                        manager.UseStructFieldsForJsonField(k, types);
                }
                catch
                {
                    // ignore
                }
            }
        }
        
        // download template
        if (file is null)
        {
            return new ExcelTemplateResponse
            {
                Output = await manager.DownloadTemplateAsync(request.InputCount ?? 10)
            };
        }
        
        // upload data
        else
        {
            return new ExcelTemplateResponse
            {
                Uploads = await manager.ReadUploadsAsync()
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
}