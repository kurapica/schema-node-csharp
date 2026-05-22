# SchemaNode Platform Architecture

> This document is written for platform architects, framework designers, domain-modeling leaders, and potential ecosystem contributors. It explains the design philosophy, platform direction, and extension model behind `SchemaNode`. While `SchemaNode.Core` is the current primary C# reference implementation, the focus of this document is not a specific API surface, but SchemaNode as a **community-oriented metadata platform**.

## 1. What We Are Building

`SchemaNode` is not trying to become another “more convenient schema file format”.

What it is building is:

- a **language-agnostic** metadata declaration system;
- a **self-describing** metadata management platform;
- an **extensible** platform for schema types and schema properties;
- an **executable runtime** for data types, operations, and associations;
- a shared kernel that can serve backend, frontend, platform, and application layers together.

From a platform perspective, SchemaNode is best understood as:

> a metadata language kernel centered on `Property` composition, together with the open ecosystem that can grow around that kernel.

`SchemaNode.Core` is the current C# implementation of that kernel. A future TypeScript implementation is expected to play the same role on the frontend side, giving the ecosystem a consistent cross-language meta-model.

---

## 2. Why This Is Not “Another JSON Schema”

When teams face metadata problems, a common first step is to create a JSON-based schema format and then attach validation, UI, workflow, and business semantics around it.

SchemaNode deliberately takes the opposite route.

The hard problem is not primarily “how to represent structure in JSON”. The hard problem is how to unify:

1. **data types**;
2. **operations and functions**;
3. **data association and dynamic rules**;
4. **UI, validation, behavior, recognition, and layout semantics** within one meta-model;
5. **third-party extensibility**, so those capabilities are not permanently hard-coded in the core.

Because of that, JSON is only an expression and interchange format in SchemaNode, not the architectural center.

In particular:

> **JSON-Schema-style application expressions are expected to live mainly in `SchemaNode.App`, not at the center of `SchemaNode.Core`.**

The responsibility of Core is the abstract model and runtime mechanics of the metadata platform itself.

---

## 3. The Central Design Thesis

The design thesis of SchemaNode can be summarized in one sentence:

> **The core of a schema system should not be a fixed set of fields; it should be a mechanism for composition through `Property`.**

That means:

- schema types can be extended;
- schema properties can also be extended;
- third parties can register not only “new data models”, but also “language components used to define data models”;
- metadata itself can become a managed platform object that is discoverable, understandable, composable, and evolvable.

That is the reason SchemaNode can grow from a developer library into a real platform.

---

## 4. First Core: Defining Schemas Through Property Composition

### 4.1 Properties are not side labels; they are the building blocks

In SchemaNode, `Property<T>` is not a convenience annotation layered on top of a fixed schema model. It is the primary capability unit of the platform.

A property may describe:

- type constraints;
- default values;
- required semantics;
- UI presentation;
- layout semantics;
- primary and index semantics;
- compile hints;
- behavior semantics;
- domain-specific extension rules.

So schema meaning is not hard-coded into class fields. It is assembled progressively through property composition.

### 4.2 Property composition scales better than fixed-field enumeration

Traditional schema systems often evolve by adding more and more built-in fields:

- today `required`
- tomorrow `displayName`
- then `ui`
- then `layout`
- then `validator`

That approach has predictable problems:

- the core model grows heavier over time;
- many capabilities remain effectively owned by the framework author;
- third-party extensions either need to modify the core or escape into side-channel configuration;
- different extensions have no unified dependency or override model.

SchemaNode takes a different route:

- keep the core schema structure as stable as possible;
- move extensibility into the property system;
- use `Depend`, `Override`, `ForSchema`, and `ForType` to describe relationships among properties and between properties and schemas;
- let the runtime interpret those composed semantics uniformly.

This is the foundation of the SchemaNode open meta-model.

---

## 5. Second Core: Schema Properties Are Platform Objects Themselves

If “defining schemas through properties” solves the problem of expressive power, then “bringing properties themselves into the schema system” solves the problem of self-description.

In SchemaNode, properties themselves are registered as `PropertySchema`.

That means the system can understand:

- the name of a property;
- its value type;
- which schema kinds it applies to;
- which value types it applies to;
- which properties it depends on;
- which properties it overrides;
- whether it is static or runtime-participating.

This turns SchemaNode into:

> **a self-describing platform that manages not only business metadata, but also the definitions of metadata themselves.**

That is essential for ecosystem growth, because contributors are no longer limited to “using metadata”; they can also contribute new **metadata language capabilities**.

---

## 6. The Real Role of the `NodeSchema` Family

Within this architecture, the `NodeSchema` family remains important, but its role must be stated precisely:

> the `NodeSchema` family is not the only center of the platform; it is the **carrier layer of the core metadata domain model**.

