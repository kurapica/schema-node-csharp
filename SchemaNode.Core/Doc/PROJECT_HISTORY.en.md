# SchemaNode Project History

> How a failed HVAC quotation system led to a metadata-driven platform that compresses 100 person-months of work into 9.

---

## Prologue: The HVAC Quotation Disaster (~2019)

The origins of SchemaNode trace back to a seemingly straightforward project in Shenzhen: converting a quotation system built on **5,000 Excel spreadsheets** into a web service. The client needed per-project quotation data delivered through a browser interface.

The technical approach seemed reasonable at the time:
- **Schema** definitions for data structures
- **Lua** as the embedded scripting language for business logic and data transformations

The system was completed, but the result was a **disaster**:
- Lua scripts grew into an unmanageable tangle of imperative logic spread across thousands of configuration points
- Debugging a single data issue required tracing through layers of dynamic script execution with no static analysis support
- The team could not confidently make changes without risking cascading failures
- **The system never went live.**

### Lessons Learned

1. **Turing-complete scripting languages are the wrong tool for schema-level business logic.** They are too powerful to analyze, too flexible to constrain, and too opaque to audit.
2. **A schema system must be semantically analyzable.** Without the ability to understand what a piece of configuration *means* without executing it, maintenance becomes impossible.
3. **One truth, many interpretations.** The same business rule should be compilable into different execution forms (validation, filtering, transformation) without rewriting.

---

## Version 1: Datanode — Agricultural Carbon Sink Accounting (2021)

### The Challenge

In 2021, a new project emerged: an **agricultural carbon sink accounting system**. The constraints were extreme:

| Constraint | Detail |
|------------|--------|
| Team size | **3 people** (1 frontend, 1 backend, 1 architect/modeler) |
| Data tables | ~700 database tables in the first phase |
| API surface | Traditional approach would need ~3,000 APIs, conservatively ~2,000 even with unified parameter tables |
| Data complexity | Cross-table dependencies everywhere: crop selection limits fertilizers, fertilizers carry parameters from farm associations, and a single input form could have 100+ data associations |
| Timeline | 3 months for first delivery |

A traditional development approach was mathematically impossible. Even with parameter tables unified, roughly 500 business tables remained — each requiring CRUD APIs (4 per table = 2,000 APIs), plus cross-table data association logic.

### The Solution: Datanode

The first version of what would become SchemaNode was built. It contained only four core modules:

1. **Data Structure Definition** — Declare data types (structs, enums, scalars) and their fields
2. **Data Relation Definition** — Define how data fields relate to each other, including cross-table lookups
3. **Function Definition** — Declare pure functions with arguments and return types for data transformation
4. **App & App Field** — Map data structures to application fields with display and storage configuration

### The Development Model

These four modules enabled a revolutionary development paradigm: **three-line parallel development with zero communication cost**.

```
Frontend Developer          Schema Modeler            Backend Developer
─────────────────         ─────────────────         ─────────────────
Layout & steps             Data structures           Project management
UI components              Data relations            Microservices
Form navigation            Functions                 ~40 APIs total
                           App/Field config          Infrastructure
```

Key characteristics:
- **Frontend and backend developers did not need to understand the business data model.** They worked on their respective layers independently.
- **Schema modelers defined the data structures, relations, and functions** — this was the sole source of business logic truth. The frontend auto-rendered forms, validated data, and handled field linkage based on the schema; the backend auto-maintained table structures and handled CRUD and data push automatically.
- **No API development for individual tables.** The system's ~40 generic APIs handled all CRUD operations dynamically based on schema configuration.
- **Zero bugs at the business logic level.** Since the schema model was declarative and the runtime was generic, there was no hand-written business code to introduce bugs.

### Results

| Metric | Traditional Approach | Datanode Approach |
|--------|---------------------|-------------------|
| APIs needed | ~2,000 | ~40 |
| Development effort | ~80–150 person-months | ~9 person-months |
| New核算 standard deployment | Months | **1 week (including testing)** |
| Production hotfixes | Redeploy required | **Online configuration change** |
| Frontend/Backend Collaboration | API contract-driven | **Schema-driven** |

