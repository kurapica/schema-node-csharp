# SchemaNode Platform Overview

> A concise external-facing summary suitable for repository front pages, architecture reviews, ecosystem outreach, and partner conversations.

## One-Sentence Description

**SchemaNode is a language-agnostic, self-describing, extensible metadata platform kernel centered on `Property` composition.**

It does more than describe data structures. It provides one model for:

- data types
- operations and functions
- data association and dynamic rules
- extensible metadata such as UI, validation, layout, and behavior

---

## What It Is Not

SchemaNode is not:

- another JSON-only schema file format;
- a small utility library limited to form configuration or field validation;
- a closed framework whose capabilities can only be expanded by the core team.

In SchemaNode, JSON is only one expression format.

`SchemaNode.Core` is focused on the **meta-model, extension mechanism, runtime, and cross-language semantics**. More application-facing JSON-Schema-style expressions are expected to evolve in `SchemaNode.App`.

---

## Its Three Core Values

### 1. Schemas are defined through property composition

SchemaNode does not scale by endlessly adding fixed schema fields. It scales by composing semantics through `Property<T>`.

That gives the platform a better evolution path:

- the core structure stays disciplined;
- new capabilities grow through properties;
- third parties can add schema properties without changing the core model.

### 2. Properties themselves enter the metadata system

In SchemaNode, properties themselves are registered as `PropertySchema`.

That means the platform manages not only business metadata, but also the **definitions of metadata themselves**, making it a self-describing platform.

### 3. It provides one shared model for types, operations, and association

SchemaNode gives the ecosystem a shared substrate:

- `Scalar / Enum / Struct / Array`: a shared type model;
- `FunctionSchema`: a shared operation language and execution model;
- `RelationSchema`: a shared model for property linkage and dynamic rules.

That means future third-party schema families do not need to reinvent their own type system and rule system.

---

## Core Abstractions

```text
Property<T>
   ↓
ExtensibleSchema
   ↓
NodeSchema Family
(Scalar / Enum / Struct / Array / Function / Property / Relation)
   ↓
SchemaRuntime / SchemaContext
   ↓
NodeType / ValueType
   ↓
DataNode / FunctionType / Relation runtime behavior
```

---

## Why the Platform Is Worth Attention

SchemaNode is attractive to senior architects and ecosystem contributors because:

- it opens two extension axes: **schema families** and **schema properties**;
- it is self-describing, which helps governance, registration, and discovery;
- it includes an executable runtime, not only a static model;
- it is naturally suited for cross-language, multi-endpoint, and plugin-based evolution;
- its ambition is platform-level, not just tool-level.

---

## Current Layering

- `SchemaNode.Core`
  - defines the meta-model
  - defines the property-composition mechanism
  - provides the C# reference implementation
  - provides runtime loading and execution
- `SchemaNode.App`
  - hosts more application-facing JSON-Schema-style expressions
  - hosts app-level workflows, UI protocols, and assembly logic
- future TypeScript Core
  - shares the same meta-model and interpretation logic on the frontend side

---

## Four Important Additional Points

### 1. Kinds are code-defined; schemas are largely dynamic

In SchemaNode, `Schema Kind` is the semantic category defined and registered in system code, although third-party code can still extend additional kinds.

The schemas actually used at runtime, however, can come from two sources and be merged together:

- `system schema` abstracted from code and `Meta`
- dynamic schema loaded from databases, central schema services, and other remote sources

In real platform scenarios, the large majority of executed schemas are expected to be dynamically defined, dynamically loaded, dynamically updated, and even dynamically unloaded. That is the foundation for frontend-driven configuration, centralized management, and live runtime behavior.

### 2. Legacy systems can be onboarded semantically

Through `Meta` descriptions, types and APIs from existing systems can be registered as SchemaNode data types and functions, which means as part of the system schema.

That means:

- legacy types can enter the shared type system semantically;
- legacy APIs can enter the shared function system semantically;
- microservices or systems implemented in other languages only need a registration/adapter layer to join the platform, rather than a full rewrite.

### 3. Schemas are naturally suitable for AI understanding and runtime applications

SchemaNode schemas are semantic objects, so they can be exported further into:

- MCP capability descriptions;
- JSON-Schema-style structures;
- ontology-based semantic structures.

That allows AI to understand, customize, and modify models at the semantic layer, and even generate temporary runtime applications that can be destroyed after execution. At the same time, the application itself and the execution data can be stored together as logs, creating an auditable and replayable AI-driven application process.

### 4. Unified multi-end configuration reduces coordination and maintenance cost

With one unified meta-model, frontend, backend, workflow, and configuration layers can collaborate around the same semantic core.

The practical benefits are:

- lower communication cost;
- fewer fragmented interfaces;
- the ability to orchestrate most business behavior with a relatively small API surface even in complex microservice environments;
- easier maintenance and secondary development than typical low-code platforms.

---

## External Summary

If SchemaNode has to be introduced externally in one sentence:

> SchemaNode is building a cross-language metadata platform that defines and extends schemas through `Property` composition, becomes self-describing through `PropertySchema`, provides a shared operation language through `FunctionSchema`, enables dynamic linkage through `RelationSchema`, and opens both schema families and schema properties as ecosystem extension axes.

