# SchemaNode.Core — 平台架构说明

> SchemaNode.Core 是一个**语言无关、自描述、可扩展的元数据平台内核**，建立在四大支柱之上：**Meta**、**Property**、**Relation**、**Function**。
> SchemaNode.Core 并不是数据库 Schema，也不是 ORM，而是一个用于描述类型、行为、关联和计算的元数据运行时。

## 目录

1. [四大支柱架构](#四大支柱架构)
2. [Meta — Schema Kind 的元数据定义](#meta--schema-kind-的元数据定义)
3. [Property — 可组合的行为注解](#property--可组合的行为注解)
4. [Relation — 动态数据关联](#relation--动态数据关联)
5. [Function — 语义表达式引擎](#function--语义表达式引擎)
6. [Schema 族的构成与协作](#schema-族的构成与协作)
7. [Node Schema 族](#node-schema-族)
8. [总结](#总结)

---

## 四大支柱架构

SchemaNode.Core 建立在四个基本抽象之上。每个支柱解决一个独特的问题：

| 支柱 | 回答的问题 | 实现方式 |
|------|-----------|---------|
| **Meta** | 这个 Schema Kind *是什么*？ | C# 申明SchemaKind的类型和其用法 |
| **Property** | 数据*如何行为*？ | `Property<T>` 为SchemaKind申明扩展属性，对数据进行描述 |
| **Relation** | 数据*如何关联*？ | `Relation` 基于关联数据计算Property |
| **Function** | 计算*如何工作*？ | 语义函数 -> 多目标编译系统 |

---

## Meta — Schema Kind 的元数据定义

以 **Node Schema** 的部分申明为例，它是数据节点类型的Schema申明：

```csharp

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NODE, SCHEMA_KIND_ORDER_NODE)] // 申明一个新的schema kind
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.schema")]        // 注册这个class为一个struct type，strut同样是一个schema kind
[Meta<Attach>(SCHEMA_KIND_NODE)]                             // Attach 是 struct kind 的一个扩展属性，
                                                             // 意思是将指定schema kind的扩展属性附着到该结构体，方便在前端展示和配置
public sealed class NodeSchema: ExtensibleSchema             // ExtensibleSchema 提供了对property扩展属性的存储支持
{
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<PrimaryIndex>(0)]                                  // 主键申明，class如果申明了主键，索引等信息，会同步生成一个array type包含这类信息
    [Meta<SchemaType>(typeof(NamespaceType))]                // Field申明SchemaType是用于指明保存为struct field时的schema type类型，这里即命名空间名称
    public string? Namespace { get; set; }                   // Node Schema族定义的schema type使用命名空间进行管理
    
    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<PrimaryIndex>(1)]                                  // 多主键，用Order确定顺序
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The full namespace name
    /// </summary>
    [SchemaIgnore]                                           // 生成struct type时忽略该属性，这是C#内使用的，不需要生成为struct field
    [JsonIgnore]
    public string FullName => $"{Namespace}.{Name}".Trim('.');
    
    /// <summary>
    /// The schema kind
    /// </summary>
    [Meta<SchemaType>(typeof(NodeSchemaKind))]               // 子 schema kind，这里 NodeSchemaKind 定义如下
    public string Kind { get; set; } = null!;
}

/// <summary>
/// Represents the node schema kinds
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.kind")]         // Enum类型会生成为一个enum schema type，同样这里制定命名空间名称
[Meta<Record>(typeof(Property.Record.NodeSchemaKind))]      // 这类枚举类型的值是基于申明动态注册的，实现是通过定义一个记录用property完成
public enum NodeSchemaKind;

/// <summary>
/// The node schema kind record
/// </summary>
public class NodeSchemaKind : RecordProperty<string>;       // 属性定义简单，基于Property<T>或者扩展类型定义即可，部分property是系统内部使用

```

系统采用了 `Meta<T>(...) where T: Property` 的申明方式来替代注册，这确保其他项目也可以基于申明注册新的schema kind，schema type，property等功能，
而无需了解复杂的代码注册语法。

通常来说，一个schema kind是一个类型原型，它通常关联一个可以注册为struct type的class，这个class的字段决定了类型原型的元数据(meta)，也就是不变的确定这个类型
功能的数据，这个类型如何使用是消费这个类型的系统所确定的，而SchemaNode.Core的内核只关心如何基于 **function组织meta+property+relation**，并且确保在前端的
配置界面可以完成配置。



## Property — 可组合的行为注解

Property 通过 `Property<T>` 基类实现，实现对Meta元数据的扩展，也完成类似数据校验等功能。

```csharp
public abstract class Property<T> : IProperty
{
    public T? Value { get; private set; }
    public bool Stackable { get; }       // 可与同类型组合
}
```

Property是各端可以申明的对schema kind的元数据进行扩展的属性，例如表单使用的visible，前后端申明的属性可以不一致，各端只消费自己能识别的属性。

参考 **struct kind** 进一步说明：

```csharp
/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]      // 申明一个新的 struct kind
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]  // 为 NodeSchemaKind 注册一个新的"struct"枚举值
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)] // 为 ValueSchemaKind 注册一个新的"struct"枚举值，这两个都是enum schema type
[Meta<NodeType>(typeof(RuntimeStructType))]                           // 申明C#的运行时类型，用于管理类型关联并提供额外功能，例如FunctionType提供CallAsync用于执行
[Meta<SchemaGenerator>(typeof(StructGenerator))]                      // 申明从C#的Type提取strut schema的生成器，它实现复杂的解析和注册流程，
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]               // 将 StructSchema C#类注册为 system.schema.struct.schema 这个命名空间的struct schema type
[Meta<Attach>(SCHEMA_KIND_STRUCT)]                                    // 将 struct kind得扩展属性关联到这个struct schema type
[Meta<Append>(typeof(Relations))]                                     // Append用于添加已经存在的属性，属性定义可能更早或者不指定自己为哪些schema kind扩展，就可以通过Append添加
[Relation<EntrySource>($"${nameof(UnionValids)}.{nameof(StructUnionValidation.Args)}.{nameof(CallArg.Source)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
public sealed class StructSchema : ExtensibleSchema
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];             // struct的元数据，定义struct field 结构体字段
    
    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }       // 定义字段间的联合校验规则
}
```

系统使用`Generator`机制从C#代码提取system schema，但实际系统运行时，绝大部分schema type都是前端配置的，system schema只是维持系统运行的基础部分。

然后我们通过property系统将struct附着到 Node Schema上:

```csharp
/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<Alias>("struct")]                                             // 属性名称，如果省略，用class名，去掉后面的Property，第一个字母小写，也是struct   
[Meta<ForSchema>(SCHEMA_KIND_NODE)]                                 // 申明这个属性为哪些schema kind定义，这是node schema的附着属性
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]                              // 申明哪个schema kind的generator可以用于解析它，struct type的generator是默认用，其余的则需要OfSchema限定
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.struct")]      // 这里注册为一个property schema type，名字指定为 system.schema.property.core.struct
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
public sealed class StructProperty: Property<StructSchema>;
```

通过这个属性，就为Node Schema附着了一个struct属性，实际效果是:

```json
{
    namespace: "system",
    name: "test",
    kind: "struct",

    struct: {
        fields: [
            {
                name: "x",
                type: "system.number",
                require: true,
            }
        ]
    }
}
```

所有注册到schema系统的类型都是和语言无关的，通常它们在各端中间传递都是基于JSON结构。所以struct就是node schema的一个扩展属性，Node schema并不会理解它，但不妨碍struct基于node定义。

除了struct这类比较特殊的属性外，系统也提供一些常规属性：

```csharp
[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_INT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(LowLimitInt)}")]
public class LowLimitInt : Property<long>, IConstraintProperty
{
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (node.IsEmpty) return null;
        return node.GetValue<long>() >= Value;
    }
}


[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DATE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(LowLimitDate)}")]
public class LowLimitDate : Property<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateDate(SchemaContext context, DateNode node)
    {
        if  (node.IsEmpty) return null;
        return node.GetValue<DateTimeOffset>() >= Value;
    }
}
```

这两个类分别为int, date 两个schema kind提供同名的 **lowlimit** 属性，同时它们实现了IConstraintProperty接口，也会被用于作为数据校验，一个数据违背的所有约束都会保存到校验结果中。
注意，只有申明了SchemaType的property会注册到schema系统，才能被传递给其他端使用（例如作在配置界面渲染）。


## Relation — 动态数据关联

Relation 定义属性值如何基于其他数据动态计算。两种内置类型：

**Assign** — 强制赋值：
```csharp
public class Assign : IRelationProcess
{
    public object? Value { get; set; }
    public Task<object?> ProcessAsync(...) => Task.FromResult(Value);
}
```

**Call** — 基于函数的计算（如 `lookup_manager($store_id)`）：
```csharp
public class Call : IRelationProcess
{
    public string Func { get; set; }
    public CallArg[] Args { get; init; }
}
```

在上面的例子中，我们用到了两个Relation申明:

```csharp
[Relation<EntrySource>($"${nameof(UnionValids)}.{nameof(StructUnionValidation.Args)}.{nameof(CallArg.Source)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
```

每个Relation都关联一个属性Property，一个目标Target，然后是执行参数，这里`Relation<T>`使用的是`Call`执行方式的关联定义，以

```csharp
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
```

为例，它关联属性是Visible，因为是在属性类型上定义，它的Target即属性自身，也就是 **struct**，结合下面的json看，

```json
{
    namespace: "system",
    name: "test",
    kind: "struct",
    struct: {}
}
```

这个Relation表达的是 `struct.visible = kind == "struct"`， 也就是仅当kind为struct时，才会显示 struct 配置表单。

实际Relation在前端配置界面使用时比较容易理解和使用，对于一个类型例如结构体，可以选择它的子字段作为target，然后指定一个属性作为Relation计算结果的保存目标，其他则是参数，对于Call，它的参数是该结构体的其他字段或者常数，当
关联字段值被修改时，Relation会被重新执行，计算结果保存到属性，触发重新渲染，这是最基本的用法。

c#层配置的Relation基本是为了对配置界面的联动规则进行控制，而非在配置界面写死，导致调整需要多处进行。



---

## Function — 语义表达式引擎

SchemaNode 函数是**纯函数、非图灵完备、语义可分析**的。两种类型：

**原子函数**（C# 注册，无函数体）：
```csharp
// 函数申明需指明命名空间，下面的static不带有[SchemaIgnore]的函数都会被注册
// 函数的参数和返回值都基于反射解析，支持范型
[Meta<SchemaType>(NS_SYSTEM_LOGIC)] 
public static class SystemLogic
{
    /// <summary>
    /// system.logic.and
    /// </summary>
    public static bool and([Meta<Default>(false)] bool a, [Meta<Default>(false)] bool b) => a && b;
}
```

**语义函数**（表达式体，前端配置，无变量/循环）：
``` json
{
    namespace: "system.math",
    name: "clamp",
    kind: "function",
    function: {
        args: [
            {
                name: "value",
                type: "system.number"
            },
            {
                name: "min",
                type: "system.number",
            },
            {
                name: "max",
                type: "system.number",
            }
        ],
        return: "system.number",
        exps: [
            {
                name: "cmin", // = max(value, min)
                type: "call",
                func: "system.math.max",
                args: [
                    {
                        "source": "value"
                    },
                    {
                        "source": "min"
                    }
                ]
            },
            {
                name: "result", // = min(cmin, max)
                type: "call",
                func: "system.math.min",
                args: [
                    {
                        "source": "cmin",
                    },
                    {
                        "source": "max"
                    }
                ]
            }
        ]
    }
}
```

语义函数采用纯函数式调用的方式定义，这样无论哪个平台都可以采用函数调用的解释执行方式实现，它的处理有几个特殊点：

- 基于返回值类型判定结果构成方式，如果返回值类型和最后一个表达式结果类型一致，则采用最后一个表达式的结果。
- 如果返回值类型是结构体，则尝试按照结构体字段，查找所有表达式名字和字段一致的，组合成结构体返回。
- 表达式的type指明了调用模式，默认都是call直接调用，但参数中存在数组时，可以使用类似map，reduce，first等执行方式处理数组。

虽然平台可以采用解释执行的方式执行语义函数，但实际在C#中提供了更强的编译模式，其余平台则参考用途实现（例如typescirpt执行采用解释执行，无需处理多用途）

C# 提供了 **CompileContext** 多目标编译系统，用于实现将语义函数表达式转换为 Expression，再编译为Delegate确保执行速度接近C#原生。
除了最基本的编译执行外，它还提供了基于不同的CompileContext将同一函数编译为不同形式。

以`RowAuths`为例，它给出基于访问用户的当前角色来确定如何过滤数据的规则，类似 `func(context, data) => data.userid == context.GetContextItem<user>().id`， 即从上下文获取当前用户，然后判定数据所有权，实际处理可能涉及数据请求等，这里不做扩展。

当用户请求数据时，系统会采用 **QueryCompileContext** 对这个函数进行编译，它的返回结果不再是bool值，而是这个逻辑处理转换得到的请求树，这个请求树可以和其他查询条件合并最终完成数据请求。

当用户上传数据时，系统则采用默认的 **CompileContext** 编译函数，将数据传入其中，返回布尔值来确定数据是否有效。这点确保了只需要修改这个函数，就可以同时影响查询和上传，避免两个动作的不一致性。

这就是**唯一真相**原则：一个函数定义 = 验证 + 过滤 + 任意编译目标。

这类多目标编译上下文很容易实现，也是函数系统对外的功能扩展点。SchemaNode.Core并不关心函数如何执行，而是函数如何组织语义表达，实际执行由编译上下文决定，哪些原子函数作为语义原子被识别也是由编译上下文决定。

---

## Schema 族的构成与协作

### 原型与实例

基于 SchemaNode.Core 的核心可以定义不同的 **Schema 族**。一个 Schema 族由两部分构成：

1. **一组原型定义（Prototype）**——即一系列 Schema Kind，每个 Kind 申明该类型原型的元数据（Meta）、可用的扩展属性（Property）以及数据间的动态关联规则（Relation）。这些原型是抽象的、可配置的模板，例如 `struct` kind 申明了"结构体"的原型含义——它包含字段列表和联合校验规则，但它本身不是一个具体的结构体。

2. **一组运行时能力（Runtime Capability）**——即消费这些原型的运行时能力。例如 Node Schema 族的 ValueType 体系提供了 `Validate()`、类型转换、序列化等功能，FunctionType 提供 `CallAsync()` 执行能力。这些功能是另行开发的代码，它们理解原型的语义并基于通用逻辑处理所有具体类型。

原型通过配置界面实例化为具体的类型（Instance）。例如，基于 `struct` 原型，可以在前端配置一个名为 `system.user` 的结构体，包含 `name`、`age`、`email` 等字段。这个具体类型遵循 `struct` 原型的元数据定义，并受 `struct` 原型关联的功能集合处理——例如 ValueType 的通用验证逻辑会遍历其所有字段并执行各自的约束检查。

在上面的例子中，Schema Kind的Meta描述由struct schema承载，它既是元数据，又是struct的instance。

```
                 Schema Family

      Prototype                 Runtime Capability
      ─────────                 ──────────────────
      struct kind ─────────────▶ RuntimeStructType
      enum kind   ─────────────▶ RuntimeEnumType
      scalar kind ─────────────▶ RuntimeScalarType
               ▲
               │
               │
        Schema Instance
      ──────────────────
      system.user
      system.order
      system.product
```

### Schema 族间的协作

每个新的 Schema 族都可以基于之前定义的族调用功能来补全自己。例如：

- **SchemaNode.App** 族定义了 `app`、`appfield`、`appworkflow` 等 Kind，但它的 AppField 的值类型引用的是 Node Schema 族中的 ValueType（`struct`、`enum`、`scalar` 等）。App 族不需要重新定义"什么是结构体"——它直接复用 Node Schema 族的定义和功能集合。
- 未来如果有人定义 **IoT 族**，它的 `device` 原型可以申明一个 `schema` 属性引用 Node Schema 族的 `struct` kind，用于描述设备的数据模型；它的 `telemetry` 原型可以引用 `function` kind 用于描述遥测数据的处理逻辑。

这种协作模式的核心在于：**每个族专注于自己的领域语义，通用能力通过引用已有族来获得。** Node Schema 族作为系统默认提供的第一个族，承担了"通用数据模型"的角色。

### 声明层与执行层的边界

SchemaNode 的架构中存在一条清晰的边界：**Schema 管理一切声明式的、可配置的、动态的内容；执行层则是 "dirty" 的命令式代码，但它通过通用处理避免了 "dirty" 的类型判定。**

具体来说：

- **声明层（Schema）**：用户通过配置界面定义的一切——结构体的字段、枚举的值、函数的表达式、Relation 的计算规则。这些内容可以随时在线修改，无需重新编译或发版。Schema 系统负责存储、校验、传递这些声明。

- **执行层（Runtime）**：功能集合中的代码——例如 `ValueType.Validate()`、`FunctionType.CallAsync()`、`IRelationProcess.ProcessAsync()`。这些代码是编译时确定的，它们不关心"当前在处理的是 user 还是 order"，只关心"当前在处理的是一个 struct，struct 有字段，字段有约束，遍历检查"。

这种边界设计的价值在于：

> **执行层不需要为每种新类型写新的判断逻辑。** 无论前端配置了多少个结构体——`user`、`order`、`product`、`invoice`——`ValueType.Validate()` 的逻辑都不需要改动。它通用地读取结构体的字段列表，遍历每个字段的约束属性，执行对应的 `IConstraintProperty`。新增 100 个结构体类型，执行层代码零增长。

同样的模式贯穿所有功能集合：
- `FunctionType.Compile()` 不关心函数的业务含义，只关心表达式树的编译
- `RelationSchema` 的 Process 不关心计算的是什么属性，只关心执行 `Call` 或 `Assign` 的规则
- SchemaNode.App 的 `IAppDataProvider` 不关心表里存的是什么数据，只关心基于 schema 动态生成 SQL

这使得 SchemaNode 能够实现"配置即功能"——新能力通过配置层产生，执行层保持稳定。


## Node Schema 族

基于以上设计，系统默认提供 **Node Schema** 族。它定义了通用的数据类型（`scalar`、`enum`、`struct`、`array`）和核心类型（`property`、`relation`、`function`），以及消费它们的功能集合（ValueType 验证体系、FunctionType 编译执行、Relation 动态计算）。

上层的应用如 SchemaNode.App 都是可替换的——它们基于 Node Schema 族构建，但 Core 本身不依赖任何上层族的存在。而异构的、基于 SchemaNode.Core 的不同系统，则可以通过 **Node Schema** 族完成数据共享和功能互操作——因为无论上层族如何定义，它们都共享同一套通用数据模型语言。


## 总结

SchemaNode.Core 不是一个"功能齐全"的平台——它刻意不包含业务功能。它是一个**语义组织框架**：

- **Meta** 定义原型，确定每种类型"是什么"
- **Property** 提供扩展，描述数据"如何行为"
- **Relation** 建立关联，定义属性值"如何计算"
- **Function** 组织执行，表达"如何运作"

四者组合在一起，通过 Schema 族（原型 + 功能集合）的组织方式，让执行层保持通用和稳定，让配置层承载所有可变性。这就是 SchemaNode 能够做到"配置即功能，新类型零代码增长"的根本原因。

SchemaNode.Core只关注于如何基于`Meta`, `Property`, `Relation`和`Function`构建，而Node Schema族是它们的载体。基于此我们可以进一步定义其他schema族完成实际功能，并基于Node Schema共享数据。

```
              SchemaNode.Core

    Meta   Property   Relation   Function
         \      |      |      /
          \     |      |     /
           \    |      |    /
            ┌───────────────┐
            │   Node Schema │
            │   Data Model  │
            └───────────────┘
                    │
              Schema Runtime
                    │
      ┌─────────────┼─────────────┐
      │             │             │
  Form Engine   Workflow      App Model
      │             │             │
```

四大支柱定义了 Schema 的描述能力，Schema Family 将这些描述组织为特定领域的原型体系，而 Runtime Capability 则负责解释并执行这些原型。新的 Schema Family 可以不断建立在已有 Family 之上，通过复用已有能力扩展新的领域，而无需修改 SchemaNode.Core。