using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Api.Schema.Edit;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.AI.Mcp;

[McpServerToolType]
public class SchemaTools
{
    #region Schema

    [McpServerTool, Description(
         "Retrieve schema node definitions for a specified schema type or namespace. " +
         "Returns the node schema and its child type structures. " +
         "If no name is provided, the root namespace schemas are returned." + 
         "Each returned item is an instance of schema type: system.schema.nodeschema. " +
         "The structure and field meanings follow the definition of this schema type.")]
    public static async Task<NodeSchema> LoadSchema(
        SchemaContext context,
        [Description(
            "Name of the schema type or namespace to browse. " +
            "Acts as a path in the schema type hierarchy. " +
            "Empty value loads the root-level schema types."
        )] string name)
    {
        AnySchemaType schemaType = await context.GetSchemaTypeAsync(name)
            ?? throw new InvalidOperationException($"Schema type '{name}' not found.");

        return await schemaType.GetNodeSchemas(context);
    }
    
    [McpServerTool, Description(
         "Load enumeration values from an enum schema type. " +
         "Supports hierarchical enums where values may have parent-child relationships. " +
         "Can return the full list or only the direct children of a specified enum value." + 
         "Each returned item is an instance of schema type: system.schema.enumvalueinfo." +
         "The structure and field meanings follow the schema type definition."
    )]
    public static async Task<EnumValueInfo[]> LoadEnumSubList(
        SchemaContext context,
        [Description("Name of the enumeration schema type to load values from.")] string name,
        [Description(
            "Parent enum value used to load its direct child values. " +
            "If null, loads top-level values."
        )] string? value = null,
        [Description(
            "If true, returns all enum values ignoring hierarchy. " +
            "If false or null, hierarchy-based loading is used."
        )] bool? fullList = null)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(name);
        if (node is not EnumType @enum) 
            throw new InvalidOperationException($"Enum schema type '{name}' not found.");