That carrier layer holds four categories of platform objects:

1. **data types**
2. **operations**
3. **property definitions**
4. **data associations**

### 6.1 Data type families

These schema families make up the shared data type system:

- `ScalarSchema`
- `EnumSchema`
- `StructSchema`
- `ArraySchema`

They provide the common semantic base on top of which future schema families can build.

### 6.2 The operation family

`FunctionSchema` provides the shared operation model.

It connects not only function signatures, but also:

- the expression DSL;
- the compiler;
- generic inference;
- system functions;
- remote functions;
- multi-target execution paths.

### 6.3 The property-definition family

`PropertySchema` formally brings properties into the metadata system itself.

### 6.4 The data-association family

`RelationSchema` gives registered properties dynamic linkage and mutable behavior.

So, from the platform perspective:

- `Property` is the center of extensibility;
- the `NodeSchema` family is the core metadata domain-model layer;
- the platform emerges from the combination of both.

---

## 7. Third Core: A Shared Model for Types, Operations, and Association

SchemaNode is not only solving “type description”.

It is attempting to provide a common substrate for all future third-party schema families:

### 7.1 A shared type model

`Scalar / Enum / Struct / Array` together define:

- what a value is;
- how it is validated;
- how it is accessed;
- how it is converted;
- how it is serialized;
- how it composes into larger structures.

### 7.2 A shared operation model

`FunctionSchema` and the compilation/execution pipeline define:

- how operations are described;
- how arguments are inferred;
- how expressions are compiled;
- how system, custom, and remote functions are executed;
- how different schema families can share one operation language.

### 7.3 A shared association model

`RelationSchema` and relation processes define:

- how properties are linked;
- how property values are computed dynamically;
- how validation, display, defaulting, and other behaviors are connected at runtime;
- how extended schema families can reuse one dynamic-rule mechanism.

Together, these three parts ensure one crucial thing:

> **future schema families extended through DLLs, remote registration, or frontend registration do not need to reinvent their own type system, operation system, or rule system.**

---

## 8. An Extension Model Designed for Ecosystem Growth

SchemaNode is not designed as a closed system.

Its architecture deliberately exposes multiple open extension surfaces:

### 8.1 New schema families

Third parties can define new schema kinds and register:

- their schema definitions;
- their runtime types;
- their generators;
- their providers;
- their applicable property sets.

### 8.2 New schema properties

Third parties can add to existing schema families:

- UI properties;
- recognition properties;
- approval properties;
- layout properties;
- validation properties;
- behavior properties;
- any vertically specialized metadata needed by a domain.

### 8.3 New function and compilation capabilities

Third parties can introduce:

- new system-function namespaces;
- new expression visitors;
- new compile strategies;
- new remote execution endpoints.

### 8.4 New relation executors

Third parties can introduce their own relation processes, allowing property linkage to grow from constraint-oriented behavior into broader dynamic semantics.

The goal of this extension model is straightforward:

> **the platform should gain value as participation grows, without becoming unmanageable as the core becomes more complex.**

### 8.5 Schema Kinds, Schema Sources, and Runtime Lifecycle

SchemaNode needs to distinguish two concepts that are easy to conflate:

- **Schema Kind**: the code-level category registered by the system and used by the runtime to recognize a schema family;
- **Schema instance**: a concrete data type, function, property definition, or relation definition that can come from multiple sources.

In other words, kinds are the semantic skeleton of the platform, while schemas are the concrete runtime content loaded into that skeleton. In the current design:

1. `Schema Kind` is defined and registered by system code by default;
2. but third-party code can still extend new kinds, their property sets, runtime types, and generators;
3. concrete schemas can come from both:
   - **system schema** abstracted from code through reflection and `Meta`;
   - **dynamic schema** loaded from databases, central schema-management services, or other remote sources;
4. system schema and remote schema can be merged together in the runtime.

This means SchemaNode is not a system where all schemas are permanently hard-coded. In real platform deployments, **the large majority of actually executed schemas are expected to be dynamically defined**, usually configured through frontend tools, centrally managed services, or runtime distribution.

At the runtime level, SchemaNode is designed to support:

- real-time loading;
- real-time updates;
- real-time unloading;
- on-demand merging of system schema and dynamic schema;
- provider-driven remote fetch and partial rebuild.

That is one of the prerequisites for making “configuration as model, and model as runtime” practical.

---

## 9. Core Abstraction Relationship Diagram

The following diagram is not intended as UML. It is an architecture-discussion view of how the core abstractions of SchemaNode fit together:

