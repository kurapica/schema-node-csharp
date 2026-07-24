# SchemaNode.App — 平台概览

SchemaNode.App 是一个基于 SchemaNode.Core 四大支柱架构（Meta + Property + Relation + Function）的**业务数据管理应用生成与执行平台**。

## 它是什么

- 将 Schema 定义转化为运行中的应用与数据管理的**应用平台**
- 支持 DAG 编排和 Fork 的**事件驱动工作流引擎**
- 覆盖 Schema、App、行、列级别的**细粒度权限系统**
- 从单一函数定义派生数据库查询、数据验证和推送管道的**多上下文编译系统**

## 核心系统

### App Schema 族
- **App**：容器（子 App + Field + Workflow），树状命名空间
- **AppField**：映射到数据库表，含值类型、Source/Push、权限策略
- **AppWorkflow**：工作流节点 DAG（WaitEvent、Call、Interaction、控制流）

### Event 系统
基于 Topic 的发布/订阅，支持通配符（`+` 单级、`*`/`#` 多级）。基于 Reactive Extensions，支持 Kafka/RabbitMQ 源。
- **App 事件**：数据增删改
- **Schema 事件**：Schema 增删改、App Schema 增删改

### Workflow 系统
DAG 编排：
- **WaitEvent**：订阅事件，支持 Fork（保持存活等待下一事件）或单次
- **Call**：执行 Schema 函数
- **Interaction**：等待外部 API 交互
- **Control**：Break、Exit、Goto、Delay、TimeSchedule（Quartz.NET）

### Auth 系统（三级）
- **Auths**：Schema/App/Workflow 级，基于求值函数的策略
- **RowAuths**：AppField 行级过滤函数
- **ColAuths**：AppField 列级求值函数

### CompileContext 系统
"唯一真相"原则的实践：
- **QueryFilterCompileContext**：`(StructType) → bool` 函数 → `AppSchemaDataFilter` 查询树，同一函数验证提交数据 + 过滤查询数据
- **DataPushCompileContext**：编译推送函数，提取第三方依赖，批量查询优化

### 动态表与数据管理
- 值类型决定表结构，~40 个通用 API
- Push & Source：源变更 → 推送函数 → 目标更新
- DisplayOnly + Relation：跨表字段查询

## 进一步阅读
- [PLATFORM_ARCHITECTURE.zh.md](./PLATFORM_ARCHITECTURE.zh.md) — 深入架构
- [FEATURE_GUIDE.zh.md](./FEATURE_GUIDE.zh.md) — 实用指南
- [Core PROJECT_HISTORY](../SchemaNode.Core/Doc/PROJECT_HISTORY.zh.md) — 为什么这样设计