        return await @enum.LoadEnumSubListAsync(context, value, fullList ?? false);
    }
    
    [McpServerTool, Description(
         "Load access control information for a specific enum value from a hierarchical enum schema type. " +
         "Each returned item represents an access node in the enum hierarchy, describing which enum values are accessible. " +
         "Sub-list access nodes may be included depending on the loading options. " +
         "Each returned item is an instance of schema type: system.schema.enumvalueaccess. " +
         "The structure and field meanings follow the schema type definition."
    )]
    public static async Task<EnumValueAccess[]> LoadEnumAccessList(
        SchemaContext context,
        [Description("Name of the hierarchical enumeration schema type.")] string name,
        [Description("The enum value whose access scope should be evaluated.")] string value,
        [Description("If true, only load access for the specified value itself, without any child access nodes.")] bool? noSubList = null,
        [Description( "If true, also include access information for child enum values. " +
                      "Ignored when noSubList is true.")] bool? withSubList = null)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(name);
        if (node is not EnumType @enum) 
            throw new InvalidOperationException($"Enum schema type '{name}' not found.");

        return await @enum.LoadEnumAccessListAsync(context, value, noSubList, withSubList);
    }
    
    [McpServerTool, Description(
         "Invoke a function defined in the schema type system. " +
         "The function is identified by its schema type name and represents an executable semantic unit. " +
         "Input parameters must follow the function's schema-defined signature. " +
         "The result is returned as structured JSON data that conforms to the function's schema return type definition." +
         "The returned data represents an instance of the schema return type."+
         "Field meanings follow the schema type definition."
         )]
    public static async Task<JsonNode?> CallFunction(
        SchemaContext context,
        [Description("Schema type name of the function to invoke.")] string functionName,
        [Description(
            "Function input arguments as a JSON array. " +
            "Each element corresponds to a parameter defined in the function's schema signature."
            )]JsonNode[] parameters,
        [Description(
            "Optional schema type name that defines the expected return structure. " +
            "If provided, the result will conform to this schema type."
            )] string? returnType = null)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(functionName);
        if (node is not FunctionType functionType)
            throw new InvalidOperationException($"Function '{functionName}' not found.");

        return await functionType.CallAsync<JsonNode>(context, parameters as object[], returnType);
    }
    
    [McpServerTool, Description(
         "Create or update a schema node definition in the schema type system. " +
         "This operation modifies the schema type model by saving the provided node schema. " +
         "The input must be an instance of schema type: system.schema.nodeschema. " +
         "If a schema node with the same identity exists, it will be updated; otherwise, a new node will be created. " +
         "Returns true if the schema node was successfully saved."
    )]
    public static async Task<bool> SaveSchema(
        SchemaContext context, 
        [Description( "Schema node definition to save. " +
                      "Represents a schema type structure and must conform to schema type: system.schema.nodeschema.")
        ]NodeSchema nodeSchema)
    {
        nodeSchema.LoadState = SchemaLoadState.Server;
        return await context.SaveSchemaAsync(nodeSchema);
    }

    [McpServerTool, Description(
         "Save a set of child enumeration nodes to a hierarchical enum schema type. " +
         "This operation modifies the enum hierarchy by updating the sub-list under a specific enum value or at the root level. " +
         "The provided items must be instances of schema type: system.schema.enumvalueinfo. " +
         "Depending on the append option, the existing sub-list may be replaced or extended. " +
         "Returns true if the update was successful."
    )]
    public static async Task<bool> SaveEnumSubList(SchemaContext context,
        [Description("Schema type name of the hierarchical enumeration to modify.")] string name,
        [Description( 
            "Parent enum value whose child list is being modified. ")] string value,
        [Description(
            "List of child enum nodes to save. " +
            "Each item must conform to schema type: system.schema.enumvalueinfo.")] EnumValueInfo[] subList,
        [Description(
            "If true, new items are appended to the existing sub-list. " +
            "If false or null, the existing sub-list under the target node will be replaced.")]bool? append)
    {
        return await context.SaveEnumSubListAsync(name, value, subList, append ?? false);
    }
    
    [McpServerTool,  Description(
         "Delete a schema node from the schema type system. " +
         "This operation modifies the schema type model by removing the specified schema type or namespace. " +
         "After deletion, the removed schema definitions will no longer be available for type reflection or semantic operations. " +
         "Returns true if the deletion was successful."
    )]
    public static async Task<bool> DeleteSchema(
        SchemaContext context,
        [Description("Schema type name or namespace path to remove from the schema type system.")] string name)
    {
        return await context.DeleteSchemaAsync(name);
    }
    
    #endregion

    #region App Schema

    [McpServerTool, Description(
         "Load the application schema model for a specified application name. " +
         "An application schema represents a business-level model container built on top of the schema type system. " +
         "It may act as either a functional application or a container of sub-applications. " +

         "The returned model can include: " +
         "application fields, field relations, workflows, authentication policies, " +
         "and nested sub-applications. " +

         "If includeTypes is true, the response also contains the schema type definitions " +
         "referenced by this application (node schemas). " +

         "The returned object is an instance of schema type: system.schema.appschema. " +
         "All field meanings and structures follow that schema definition." +
         "This model acts as a business semantic composition unit rather than a primitive schema type."
    )]
    public static async Task<AppSchema?> LoadAppSchema(
        SchemaContext context,
        [Description("The application schema name")]  string appName,
        [Description("Whether contains the types that the application used")] bool includeTypes = false)
    {
        AppType? node = await context.GetAppTypeAsync(appName);
        if (node == null) return null;
        
        // Generate schema
        AppSchema schema = new()
        {
            Name = node.Name,
            Display = node.Display,
            ScopePolicy = node.ScopePolicy,
            Auth = node.Auth?.Name,
            Auths = node.Auths,
            Status = node.Status,
            HasApps = node.Apps is { Length: > 0 },
            HasFields = node.Fields is { Count: > 0 },
            Workflows = node.Workflows?.Select(w => (AppWorkflowSchema)w).ToArray(),
            Additional = node.Additional,
            Apps = node.Apps?.Select(a => {
                AppType? childNode = node.SubAppList?.Values.FirstOrDefault(p => p.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
                return new AppSchema
                {
                    Name = a.Name,
                    Display = a.Display,
                    Auth = a.Auth,
                    Auths = a.Auths,
                    Status = node.Status,
                    Additional = a.Additional,
                    HasApps = (a.HasApps ?? false) || a.Apps is { Length: > 0 } || childNode?.Apps is {  Length: > 0 },
                    HasFields = (a.HasFields ?? false) || a.Fields is { Length: > 0 } || childNode?.Fields is { Count: > 0},
                };
            }).ToArray(),
        };

        if (node.Fields is { Count: > 0 })
        {
            schema.Fields = node.Fields.Select(p => (AppFieldSchema)p).ToArray();
            schema.Relations = node.Relations?.Select(r => new StructRelationSchema
            {
                Field = !string.IsNullOrEmpty(r.DataField) ? $"{r.AppField}.{r.DataField}" : r.AppField,
                Prop = r.Prop,
                Func = r.Func,
                Args = r.Args.Select(a => new FuncCallArg
                {
                    Name = !string.IsNullOrEmpty(a.DataField) ? $"{a.AppField}.{a.DataField}" : a.AppField,
                    Value = a.Value,
                }).ToArray()
            }).ToArray();

            if(includeTypes)
                schema.NodeSchemas = await node.GetNodeSchemas(context, includeUsedBy: true);
        }

        return schema;
    }
    
    [McpServerTool, Description(
         "Create or update the base definition of an application schema in the schema type system. " +
         "An application schema represents a business-level model container built on top of the schema type system. " +

         "This operation ONLY manages the core application container metadata, " +
         "such as application name, display information, authentication settings, and field relations for frontend. " +

         "It DOES NOT include or modify application fields, workflows, or other structural components. " +
         "Those parts are managed by their dedicated tools. " +

         "The provided data must conform to schema type: system.schema.appschema. " +
         "Returns true if the application schema base definition was successfully saved." +
         "This tool should be used to manage application identity and hierarchy, not business structure."
    )]
    public static async Task<bool> SaveAppSchema(
        SchemaContext context,
        [Description( "Application schema definition to save. " +
                      "Represents a business-level application model and must conform to schema type: system.schema.appschema.")
        ]AppSchemaData appSchemaData)
    {
        return await context.SaveAppSchemaAsync(appSchemaData);
    }
    
    [McpServerTool, Description(
         "Delete an application schema container from the schema type system. " +
         "An application schema represents a business-level model container built on top of the schema type system. " +

         "This operation can only be executed when the application schema is empty. " +
         "The application must NOT have any sub-applications, fields, workflows, or other dependent business components. " +
         "In other words, only a leaf-level and structurally empty application container can be deleted. " +

         "This tool is intended for removing unused or placeholder application containers, " +
         "not for restructuring active business models. " +

         "Returns true if the application schema was successfully deleted." +
         "This operation enforces structural integrity of the application model hierarchy."
    )]
    public static async Task<bool> DeleteAppSchema(
        SchemaContext context,
        [Description("Application schema name to delete from the system.")] string appName)
    {
        return await context.DeleteAppSchemaAsync(appName);
    }
    
    [McpServerTool, Description(
         "Create or update a data field definition within a specified application schema. " +
         "An application field schema defines a logical data field that belongs to an application model. " +

         "If the field is not marked as frontend-only, it represents a persistent data field " +
         "and may result in physical data storage structures being created or updated. " +

         "Fields can declare data propagation behavior by referencing a function type (Func) " +
         "and specifying dependent source fields through Args. " +
         "This enables automatic data flow and derived value computation between fields, " +
         "forming a field-level data dependency graph inside the application. " +

         "The provided field schema must conform to schema type: system.schema.appfieldschema. " +
         "Returns true if the application field schema was successfully saved." + 
         "This tool defines both data structure and optional computation semantics of application-level data."
    )]
    public static async Task<bool> SaveAppFieldSchema(
        SchemaContext context,
        [Description("Application schema name where the field belongs.")] string appName,
        [Description( "Application field schema definition to save. " +
                      "Represents a field within an application and must conform to schema type: system.schema.appfieldschema.")
        ]AppFieldSchema appFieldSchema)
    {
        return await context.SaveAppFieldSchemaAsync(appName, appFieldSchema);
    }
    
    [McpServerTool, Description(
         "Swap the presentation order of two existing data fields within a specified application schema. " +
         "This operation only affects the relative ordering of fields, typically for user interface display purposes. " +

         "It does NOT modify field definitions, data types, storage structure, data dependencies, " +
         "or business logic. No data model or computation semantics are changed. " +

         "The two fields must already exist in the target application schema. " +
         "After execution, their display positions are exchanged. " +

         "Returns true if the swap operation was successful."
    )]
    public static async Task<bool> SwapAppFieldSchema(
        SchemaContext context,
        [Description("Application schema name where the fields belong.")] string appName,
        [Description("Name of the first application field to swap.")] string fieldA,
        [Description("Name of the second application field to swap.")] string fieldB)
    {
        return await context.SwapAppFieldSchemaAsync(appName, fieldA, fieldB);
    }
    
    [McpServerTool, Description(
         "Delete an existing data field definition from a specified application schema. " +
         "This operation removes the field from the application data model. " +

         "If the field represents a persistent (non-frontend-only) field, " +
         "the corresponding physical data storage structures may also be altered or removed. " +

         "Deleting a field may impact data integrity, field dependency relationships, " +
         "and derived data flows within the application. " +
         "This is a structural and potentially destructive operation. " +

         "The field must already exist in the target application schema. " +
         "Returns true if the field schema was successfully deleted." + 
         "This tool should only be used when the field is no longer required and no longer participates in business logic or data dependencies."
    )]
    public static async Task<bool> DeleteAppFieldSchema(
        SchemaContext context,
        [Description("Application schema name where the field belongs.")] string appName,
        [Description("Name of the application field to delete.")] string fieldName)
    {
        return await context.DeleteAppFieldSchemaAsync(appName, fieldName);
    }

    #endregion
    
    #region Data
    
    [McpServerTool, Description(
         "Batch data retrieval tool for querying application data. " +
         "Accepts multiple queries in a single request. " +
         "Each query specifies the target application schema, fields to retrieve, and optional filter conditions. " +
         "Returns the results for each query, structured according to the corresponding application schema definitions. " +
         "This tool is used to efficiently fetch data from one or more applications in a single call."
     )]
    public static async Task<BatchQueryAppDataResponse> BatchQueryAppData(
        SchemaContext context,
        [Description("Array of application data queries to retrieve application data in batch.")]
        AppDataQuery[] queries)
    {
        (AppDataResult[] result, NodeSchema[]? schemas) = await context.BatchQueryAppDataAsync(queries);
        return new BatchQueryAppDataResponse
        {
            Results = result,
            Schemas = schemas
        };
    }
    
    [McpServerTool, Description(
         "Batch data push tool for saving application data. " +
         "Accepts multiple data push operations in a single request. " +
         "Each operation specifies the target application schema, fields to update, new data values, and optional delete instructions. " +
         "The tool validates and saves the provided data according to the application schema definitions and field-level rules. " +
         "Returns the success status and any error information for each push operation. " +
         "This tool is used to efficiently update data in one or more applications in a single call."
     )]
    public static async Task<PushAppDataResponse> PushAppData(
        SchemaContext context,
        [Description("The application schema name where data will be pushed.")] string app,
        [Description("The target within the application where data will be pushed.")] string target,
        [Description("Dictionary of data fields and their corresponding push queries.")] Dictionary<string, DataFieldPushQuery> datas)
    {
        Dictionary<string, AppDataFieldPushQuery> convData = [];
        foreach (var (key, value) in datas)
        {
            JsonElement? data = value.Data;
            JsonElement? deletes = value.Deletes;
            JsonNode? update = null;
            JsonArray? delete = null;
            if (data != null && data.Value.ValueKind != JsonValueKind.Null)
                update = JsonNode.Parse(data.Value.GetRawText());
            if (deletes != null && deletes.Value.ValueKind != JsonValueKind.Null)
            {
                delete = JsonNode.Parse(deletes.Value.GetRawText()) as JsonArray;
            }
            
            convData[key] = new AppDataFieldPushQuery
            {
                Data = update,
                Deletes = delete
            };
        }
        var (result, error) = await context.PushAppDataAsync(app, target, convData);
        return new PushAppDataResponse
        {
            Result = result,
            Error = error
        };
    }

    public class DataFieldPushQuery
    {
        /// <summary>
        /// The push data
        /// </summary>
        public JsonElement? Data { get; set; }
    
        /// <summary>
        /// The deleted data
        /// </summary>
        public JsonElement? Deletes { get; set; }
    }
    
    #endregion
}