```text
┌──────────────────────────────────────────────────────────────────────┐
│                         SchemaNode Platform                         │
├──────────────────────────────────────────────────────────────────────┤
│  Extension Axis A: Schema Families                                  │
│  Extension Axis B: Schema Properties                                │
└──────────────────────────────────────────────────────────────────────┘

				declares / constrains / augments
┌──────────────────────┐    attaches to    ┌──────────────────────────┐
│      Property<T>     │ ─────────────────▶│     ExtensibleSchema     │
│  IProperty metadata  │                   │  extension data carrier  │
└──────────────────────┘                   └─────────────┬────────────┘
														 │
														 │ materializes as
														 ▼
										┌──────────────────────────────────┐
										│         NodeSchema Family        │
										│ Scalar / Enum / Struct / Array  │
										│ Function / Property / Relation  │
										└────────────────┬─────────────────┘
														 │
								registered by            │ loaded by
														 ▼
						   ┌──────────────────────────────────────────────┐
						   │       SchemaRuntime / SchemaContext          │
						   │ kind registry / property registry / merge    │
						   └────────────────┬─────────────────────────────┘
											│
											│ interpreted as executable model
											▼
						   ┌──────────────────────────────────────────────┐
						   │            NodeType / ValueType              │
						   │ compatibility / validation / references      │
						   └───────────────┬───────────────┬──────────────┘
										   │               │
										   │ creates       │ executes
										   ▼               ▼
						   ┌──────────────────────┐   ┌───────────────────┐
						   │       DataNode       │   │   FunctionType /  │
						   │ runtime value model  │   │  CompileContext   │
						   └──────────┬───────────┘   └─────────┬─────────┘
									  │                         │
									  │ linked dynamically by   │
									  └──────────┬──────────────┘
												 ▼
									  ┌─────────────────────────┐
									  │      RelationSchema     │
									  │ dynamic property rules  │
									  └─────────────────────────┘
```

This diagram highlights five key facts:

1. `Property<T>` is the extension starting point, not a secondary annotation layer;
2. `ExtensibleSchema` is the shared carrier for property composition;
3. the `NodeSchema` family carries the core metadata domain objects;
4. `SchemaRuntime`, `SchemaContext`, and `NodeType` turn metadata into executable runtime semantics;
5. `FunctionSchema` and `RelationSchema` make the platform capable not only of description, but also of operation and dynamic linkage.

---

## 10. Why the Runtime Matters

Without a runtime, SchemaNode would still be only an abstract meta-model.

One of the most important contributions of `SchemaNode.Core` is that it turns the meta-model into an executable system.

### 9.1 `SchemaRuntime`

Maintains globally:

- schema-kind registration;
- property registration;
- kind-to-runtime-type mappings;
- system schemas;
- array-schema caches;
- CLR correspondences.

### 9.2 `SchemaContext`

Handles:

- on-demand loading;
- generic resolution;
- provider merging;
- context objects;
- scoped caching.

### 9.3 `NodeType` / `ValueType`

Translate composed property semantics into runtime behavior, including:

- type compatibility;
- value creation;
- value validation;
- reference tracking;
- converter functions;
- array association.

### 9.4 `DataNode`

Carries real runtime data, making schemas not only definable, but executable.

This is what gives SchemaNode platform-level capability in:

- description
- loading
- inference
- validation
- execution
- linkage

rather than merely exporting a descriptive file.

---

## 11. Cross-Language Strategy and Layer Boundaries

If SchemaNode is going to become a platform, its layer boundaries must remain clear.

### 10.1 The responsibility of `SchemaNode.Core`

`SchemaNode.Core` is responsible for:

- defining the meta-model;
- defining the property-composition mechanism;
- defining the runtime loading and execution model;
- providing the C# reference implementation;
- serving as the semantic baseline for implementations in other languages.

### 10.2 The responsibility of `SchemaNode.App`

`SchemaNode.App` is the more appropriate place for:

- JSON-Schema-style expressions;
- app-level workflow semantics;
- UI and interaction protocols closer to products;
- application-layer composition logic.

### 10.3 Why a TypeScript frontend core matters

The goal of the frontend implementation is not simply to parse JSON emitted by the backend. The goal is to:

- share the same meta-model;
- share the same property interpretation logic;
- share the same schema-family extension semantics;
- let frontend and backend truly collaborate around one metadata core.

That is one of the strongest indicators that SchemaNode is designed as a platform rather than a single-language library.

### 10.4 Semantic onboarding of legacy systems

One of the most important platform values of SchemaNode is that `Meta` descriptions can be used to register existing:

- types;
- APIs;
- microservice endpoints;
- and even capabilities implemented in other languages;

as SchemaNode data types and functions, which means as part of the **system schema**.

This allows the platform to perform a semantic translation layer over legacy systems:

- legacy types can be registered as schema data types;
- legacy APIs can be declared as system-function calls behind `FunctionSchema`;
- microservice endpoints can be brought into the schema operation model through an adapter layer;
- systems written in other languages can still join the same semantic model as long as there is an invocable registration/adaptation layer.

