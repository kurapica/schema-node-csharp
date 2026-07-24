# SchemaNode.Core — 平台概览

SchemaNode.Core 是一个**语言无关、自描述、可扩展的元数据平台内核**，建立在四大支柱之上：**Meta**、**Property**、**Relation**、**Function**。

## 它是什么

- 在单一元模型中**统一管理**数据类型、操作和关联
- 一个**自描述平台**，Property 定义本身即是受管理的 Schema 对象
- 一个**可扩展架构**，允许第三方定义新的 Schema 族、属性和编译策略
- 一个**可执行运行时**，解析、验证、编译 Schema

## 它不是什么

- ❌ 另一个 JSON Schema 格式
- ❌ 低代码平台
- ❌ 数据库 ORM
- ❌ 封闭框架

## 四大支柱

| 支柱 | 问题 | 实现 |
|------|------|------|
| **Meta** | 这个 Schema Kind *是什么*？ | `[Meta<T>]` C# 注解 |
| **Property** | 数据*如何行为*？ | `Property<T>` 可组合注解 |
| **Relation** | 数据*如何关联*？ | `IRelationProcess` 动态规则 |
| **Function** | 计算*如何工作*？ | 纯函数表达式 → 编译委托 |

## 核心概念

### Meta — Schema Kind 身份
通过 `[Meta<T>]` 定义 Schema Kind，如 `StructSchema` 声明 `[Meta<SchemaKind>("struct")]`、`[Meta<NodeType>(typeof(RuntimeStructType))]`。Node Schema 族含 16 个内建 Kind。

### Property — 可组合注解
约束（`Require`、`UpLimit`）、显示（`Visible`、`Unit`）、系统（`SchemaType`、`OverrideType`）。特殊属性：`IConstraintProperty`（验证）、`ITypeRefProperty`（引用完整性）。

### Relation — 动态数据关联
`Assign` 强制赋值；`Call` 函数计算（如 `lookup_manager($store_id)`）。支持自定义 `IRelationProcess`。

### Function — 语义表达式引擎
**原子函数**从 C# 注册；**语义函数**是纯表达式组合。**CompileContext** 将同一函数编译为多种目标——**唯一真相**原则。

## 运行时
- `SchemaRuntime`：全局注册
- `SchemaContext`：按请求解析，System→Service→Remote 链
- `NodeType`/`ValueType`：可执行运行时类型
- `DataNode`：运行时数据

## 扩展性
1. **Schema 族** — 新 Kind、生成器（平台架构师）
2. **Property** — 新 `Property<T>` 子类（解决方案架构师）
3. **函数库** — C# 原子函数（开发者）
4. **CompileContext** — 自定义编译策略（解决方案架构师）

## 跨语言
- TypeScript 前端共享元模型
- JSON 载荷自描述
- MCP 支持 AI 语义理解
- `Meta` 接入遗留系统

## 延伸阅读
- [PROJECT_HISTORY.zh.md](./PROJECT_HISTORY.zh.md) — SchemaNode 为何存在（三版演化史）
- [PLATFORM_ARCHITECTURE.zh.md](./PLATFORM_ARCHITECTURE.zh.md) — 含代码示例的架构深探
- [FEATURE_GUIDE.zh.md](./FEATURE_GUIDE.zh.md) — 实用指南
