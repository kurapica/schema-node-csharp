# SchemaNode.Core 文档索引

> 本页是 `SchemaNode.Core/Doc` 的中文入口，帮助不同角色快速找到合适的材料。

## 文档清单

### 1. `PLATFORM_OVERVIEW.zh.md`

**定位：对外摘要版 / 快速介绍版**

适合场景：

- 第一次了解 SchemaNode
- 对外沟通、生态招募、合作交流
- 架构评审前的快速预读
- 仓库首页或提案材料中的简版引用

你会看到：

- SchemaNode 是什么
- 它不是什么
- 三个核心价值
- 核心抽象的极简关系
- 为什么它值得高级架构人员关注

### 2. `PLATFORM_ARCHITECTURE.zh.md`

**定位：对外平台架构长文档 / 白皮书风格说明**

适合场景：

- 需要系统理解 SchemaNode 的平台定位
- 需要评估架构边界、扩展机制和生态潜力
- 需要对外展示平台设计思想
- 希望参与平台共建或长期架构演进

你会看到：

- 为什么 SchemaNode 不是“再做一套 JSON Schema”
- 为什么 `Property` 组合是平台第一核心
- 为什么 `PropertySchema` 让平台自解释
- `NodeSchema` 族在平台中的真实定位
- 统一的数据类型、操作和关联模型
- 动态 schema 的来源、合并与运行时生命周期
- 老系统语义化接入路径
- 面向 AI 的语义输出与运行期临时应用
- 多端一体化配置与 API 收敛价值
- 核心抽象关系图（ASCII）

### 3. `PLATFORM_OVERVIEW.en.md`

**定位：英文摘要版**

适合：

- 对英文读者进行快速介绍
- 用于国际化交流、邮件、提案摘要

### 4. `PLATFORM_ARCHITECTURE.en.md`

**定位：英文平台架构长文档**

适合：

- 英文环境下的架构沟通
- 吸引国际化技术贡献者
- 对外展示平台架构愿景

---

## 推荐阅读路径

### 路径 A：第一次了解 SchemaNode

建议顺序：

1. `PLATFORM_OVERVIEW.zh.md`
2. `PLATFORM_ARCHITECTURE.zh.md`

### 路径 B：面向高级架构评审

建议顺序：

1. `PLATFORM_OVERVIEW.zh.md`
2. `PLATFORM_ARCHITECTURE.zh.md`
3. 结合代码查看 `Property<T>`、`ExtensibleSchema`、`SchemaRuntime`、`SchemaContext`、`FunctionSchema`、`RelationSchema`

### 路径 C：面向国际沟通

建议顺序：

1. `PLATFORM_OVERVIEW.en.md`
2. `PLATFORM_ARCHITECTURE.en.md`

### 路径 D：想快速判断是否值得参与

如果你只想在 5 分钟内判断 SchemaNode 是否值得继续看，优先读：

1. `PLATFORM_OVERVIEW.zh.md`
2. `PLATFORM_OVERVIEW.en.md`（如需要英文转发）

---

## 不同角色应该读什么

### 平台架构师

优先阅读：

- `PLATFORM_OVERVIEW.zh.md`
- `PLATFORM_ARCHITECTURE.zh.md`

重点关注：

- property 组合机制
- schema family / schema property 双扩展轴
- 动态 schema 与 runtime 生命周期
- 多端统一元模型
- AI 语义化连接能力

### 前端核心或 TypeScript 实现参与者

优先阅读：

- `PLATFORM_OVERVIEW.zh.md`
- `PLATFORM_ARCHITECTURE.zh.md`
- 英文对应版本（用于统一术语）

重点关注：

- 跨语言语义基线
- property 解释逻辑
- schema family 扩展语义
- 前后端共享元模型边界

### 后端框架与微服务架构人员

优先阅读：

- `PLATFORM_ARCHITECTURE.zh.md`

重点关注：

- Meta 驱动的 system schema
- 旧 API / 微服务语义化接入
- 少量稳定 API + 动态 schema 的编排方式
- provider 驱动的动态加载和合并

### AI / 知识工程参与者

优先阅读：

- `PLATFORM_OVERVIEW.zh.md`
- `PLATFORM_ARCHITECTURE.zh.md`

重点关注：

- schema 的语义化输出
- MCP / JSON Schema / Ontology 映射
- 运行期临时应用
- 可审计、可回放的元数据执行日志

---

## 一句话导航

如果只保留一句话来说明本目录：

> 先看 `PLATFORM_OVERVIEW`，快速理解平台价值；再看 `PLATFORM_ARCHITECTURE`，完整理解 SchemaNode 为什么值得作为跨语言、可扩展、自解释的元数据平台持续投入。

