# SchemaNode 平台概览

> 面向外部沟通的摘要版本，适合在仓库首页、架构评审、生态招募或合作交流中快速介绍 SchemaNode。

## 一句话说明

**SchemaNode 是一个以 `Property` 组合为核心的、语言无关、自解释、可扩展的元数据平台内核。**

它不只描述数据结构，还统一描述：

- 数据类型
- 操作与函数
- 数据关联与动态规则
- UI / 校验 / 布局 / 行为等扩展元数据

---

## 它不是什么

SchemaNode 不是：

- 另一套仅围绕 JSON 的 Schema 文件格式；
- 一个只做表单配置或字段校验的工具库；
- 一个只能由核心团队维护能力边界的封闭框架。

JSON 在 SchemaNode 中只是表达形式之一。

`SchemaNode.Core` 关注的是**元模型、扩展机制、运行时和跨语言实现**；更贴近应用层的 JSON Schema 风格表达，会放到 `SchemaNode.App` 中演进。

---

## 它的三个核心价值

### 1. 用 Property 组合定义 Schema

SchemaNode 的核心不是不断给 schema 增加固定字段，而是通过 `Property<T>` 组合形成语义。

这让平台可以稳定演进：

- 核心结构保持克制；
- 扩展能力通过 property 增长；
- 第三方可以新增 schema property，而不必修改核心模型。

### 2. Property 自身进入元数据系统

在 SchemaNode 中，property 自己也会被注册为 `PropertySchema`。

这意味着平台不仅管理业务元数据，还管理“元数据定义本身”，从而形成**自解释平台**。

### 3. 统一类型、操作和关联模型

SchemaNode 为扩展生态提供统一底座：

- `Scalar / Enum / Struct / Array`：统一数据类型模型；
- `FunctionSchema`：统一操作语言与执行模型；
- `RelationSchema`：统一 property 联动与动态规则模型。

这意味着未来第三方扩展 schema family 时，不需要重新发明自己的类型系统和规则系统。

---

## 核心抽象

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

## 平台为什么值得关注

SchemaNode 适合吸引高级架构和生态参与者的原因在于：

- 它开放 **schema family** 和 **schema property** 两条扩展轴；
- 它具备自解释能力，适合治理、注册和生态发现；
- 它既有静态元模型，也有可执行运行时；
- 它天然适合跨语言、多端共享和插件式演进；
- 它的目标不是单点工具，而是社区型平台内核。

---

## 当前分层定位

- `SchemaNode.Core`
  - 定义元模型
  - 定义 property 组合机制
  - 提供 C# 参考实现
  - 提供运行时加载与执行能力
- `SchemaNode.App`
  - 承载更贴近应用层的 JSON Schema 风格表达
  - 承载 App 级工作流、UI 协议与装配逻辑
- 未来 TypeScript Core
  - 作为前端核心共享相同元模型与解释逻辑

---

## 需要特别补充的四点

### 1. Kind 固定于代码，Schema 大量动态化

SchemaNode 中的 `Schema Kind` 是由系统代码定义和注册的语义入口，但它仍然允许第三方代码扩展新的 kind。

而真正运行时使用的 schema，则可以来自两类来源并合并使用：

- 由代码和 `Meta` 抽象出的 `system schema`
- 从数据库、中心 schema 服务等远端加载的动态 schema

在实际平台场景里，绝大多数被执行的 schema 都会是动态定义、动态加载、动态更新甚至动态卸载的。这也是前端配置、中心管理和实时运行时能够成立的基础。

### 2. 旧系统可以被语义化接入

通过 `Meta` 描述，既有系统中的类型和 API 可以被注册为 SchemaNode 中的数据类型和函数，也就是注册为 system schema 的一部分。

这意味着：

- 老类型可以语义化进入统一类型系统；
- 老 API 可以语义化进入统一函数系统；
- 微服务或其他语言系统只需要一层注册/适配调用，就可以接入平台，而不必整体重写。

### 3. Schema 天然适合 AI 识别与运行期应用

SchemaNode 的 schema 是语义化对象，因此可以进一步输出为：

- MCP 能力描述；
- JSON Schema 风格结构；
- Ontology 语义结构。

这使 AI 可以在语义层理解、定制、修改模型，甚至在运行期生成临时应用并执行后销毁。同时，应用本身和执行数据都可以一起作为日志保存，形成可审计、可回放的 AI 驱动应用过程。

### 4. 多端一体化配置减少沟通与维护成本

统一元模型意味着前端、后端、流程和配置可以围绕同一个语义核心协同。

它带来的直接收益是：

- 更低的沟通成本；
- 更少的接口分裂；
- 在复杂微服务系统中仍能用较少 API 完成大部分业务编排；
- 比传统低代码平台更容易维护，也更适合二次开发。

---

## 对外结论

如果用一句话向外部介绍 SchemaNode：

> SchemaNode 正在构建一个跨语言的元数据平台：通过 `Property` 组合定义和扩展 schema，通过 `PropertySchema` 形成自解释能力，通过 `FunctionSchema` 提供统一操作语言，通过 `RelationSchema` 提供动态联动，并向第三方开放 schema family 与 schema property 两条生态扩展轴。

