# SchemaNode.Core — Platform Overview

SchemaNode.Core is a **language-agnostic, self-describing, extensible metadata platform kernel** built on four pillars: **Meta**, **Property**, **Relation**, and **Function**.

## What It Is

- A **unified model** for data types, operations (functions), and data associations (relations) in one meta-model
- A **self-describing platform** where Property definitions are themselves managed as schema objects
- An **extensible architecture** where third parties define new schema families, properties, and compile strategies
- An **executable runtime** resolving, validating, and compiling schemas into code

## What It Is Not

- ❌ Another JSON Schema format
- ❌ A low-code platform
- ❌ A database ORM
- ❌ A closed framework

## The Four Pillars

| Pillar | Question | Implementation |
|--------|----------|----------------|
| **Meta** | What *is* this schema kind? | `[Meta<T>]` C# attributes |
| **Property** | How does data *behave*? | `Property<T>` composable annotations |
| **Relation** | How does data *relate*? | `IRelationProcess` dynamic rules |
| **Function** | How does computation *work*? | Pure functional expressions → compiled delegates |

## Key Concepts

### Meta — Schema Kind Identity
Define schema kinds via `[Meta<T>]` attributes. For example, `StructSchema` declares its identity through `[Meta<SchemaKind>("struct")]`, `[Meta<NodeType>(typeof(RuntimeStructType))]`, and `[Meta<SchemaGenerator>(typeof(StructGenerator))]`. The Node Schema Family includes 16 built-in kinds covering scalars, enums, structs, arrays, functions, properties, and relations.

### Property — Composable Annotations
Properties extend schema behavior: constraints (`Require`, `UpLimit`), display (`Visible`, `Unit`), and system (`SchemaType`, `OverrideType`). Special properties include `IConstraintProperty` for validation and `ITypeRefProperty` for referential integrity. Properties are stackable (combinable) or overriding.

### Relation — Dynamic Data Association
`Assign` forces values; `Call` computes values via functions (e.g., `lookup_manager($store_id)`). `OverrideType` enables runtime type polymorphism. Custom `IRelationProcess` implementations are supported.

### Function — Semantic Expression Engine
**Atomic functions** are C# methods registered as schema functions. **Semantic functions** are pure expression compositions — no variables, no loops. The **CompileContext** system compiles one function into multiple targets: in-memory validation, database query filters, GraphQL expressions, etc. This is the **unique truth** principle.

## The Node Schema Family
The universal carrier ensuring data types, operations, definitions, and associations share the same self-describing infrastructure. Ship a `StructSchema` alongside a `FunctionSchema` — receivers understand both because their definitions are also node schemas.

## Runtime
- `SchemaRuntime`: Global registry for kinds, types, system schemas
- `SchemaContext`: Scoped resolution with System → Service → Remote provider chain
- `NodeType`/`ValueType`: Executable runtime types with validation, compatibility, references
- `DataNode`: Runtime data carrying schema reference and violation state

## Extensibility (Four Surfaces)
1. **Schema Families** — New kinds, generators, runtime types (Platform Architects)
2. **Properties** — New `Property<T>` subclasses (Solution Architects)
3. **Function Libraries** — C# static classes as atomic functions (Developers)
4. **CompileContexts** — Custom compilation strategies (Solution Architects)

## Cross-Language
- TypeScript frontend shares the same meta-model
- JSON payloads are self-describing
- MCP enables AI agents to understand schema semantics
- `Meta` registers legacy systems as SchemaNode types/functions

## Further Reading
- [PROJECT_HISTORY.en.md](./PROJECT_HISTORY.en.md) — Why SchemaNode exists (3-version evolution story)
- [PLATFORM_ARCHITECTURE.en.md](./PLATFORM_ARCHITECTURE.en.md) — Deep architectural dive with code examples
- [FEATURE_GUIDE.en.md](./FEATURE_GUIDE.en.md) — Practical usage guide