In a traditional enterprise development model, a system of this scale would typically require **80–150 person-months** for the first phase. Even today, with AI auto-generating CRUD, APIs, and basic pages, the business complexity — cross-table relations, dynamic forms, and data linkage — still demands manual design and verification, putting the effort at roughly **40–70 person-months**.

With the schema-driven data model and Datanode v1, we delivered the first phase in **9 person-months** (3 people × 3 months). This effort included not only the agricultural carbon sink accounting system but also the design and implementation of Datanode's own frontend and backend frameworks — achieving roughly an order-of-magnitude reduction in human effort.

After the first phase, each additional carbon sink accounting standard added approximately 70–80 tables. The entire cycle — modeling, configuration, frontend adaptation, and testing — completed **within one week**, without requiring a new release.

Notably, Datanode v1's design focus was not the backend — it was the frontend.

What truly consumed development time in the project was not CRUD, but **complex data associations**: a single form often needed to link dozens or even hundreds of data sources, with cascading constraints, dynamic filtering, and conditional visibility across fields. As a result, Datanode v1's core capabilities were almost entirely built around **dynamic forms**: data source binding, field linkage, expression evaluation, and a unified data access model.

In version 2, as cross-table query requirements grew, the `Relation` system began to take on fuller semantic description capabilities, participating in backend query resolution — for example, supporting `DisplayOnly` associative queries. This allowed data relations described in the schema to drive both frontend display and backend queries, rather than being merely a UI-layer configuration.

---

## Version 2: SchemaNode — Low-Code Platform Replacement (Mid-2025)

### Motivation

By 2025, the system had proven itself in production for four years. The next challenge: **replace a commercial low-code platform** with a more capable, more extensible foundation.

This required capabilities far beyond version 1's scope:
- **Authentication & Authorization** — Fine-grained access control at schema, app, row, and column levels
- **Event System** — Publish/subscribe for data changes, schema changes, and custom events
- **Workflow Engine** — DAG-based workflow orchestration with fork support
- **App Workflow** — Per-application workflow definitions integrated into the schema model

### Key Additions

| System | Description |
|--------|-------------|
| **Auth** | Policy-based auth with evaluator functions; supports `Auths` (general), `RowAuths` (row-level filtering), `ColAuths` (column-level filtering) |
| **Event** | Topic-based pub/sub with wildcard matching (`+` single, `*`/`#` multi); Reactive Extensions backend; Kafka/RabbitMQ source support |
| **Workflow** | DAG nodes: `WaitEvent`, `Call`, `Interaction`, plus control flow (`Break`, `Exit`, `Goto`, `Delay`, `TimeSchedule`); Quartz.NET scheduling |
| **Microservice Integration** | Existing microservice functions registered as schema functions, enabling cross-service workflow orchestration |
| **MCP** | Model Context Protocol support for AI agents to read and understand the entire schema semantic model |

### Architectural Evolution: Meta/Property Split

Version 1 had Meta attributes and Property attributes mixed together. When the frontend team needed display-oriented properties like `color`, `layout`, `width`, etc., it became clear that **metadata (structural definition)** and **properties (behavioral annotation)** needed to be separate concerns:

- **Meta**: Defines *what* a schema kind is — its identity, its core structure
- **Property**: Defines *how* data behaves — constraints, display rules, relations

This split enabled rich frontend configuration without polluting the core structural definitions.

### Design Debt Identified

The Meta/Property split exposed a deeper issue: **the system's extension model was tightly coupled to a single schema family.** Adding new kinds of schemas, properties, or compilation strategies required modifying core code. The architecture needed another level of abstraction.

---

## Version 3: Core/App Split — Three-Tier Extensibility (April 2026)

### The Insight

The fundamental insight of version 3: **SchemaNode is not an application platform — it is a platform for building application platforms.**

This meant splitting the codebase into two layers:

