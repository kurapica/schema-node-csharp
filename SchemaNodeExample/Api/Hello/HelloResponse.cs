using System;
using SchemaNode.Http;

namespace SchemaNode.Example.Api;

/// <summary>
/// The Hello response data
/// </summary>
public class HelloResponse: SchemaApiResponse
{
    public string Response { get; set; }
}