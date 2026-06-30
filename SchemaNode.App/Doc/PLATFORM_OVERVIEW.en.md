# SchemaNode.App — Platform Overview

SchemaNode.App is a **business data management application generation and execution platform** built on SchemaNode.Core's four-pillar architecture (Meta + Property + Relation + Function).

## What It Is

- An **application platform** that turns schema definitions into running applications with data management
- An **event-driven workflow engine** with DAG orchestration and fork support
- A **fine-grained auth system** covering schema, app, row, and column levels
- A **multi-context compilation system** that derives database queries, data validators, and push pipelines from single function definitions

## What It Is Not

- ❌ A low-code drag-and-drop builder (configuration is schema-driven, not visual)
- ❌ A traditional API-centric CRUD framework (APIs are generic, ~40 total)
- ❌ A standalone system (requires SchemaNode.Core)

## Core Systems

### App Schema Family
Defines the application model:
- **App**: Container with sub-apps, fields, and workflows — tree-structured namespace
- **AppField**: Maps to a database table with value type, source/push, auth policies
- **AppWorkflow**: DAG of workflow nodes (WaitEvent, Call, Interaction, Control flow)

### Event System
Topic-based pub/sub with wildcard matching (`+` single-level, `*`/`#` multi-level). Built on Reactive Extensions with Kafka/RabbitMQ source support. Events include:
- **App Events**: Data create/update/delete per app field
- **Schema Events**: Schema create/delete/change, app schema create/delete/change

### Workflow System
DAG-based orchestration with:
- **WaitEvent**: Subscribes to events, supports fork (stay alive for next event) or one-shot
- **Call**: Executes schema functions
- **Interaction**: Waits for external API interaction
- **Control**: Break, Exit, Goto, Delay, TimeSchedule (Quartz.NET)
- Fork with dedup keys, cancel-previous semantics, payload persistence

### Auth System (Three-Level)
- **Auths** (Schema/App/Workflow level): Policy-based with evaluator functions
- **RowAuths** (AppField row level): Filter functions returning bool per row
- **ColAuths** (AppField column level): Column-level evaluator functions

### CompileContext System
The "unique truth" principle in action:
- **QueryFilterCompileContext**: Compiles `(StructType) → bool` functions into `AppSchemaDataFilter` query trees for database filtering. The same function validates data on submission AND filters data on retrieval.
- **DataPushCompileContext**: Compiles push functions, extracts third-field dependencies, optimizes batched queries.

### Dynamic Tables & Data Management
- Value types determine database table structure
- Generic CRUD via ~40 APIs (no per-table endpoints)
- Push & Source: source field changes → push function → target field update
- DisplayOnly + Relation: cross-table field lookups via primary key relations

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                   SchemaNode.App                         │
│                                                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐ │
│  │   App    │  │  Event   │  │ Workflow │  │  Auth   │ │
│  │  Family  │  │  System  │  │  Engine  │  │ System  │ │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬────┘ │
│       │             │             │             │       │
│  ┌────┴─────────────┴─────────────┴─────────────┴────┐  │
│  │              CompileContext Layer                  │  │
│  │  QueryFilterCompile / DataPushCompile / ...       │  │
│  └──────────────────────┬────────────────────────────┘  │
│                         │                               │
│  ┌──────────────────────┴────────────────────────────┐  │
│  │          Dynamic Table / Data Provider            │  │
│  │         Generic CRUD (~40 APIs total)             │  │
│  └──────────────────────┬────────────────────────────┘  │
└─────────────────────────┼───────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────┐
│                  SchemaNode.Core                         │
│         Meta + Property + Relation + Function            │
└─────────────────────────────────────────────────────────┘
```

## Further Reading
- [PLATFORM_ARCHITECTURE.en.md](./PLATFORM_ARCHITECTURE.en.md) — Deep architecture dive
- [FEATURE_GUIDE.en.md](./FEATURE_GUIDE.en.md) — Practical usage guide
- [Core PROJECT_HISTORY](../SchemaNode.Core/Doc/PROJECT_HISTORY.en.md) — Why this approach (700 tables, 3 people, 3 months)
