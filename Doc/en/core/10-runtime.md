# 10. The Semantic Runtime

The previous chapters introduced the core concepts and prototypes of SchemaNode.Core. This final chapter explains how to initialize the semantic runtime, and how to load semantic types, create and process semantic entities through the runtime.

## 1. Lifecycle of the Semantic Runtime

In `Program.cs` of `SchemaNode.Example`, you can see the runtime initialization process:

```csharp
builder.Services.PrepareSchemaRuntime();

var app = builder.Build();

await app.Services.InitSchemaRuntimeAsync();
await app.Services.ActivateSchemaRuntimeAsync();
```

These three methods are responsible for preparing, initializing, and activating the semantic runtime respectively, and the corresponding implementations are located in `SchemaNode.Core/Service/Service.cs`.

- **Prepare**: registers the services required by the runtime, and notifies `IRuntimeStageHandler` implementations to perform preparation work. Different assemblies can participate in the runtime lifecycle through this mechanism, extending the runtime with prototypes, semantic types, and related processing.
- **Init**: discovers registered prototypes and their generators, registers semantic types declared as `SchemaType`, and constructs the corresponding semantic declarations through the generators. The initialization process includes stages such as `SystemSchemaLoading`, `SystemSchemaLoaded`, `SchemaLoading`, and `SchemaLoaded`, each of which is completed cooperatively through `IRuntimeStageHandler`.
- **Activate**: notifies each lifecycle handler to activate the runtime.

The runtime also provides a corresponding deactivation operation `DeactivateSchemaRuntimeAsync`, used to notify each handler to perform deactivation logic.

The same service can host multiple semantic runtimes, and each runtime can have different prototypes and semantic types. Therefore, a runtime is not only a static registry, but also a hosting environment for specific semantic definitions and processing capabilities.

## 2. Obtaining Semantic Types

After the runtime is initialized and activated, `ISchemaRuntime` or `ISchemaContext` can be obtained through dependency injection.

Usually, what users interact with more directly is `SchemaContext`. The runtime processing related to the current `node` prototype family is implemented by it, and the general `ISchemaContext` interface itself does not guarantee these capabilities.

For example, to obtain the runtime type corresponding to `system.string`:

```csharp
var schemaContext =
    app.Services.GetRequiredService<ISchemaContext>() as SchemaContext;

var runtimeType =
    await schemaContext.GetNodeTypeAsync("system.string");
```

`GetNodeTypeAsync` obtains the corresponding runtime type by semantic type name. Different types can provide different runtime operations, for example `FunctionType` provides `CallAsync` to execute functions.

The related implementation is concentrated in the `Runtime` directory. Runtime types turn the semantic definitions introduced earlier into operational capabilities that programs can invoke.

## 3. Creating and Processing Data Semantic Entities

Common data semantic types include `node.bool`, `node.int`, and `node.string`. They not only describe the type of a value, but also provide the ability to create the corresponding semantic nodes.

For example, the core implementation of `IntType` is as follows:

```csharp
public sealed class IntType : ScalarType
{
    public override bool IsIndexable => true;

    public override IValueAccess Create(
        IValueAccess? parent = null,
        IPropertyProvider? propertyProvider = null)
        => new IntNode
        {
            Type = this,
            Parent = parent,
            PropertyProvider = propertyProvider ?? this
        };

    protected override ScalarSchema? GetScalarSchema()
        => GetProperty<IntProperty>()?.Value;
}
```

Among them, `Create` creates a data node according to the semantic type, and associates information such as the type, parent node, and property provider. Different data semantic types create the corresponding nodes through their own implementations.

`IValueAccess` provides general access capabilities for data nodes, including path access, reading and setting values, determining empty values, and obtaining node properties. Operations related to data constraints are used to handle validation requirements in semantic declarations.

For example, an integer value can be validated through the semantic type:

```csharp
var runtimeType =
    await schemaContext.GetNodeTypeAsync("system.int");

var intNode =
    await runtimeType!.ValidateValueAsync(schemaContext, value);
```

The validation result can be determined through `IsValid`, and the violated constraints can be obtained through `GetViolatedConstraints`.

Therefore, a data node can be understood as **a JSON-style data entity carrying a semantic type and property capabilities**. It does not merely store a value; it can also perform the corresponding access and validation operations according to the associated semantic declarations.

A data node does not require all semantic properties to be expanded immediately. The backend usually processes relevant properties only when needed, to control the runtime cost of requests; while in environments such as the frontend that need to hold data entities for a long time, properties and relations can continue to be expanded, forming a more complete semantic space in memory.

## 4. Definition-Domain Semantic Types

In addition to types that directly process data, Core also provides auxiliary types for defining and operating semantic declarations. They are mainly located in the `system.schema` definition domain, used to describe semantic types, access type information, and support related configuration operations.

This part of the implementation is mainly declarative code, and this chapter will not expand on it one by one. It is usually easier to understand their role by observing actual nodes together with the configuration interface, and then returning to the corresponding type definitions to read the code.

For example, after enabling Debug mode on the configuration page, you can view the semantic type actually used by a node. The `system.schema.namespace.type` used by the namespace selection node can serve as an entry point for further reading of the source code.

## Conclusion

At this point, the introduction of SchemaNode.Core's core concepts and runtime mechanisms is complete.

Core provides public prototypes, the semantic type loading mechanism, and the auxiliary facilities needed to define and construct semantic types based on these prototypes. It does not limit a specific business domain, but provides a common semantic foundation for different dialects built on C#.

On this foundation, dialects can declare their own semantic types and add specific execution capabilities to them. The subsequent App dialect will further demonstrate that once data structures, properties, functions, and relations are expressed by the public semantic mechanism, the business layer can compose more concrete application capabilities on top of them.

From an implementation perspective, Core's main mechanisms do not depend on language features unique to C#; apart from implementation details such as functions ultimately being compiled into `Delegate`, the other core concepts can all be mapped to other languages. This also allows the existing implementation to serve as a reference for building semantic runtimes in other languages.

SchemaNode.Core is the language foundation of this system. It is responsible for establishing the basic contract between semantic definitions and the runtime, while the semantic accumulation and execution capabilities of specific domains continue to be completed by the dialects built on top of it.
