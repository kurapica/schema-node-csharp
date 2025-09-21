using System;
using System.ComponentModel.DataAnnotations;

namespace SchemaNode.Example.Api;

/// <summary>
/// The Hello request data
/// </summary>
public class HelloRequest : MicroserviceApiRequest
{
    public string Name { get; set; }
}