- **`SchemaNode.Core`**: The minimal, vendor-neutral kernel. Defines the four-pillar architecture (Meta + Property + Relation + Function) and the node schema family. Contains no application-specific code. **Extremely small core** — enterprises cannot be "locked in" because the kernel is trivial to understand and replace.
- **`SchemaNode.App`**: The reference application layer built on Core. Provides App/AppField/AppWorkflow, Event, Workflow, Auth, and data management. Can be used as-is or replaced entirely.

### Three Tiers of Extensibility

| Tier | Audience | What You Can Do | Examples |
|------|----------|-----------------|----------|
| **Core** | Platform Architects | Define new schema families (beyond node schema) | New schema kind + generator + runtime type |
| **Architecture** | Solution Architects | Add new properties, CompileContexts, event types, workflow types, API protocols | `QueryFilterCompileContext`, new constraint property, Kafka event source |
| **Engineer** | Developers & Modelers | Define new schema types, functions, complete applications | New struct type, business function, full App with fields and workflows |

### What Each Layer Can Extend

**At the Core level**, you can create entirely new schema families — not just new schemas within the existing node family, but new families with their own kind system, their own runtime types, and their own generators. SchemaNode.App is just one such family.

**At the Architecture level**, you can:
- Register new `Property<T>` subclasses that any schema kind can consume
- Implement new `CompileContext` subclasses that interpret functions differently (e.g., a `GraphQLCompileContext` that compiles validation functions into GraphQL filter syntax)
- Define new event types and workflow node types
- Implement new API protocols (JSON-RPC, gRPC, etc.) for microservice communication
- Register new context providers that inject authentication, localization, or business context

**At the Engineer level**, you can:
- Define new data types (structs, enums) with their fields and constraints
- Register new atomic functions (C# methods) as schema functions
- Define complete applications with fields, workflows, and auth policies
- Configure cross-app data relations and push pipelines

### Design Principles

1. **Minimal Core**: `SchemaNode.Core` should contain only what is absolutely necessary for schema definition and resolution. Everything application-specific lives in `SchemaNode.App` or third-party packages.
2. **No Vendor Lock-in**: Because the Core is small and well-defined, enterprises can replace `SchemaNode.App` with their own application layer without losing their schema investments.
3. **Progressive Disclosure**: Simple use cases (defining a struct with fields) require minimal code; complex use cases (custom CompileContexts) are possible but not required.
4. **Semantic Functions as Universal Interface**: Functions remain the bridge between layers — a function defined in Core can be compiled differently by different CompileContexts in different application layers.

---

## Timeline Summary

```
2019 ─ HVAC Quotation (Lua + Schema) ─── Disaster, never launched
  │
  │  Lessons: Turing-complete scripts unanalyzable;
  │           need semantic functions with multi-context compilation
  │
2021 ─ Datanode v1 ─── Carbon sink accounting
  │     3 people, 700 tables, 40 APIs, 3 months
  │     Core: Data Structure + Relation + Function + App/Field
  │
2025 ─ SchemaNode v2 ── Low-code replacement
  │     + Auth, Event, Workflow, App Workflow
  │     + Microservice integration, MCP for AI
  │     + Meta/Property split
  │
2026 ─ SchemaNode v3 ── Core/App split
        Three-tier extensibility
        Minimal Core = no vendor lock-in
```

---

## Why This History Matters

Every design decision in SchemaNode has a concrete origin in real-world failure or success:

| Design Decision | Origin |
|-----------------|--------|
| Semantic functions (not Turing-complete scripts) | HVAC Lua disaster — unanalyzable scripts are unmaintainable |
| Unique truth: one function → filter + validation | Carbon sink — cannot maintain separate filter and validation logic across 700 tables |
| Schema-driven zero-API architecture | Carbon sink — 3 people cannot write and maintain 2,000 APIs |
| Meta/Property split | v2 frontend requirements — structural identity ≠ behavioral annotation |
| Core/App split with three-tier extensibility | v2 design debt — tight coupling prevents ecosystem growth |
| Minimal Core | Enterprise requirement — no vendor lock-in; replace App layer freely |

SchemaNode is not an academic exercise in metadata theory. It is a **battle-tested platform** born from extreme real-world constraints and refined through years of production use.
