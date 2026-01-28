using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using SchemaNode.Api.Schema.Edit;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.McpHost.Tools;

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

        return await @enum.LoadEnumSubListAsync(context, value, fullList);
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
            )]JsonArray? parameters = null,
        [Description(
            "Optional schema type name that defines the expected return structure. " +
            "If provided, the result will conform to this schema type."
            )] string? returnType = null)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(functionName);
        if (node is not FunctionType functionType)
            throw new InvalidOperationException($"Function '{functionName}' not found.");

        return await context.CallFunctionAsync(functionType, parameters ?? [], returnType);
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
            "Parent enum value whose child list is being modified. " +
            "If null, the operation applies to the top-level enum nodes.")] string? value,
        [Description(
            "List of child enum nodes to save. " +
            "Each item must conform to schema type: system.schema.enumvalueinfo.")] EnumValueInfo[] subList,
        [Description(
            "If true, new items are appended to the existing sub-list. " +
            "If false or null, the existing sub-list under the target node will be replaced.")]bool? append)
    {
        return await context.SaveEnumSubListAsync(name, value, subList, append);
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
}