The strategic significance is this:

> **SchemaNode does not require enterprises to rewrite legacy systems before they can participate. It allows them to be semantically registered first and unified gradually.**

That makes the platform much more realistic for large organizations with deep existing system investments.

### 10.5 AI-oriented semantic output and temporary applications

Schemas in SchemaNode are semantic objects by nature, not just internal structures tied to a single runtime.

Because of that, they can be exported or mapped into AI-friendly structures such as:

- MCP capability descriptions;
- JSON-Schema-style structures;
- ontology / knowledge-graph semantics;
- domain-specific semantic context for specialized models.

This creates several important possibilities:

1. AI can understand system capability through schema semantics instead of fragmented API descriptions alone;
2. AI can customize or modify models at the semantic layer instead of editing business code directly;
3. AI can generate temporary applications, temporary models, or temporary flows at runtime and destroy them after execution;
4. the application itself is also data, so “the application used during execution + the input/output data” can be preserved together as logs;
5. the platform can therefore connect legacy API platforms with AI-driven semantic programming.

One major benefit of this design is that it reduces the regression risk of letting AI directly rewrite code.

Instead of asking AI to repeatedly generate, patch, and overwrite source code, SchemaNode provides a better boundary where AI can operate at the:

- semantic model layer;
- application composition layer;
- temporary runtime layer;
- auditable and replayable metadata execution layer.

That creates a more stable engineering model for AI participation in enterprise systems.

### 10.6 Unified multi-end configuration and API consolidation

The unified meta-model of SchemaNode also has a very practical benefit: **integrated multi-end configuration**.

In many business systems today, backend, frontend, workflow, form, and reporting layers repeatedly remodel the same concepts and pay the coordination cost each time. SchemaNode aims to converge those concerns onto one semantic core, resulting in:

- lower communication cost during development;
- lower alignment cost between frontend and backend;
- more centralized service-interface semantics;
- simpler maintenance and secondary development.

In the ideal platform shape, even if the underlying landscape is a complex microservice topology, the upper layer does not necessarily need hundreds of exposed APIs. A relatively small set of stable semantic APIs plus dynamic schema can orchestrate most business behavior.

That gives the platform a valuable combination:

1. the abstraction and rapid assembly benefits often associated with low-code platforms;
2. without the usual low-code limitation that deep customization and secondary development become difficult.

---

## 12. Why This Is Interesting to Senior Architects

Senior architects rarely care only about whether “a library is useful today”. They care about:

- whether the abstraction boundaries are clear;
- whether the system can evolve over time;
- whether its extension surfaces are stable;
- whether it supports ecosystem collaboration;
- whether it can serve multiple teams, products, and endpoints.

That is exactly where SchemaNode becomes compelling:

1. **it elevates metadata from a local tooling concern to a platform-level abstraction;**
2. **it opens two extension axes at once: schema families and schema properties;**
3. **it is self-describing, which improves governance, registration, collaboration, and discovery;**
4. **it has a runtime, not only a static description model;**
5. **it is naturally suited for cross-language, multi-endpoint, plugin-based evolution.**

That is also why SchemaNode documentation must be presented as platform architecture, not just as internal library notes.

---

## 13. External Architectural Conclusions

From an external platform perspective, SchemaNode can be summarized as follows:

1. **SchemaNode is a language-agnostic metadata platform kernel, not a single-format schema tool.**
2. **Its first core is the mechanism of defining schemas through `Property` composition.**
3. **Its second core is that properties themselves are brought into schema management, making the platform self-describing.**
4. **The `NodeSchema` family carries data types, operations, property definitions, and data associations as the domain-model layer.**
5. **`FunctionSchema` and the compiler pipeline give the platform a shared operation language.**
6. **`RelationSchema` gives registered properties dynamic linkage behavior.**
7. **Platform extension is achieved primarily by opening new schema families and schema properties, not by endlessly adding fixed core fields.**
8. **`SchemaNode.Core` is the current C# reference implementation, and together with a future TypeScript core it forms the basis of a cross-language ecosystem.**

---

## 14. Summary

In one sentence, the platform value of SchemaNode is this:

> `SchemaNode` is building a cross-language metadata platform centered on `Property` composition, made self-describing through `PropertySchema`, operational through `FunctionSchema`, dynamically connected through `RelationSchema`, and continuously extensible through third-party schema families and schema properties.

The significance of this architecture is not merely that developers can “write less configuration”. It is that it creates a sustainable foundation for:

- community collaboration
- plugin ecosystems
- shared meta-models across multiple endpoints
- platform-level governance
- vertical domain language extensions

all on top of one evolving core.
