using System;
using System.ComponentModel.DataAnnotations;
using SchemaNode.Http;

namespace SchemaNode.Example.Api;

/// <summary>
/// The Hello request data
/// </summary>
public class HelloRequest : SchemaApiRequest
{
    public string Name { get; set; }
}