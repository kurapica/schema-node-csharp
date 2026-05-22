# SchemaNode.Core Documentation Index

> This page is the English entry point for `SchemaNode.Core/Doc`, helping different readers find the right material quickly.

## Document List

### 1. `PLATFORM_OVERVIEW.en.md`

**Positioning: external short summary / quick introduction**

Useful when you need:

- a first look at SchemaNode
- external communication, ecosystem outreach, or partner conversations
- a short pre-read before architecture review
- a concise reference for repository front pages or proposal decks

You will find:

- what SchemaNode is
- what it is not
- its three core values
- a minimal abstraction view
- why it is interesting to senior architects

### 2. `PLATFORM_ARCHITECTURE.en.md`

**Positioning: long-form external platform architecture document / whitepaper-style explanation**

Useful when you need to:

- understand the platform positioning of SchemaNode in depth
- evaluate abstraction boundaries, extension mechanisms, and ecosystem potential
- present the platform architecture externally
- participate in long-term platform evolution

You will find:

- why SchemaNode is not “another JSON Schema”
- why `Property` composition is the first architectural core
- why `PropertySchema` makes the platform self-describing
- the real role of the `NodeSchema` family
- the shared model for data types, operations, and association
- dynamic schema sources, merging, and runtime lifecycle
- semantic onboarding of legacy systems
- AI-oriented semantic output and temporary runtime applications
- unified multi-end configuration and API consolidation
- the core abstraction relationship diagram (ASCII)

### 3. `PLATFORM_OVERVIEW.zh.md`

**Positioning: Chinese short summary**

Useful when:

- introducing SchemaNode quickly to Chinese-speaking readers
- sharing concise materials in local discussions

### 4. `PLATFORM_ARCHITECTURE.zh.md`

**Positioning: Chinese long-form platform architecture document**

Useful when:

- conducting architecture discussions in Chinese
- presenting the platform vision to local contributors and partners

---

## Recommended Reading Paths

### Path A: First time learning about SchemaNode

Recommended order:

1. `PLATFORM_OVERVIEW.en.md`
2. `PLATFORM_ARCHITECTURE.en.md`

### Path B: Senior architecture review

Recommended order:

1. `PLATFORM_OVERVIEW.en.md`
2. `PLATFORM_ARCHITECTURE.en.md`
3. Then inspect the code around `Property<T>`, `ExtensibleSchema`, `SchemaRuntime`, `SchemaContext`, `FunctionSchema`, and `RelationSchema`

### Path C: Chinese-language communication

Recommended order:

1. `PLATFORM_OVERVIEW.zh.md`
2. `PLATFORM_ARCHITECTURE.zh.md`

### Path D: Fast “is this worth following?” judgment

If you only want a 5-minute answer to whether SchemaNode is worth further attention, start with:

1. `PLATFORM_OVERVIEW.en.md`
2. `PLATFORM_OVERVIEW.zh.md` (if you need to forward the Chinese version)

---

## What Different Readers Should Read

### Platform architects

Start with:

- `PLATFORM_OVERVIEW.en.md`
- `PLATFORM_ARCHITECTURE.en.md`

Focus on:

- the property-composition mechanism
- the dual extension axes of schema families and schema properties
- dynamic schema lifecycle in runtime
- the unified meta-model across endpoints
- AI-semantic integration potential

### Frontend core / future TypeScript contributors

Start with:

- `PLATFORM_OVERVIEW.en.md`
- `PLATFORM_ARCHITECTURE.en.md`
- the Chinese versions as needed for terminology alignment

Focus on:

- cross-language semantic baseline
- property interpretation logic
- schema-family extension semantics
- frontend/backend shared meta-model boundaries

### Backend framework and microservice architects

Start with:

- `PLATFORM_ARCHITECTURE.en.md`

Focus on:

- Meta-driven system schema
- semantic onboarding of legacy APIs and microservices
- orchestration through a small stable API surface plus dynamic schema
- provider-driven dynamic loading and merging

### AI / knowledge-engineering contributors

Start with:

- `PLATFORM_OVERVIEW.en.md`
- `PLATFORM_ARCHITECTURE.en.md`

Focus on:

- semantic export of schemas
- MCP / JSON Schema / ontology mapping
- temporary runtime applications
- auditable, replayable metadata execution logs

---

## One-Sentence Navigation

If this directory needs to be summarized in one sentence:

> Start with `PLATFORM_OVERVIEW` to understand the platform value quickly, then move to `PLATFORM_ARCHITECTURE` to understand why SchemaNode is worth long-term investment as a cross-language, extensible, self-describing metadata platform.

