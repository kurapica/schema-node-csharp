using Microsoft.OpenApi;
using SchemaNode.Utility;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;

namespace SchemaNode.Http;

/// <summary>
/// Provides a swagger document for all APIs in the microservice.
/// </summary>
public static class SchemaApiDocument
{
    /// <summary>
    /// Generate the document
    /// </summary>
    public static OpenApiDocument Generate(OpenApiDocument? document = null)
    {
        // Create swagger model.
        document ??= new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Version = "1.0",
                Description = "The Schema API",
                Title = "Schema Apis"
            },
        };
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        IDictionary<string, IOpenApiSchema> schemas = document.Components!.Schemas;
        HandledTypes.Value = new HashSet<string>();

        // Add each API.
        foreach (var (api, url) in SchemaApiExtension.GetSchemaApis())
        {
            // Add document paths.
            AddDocumentPath(
                schemas,
                document.Paths,
                api.Api,
                api.Request,
                api.Response,
                url
            );
        }

        return document;
    }

    #region Implementations

    /// <summary>
    /// Add the paths object to OpenAPI document.
    /// </summary>
    static void AddDocumentPath(IDictionary<string, IOpenApiSchema> schemas, OpenApiPaths paths, Type apiType, Type requestType, Type responseType, string url)
    {
        // Get the URL.
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException();
        }

        //Get category from attribute
        string category = "";

        // Get category from namespace
        if (string.IsNullOrEmpty(category) || !category.Contains('.'))
        {
            string[] assemblyNames = apiType.Assembly.FullName?.Split(",")[0].Split(".") ?? [];
            string[] typeNames = apiType.FullName?.Split(",")[0].Split(".") ?? [];
            if (!string.IsNullOrEmpty(category))
                typeNames[^1] = category;
            int i = 0;
            for (; i < assemblyNames.Length; i++)
            {
                if (i >= typeNames.Length || assemblyNames[i] != typeNames[i]) break;
            }
            category = string.Join(".", typeNames.Skip(i).SkipLast(1).Select(s => s.ToLower()).ToArray());
        }
        
        // Create the operation.
        OpenApiOperation operation = new()
        {
            OperationId = requestType.Name[..^"Request".Length],
            Tags = !string.IsNullOrEmpty(category)
                ? new HashSet<OpenApiTagReference>
                {
                    new OpenApiTagReference(category)
                }
                : null,
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["id"] = new OpenApiSchema { 
                                    Type = JsonSchemaType.String
                                },
                                ["jsonrpc"] = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.String,
                                    Default ="2.0"
                                },
                                ["params"] = GetTypeSchema(schemas, requestType)
                            }
                        }
                    }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new()
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Object,
                                Properties = new Dictionary<string, IOpenApiSchema>
                                {
                                    ["id"] = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.String,
                                    },
                                    ["jsonrpc"] = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.String,
                                        Default = "2.0"
                                    },
                                    ["method"] = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.String,
                                    },
                                    ["error"] = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.Object,
                                        Properties = new Dictionary<string, IOpenApiSchema>
                                        {
                                            ["data"] = new OpenApiSchema
                                            {
                                                Type = JsonSchemaType.Object
                                            },
                                            ["message"] = new OpenApiSchema
                                            {
                                                Type = JsonSchemaType.Object
                                            },
                                            ["code"] = new OpenApiSchema
                                            {
                                                Type = JsonSchemaType.String,
                                                Enum = System.Enum.GetNames<SchemaApiResponseErrorCode>().Select(s => (JsonNode)JsonValue.Create(GetRegularStrFormat(s, true))).ToList()
                                            }
                                        },
                                    },
                                    ["result"] = GetTypeSchema(schemas, responseType)
                                }
                            }
                        }
                    }
                }
            },
            // Add Summary
            Summary = GetSummaryFromXmlDoc(apiType, "T:")
        };

        // Add the path item.
        paths[$"/{url}"] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                { HttpMethod.Post, operation }
            },
        };
    }

    /// <summary>
    /// Register the scheams object to OpenAPI document.
    /// </summary>
    static OpenApiSchema GetTypeSchema(IDictionary<string, IOpenApiSchema> schemas, Type type)
    {
        #region Nullable<>

        if (type.IsSubclassOfGenericType(typeof(Nullable<>)))
        {
            OpenApiSchema schema = new();
            schema.OneOf = [GetTypeSchema(schemas, type.GetGenericArguments()[0]), new OpenApiSchema { Type = JsonSchemaType.Null }];
            return schema;
        }

        #endregion

        #region Value Type: string, int, float, bool ...

        if (TypeMapping.ContainsKey(type))
        {
            return new OpenApiSchema
            {
                Type = TypeMapping[type].Type,
                Format = TypeMapping[type].Format
            };
        }

        #endregion

        #region Reference Type: List<>, Dictionary<,>

        if (type.IsSubclassOfGenericType(typeof(List<>)))
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = GetTypeSchema(schemas, type.GetGenericArguments()[0])
            };
        }
        if (type.IsSubclassOfGenericType(typeof(Dictionary<,>)))
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = GetTypeSchema(schemas, type.GetGenericArguments()[1])
            };
        }

        #endregion

        #region Register to the components/schemas

        string typeKey = GetRegularStrFormat(type.Name, true);

        if (!HandledTypes.Value!.Contains(typeKey))
        {
            HandledTypes.Value.Add(typeKey);

            // default value holder
            object? defaultHolder = null;
            try
            {
                defaultHolder = Activator.CreateInstance(type);
            }
            catch
            {
                //pass
            }

            // Get the properties
            Dictionary<string, IOpenApiSchema> props = new();
            ISet<string> requiredSet = new HashSet<string>();
            void PropertyHandler(PropertyInfo t)
            {
                string propName = GetRegularStrFormat(t.Name);

                //[Required] Check
                if (t.GetCustomAttribute<RequiredAttribute>() != null)
                {
                    requiredSet.Add(propName);
                }
                Type propertyType = t.PropertyType;

                //Check Nullable Enum
                if (propertyType.IsSubclassOfGenericType(typeof(Nullable<>)))
                {
                    Type inner = propertyType.GetGenericArguments()[0];
                    if (inner.IsEnum)
                        propertyType = inner;
                }
                if (propertyType.IsEnum)
                {
                    props.Add(propName, new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = GetRegularStrFormat(propertyType.Name), // Save Enum name to the format
                        Enum = Activator.CreateInstance(propertyType)
                            ?.GetType()
                            .GetEnumValues()
                            .Cast<object>()
                            .Select(value => (JsonNode)JsonValue.Create(value.ToString())!)
                            .ToList(),
                        Description = GetSummaryFromXmlDoc(type, "P:", t),
                        Deprecated = propertyType.GetCustomAttribute<ObsoleteAttribute>() != null
                    });
                }
                else
                {
                    OpenApiSchema schema = GetTypeSchema(schemas, propertyType);
                    schema.Description = GetSummaryFromXmlDoc(type, "P:", t);
                    try
                    {

                        if (defaultHolder != null)
                        {
                            object? def = t.GetValue(defaultHolder);
                            if (def != null)
                            {
                                if (def is int intval)
                                    schema.Default = intval == 0 ? null : intval;
                                else if (def is long lval)
                                    schema.Default = lval == 0 ? null : lval;
                                else if (def is double dbl)
                                    schema.Default = dbl == 0 ? null : dbl;
                                else if (def is float fval)
                                    schema.Default = fval == 0 ? null : fval;
                                else if (def is bool bval)
                                    schema.Default = bval ? true : null;
                                else
                                    schema.Default = def.ToString();
                            }
                        }
                    }
                    catch
                    {
                        //pass
                    }
                    props.Add(propName, schema);
                }
            }

            List<PropertyInfo> propInfos = type.GetProperties().Where(t => t.GetCustomAttribute<JsonIgnoreAttribute>() == null).ToList();

            // Require properties from super class first
            propInfos
                .Where(t => !t.Name.StartsWith("_") && t.GetCustomAttribute<RequiredAttribute>() != null && t.DeclaringType != type)
                .ToList().ForEach(PropertyHandler);

            // Require properties from the class second
            propInfos
                .Where(t => !t.Name.StartsWith("_") && t.GetCustomAttribute<RequiredAttribute>() != null && t.DeclaringType == type)
                .ToList().ForEach(PropertyHandler);

            // Non-require properties
            propInfos
                .Where(t => !t.Name.StartsWith("_") && t.GetCustomAttribute<RequiredAttribute>() == null)
                .ToList().ForEach(PropertyHandler);

            // Add the type schema
            schemas.Add(typeKey, new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = GetSummaryFromXmlDoc(type, "T:"),
                Properties = props,
                Required = requiredSet
            });
        }

        #endregion

        #region Defult Schema: Reference

        return new OpenApiSchema
        {
            DynamicRef = "#/components/schemas/" + GetRegularStrFormat(type.Name, true)
        };

        #endregion
    }

    /// <summary>
    /// Define the mapping of type to format string.
    /// </summary>
    static readonly Dictionary<Type, TypeFormat> TypeMapping = new()
    {
        { typeof(int), new TypeFormat(JsonSchemaType.Integer, null) },
        { typeof(short), new TypeFormat(JsonSchemaType.Integer, "int16") },
        { typeof(long), new TypeFormat(JsonSchemaType.Integer, "int64") },
        { typeof(float), new TypeFormat(JsonSchemaType.Number, "float") },
        { typeof(double), new TypeFormat(JsonSchemaType.Number, "double") },
        { typeof(decimal), new TypeFormat(JsonSchemaType.Number, "decimal") },
        { typeof(string), new TypeFormat(JsonSchemaType.String, null) },
        { typeof(bool), new TypeFormat(JsonSchemaType.Boolean, null) },
        { typeof(DateTime), new TypeFormat(JsonSchemaType.String, "datetime") },
        { typeof(DateTimeOffset), new TypeFormat(JsonSchemaType.String, "datetime") },
        { typeof(sbyte), new TypeFormat(JsonSchemaType.Integer, "sbyte") },
        { typeof(byte), new TypeFormat(JsonSchemaType.Integer, "byte") },
        { typeof(ushort), new TypeFormat(JsonSchemaType.Integer, "ushort") },
        { typeof(uint), new TypeFormat(JsonSchemaType.Integer, "uint") },
        { typeof(ulong), new TypeFormat(JsonSchemaType.Integer, "ulong") },
        { typeof(char), new TypeFormat(JsonSchemaType.Integer, "char") },
        { typeof(Guid), new TypeFormat(JsonSchemaType.String, "guid") },
        { typeof(JsonNode), new TypeFormat(JsonSchemaType.Object, "any") },
        { typeof(JsonArray), new TypeFormat(JsonSchemaType.Object, "array") },
        { typeof(JsonObject), new TypeFormat(JsonSchemaType.Object, "hash") },
        { typeof(JsonValue), new TypeFormat(JsonSchemaType.Object, "scalar") }
    };

    /// <summary>
    /// Get a regular string format.
    /// </summary>
    static string GetRegularStrFormat(string str, bool skip = false)
    {
        return skip ? str : str.ToCamelCase();
    }

    /// <summary>
    /// Get summary contents from XML document.
    /// </summary>
    static string GetSummaryFromXmlDoc(Type type, string preFix, PropertyInfo? prop = null)
    {
        string typeName = prop != null ? prop.DeclaringType!.Name : type.Name;
        string xmlPath = prop != null ? prop.DeclaringType!.Assembly.Location.Replace(".dll", ".xml") : type.Assembly.Location.Replace(".dll", ".xml");
        string propertyName = prop != null ? prop.Name : string.Empty;

        if (!File.Exists(xmlPath)) return string.Empty;
        
        if (!XmlFiles.ContainsKey(xmlPath))
        {
            XmlDocument document = new();
            document.Load(xmlPath);
            XmlFiles[xmlPath] = document;
        }
        string xPath = "/doc/members";
        XmlNode? nodeList = XmlFiles[xmlPath].SelectSingleNode(xPath);
        foreach (XmlElement node in nodeList!)
        {
            if (node.HasChildNodes && node.Attributes.Count > 0)
            {
                string name = node.Attributes[0].Value;
                if (!string.IsNullOrEmpty(name))
                {
                    if ((name.StartsWith(preFix) && name.Contains(typeName) && string.IsNullOrEmpty(propertyName))
                        ||
                        (name.StartsWith(preFix) && name.Contains(typeName) && name.EndsWith(propertyName))
                       )
                    {
                        string summaryContent = node.InnerText;
                        return string.Join("\n",
                            summaryContent
                                .Split('\n', '\r')
                                .Where(t =>
                                    !string.IsNullOrWhiteSpace(t) &&
                                    !string.IsNullOrEmpty(t))
                                .Select(p => p.Trim())
                                .ToArray()
                        );
                    }
                }
            }
        }
        return string.Empty;
    }

    #endregion

    #region Private

    static readonly Dictionary<string, XmlDocument> XmlFiles = new();
    static readonly AsyncLocal<HashSet<string>> HandledTypes = new();

    #endregion

    #region Inner Types

    record TypeFormat(JsonSchemaType Type, string? Format);

    #endregion
}