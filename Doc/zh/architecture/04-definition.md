# 04. 定义域和公共语义共识

前三章从代码与语义的关系出发，逐步构造了多维语义空间，并由 Meta、Property、Relation、Function 四种语义原语构成了语义实体。

随后，我们进一步引入**原型（Prototype）**，使语义类型获得了稳定的结构和能力边界，并建立了语义与执行层之间的最小契约。

到这里，理论上的推演已经完成。

如前所述，我们无法为所有业务领域定义一组统一的语义原型，但我们可以围绕**原型的定义域**来构建一个**公共语义共识**。

---

## 1. 定义域

定义域是解决“如何在语义空间中定义实体”这一问题的工程领域。按照之前的讨论，它可以分为三个阶段：

1. 如何定义语义原型，以及如何管理语义原型。
2. 如何从语义原型定义语义类型，并对语义类型进行管理。
3. 如何从语义类型构造语义实体。

定义域需要覆盖未知领域的语义原型定义——这是它区别于固定业务模型的关键。

如果定义域只能定义预先确定的几种实体，那么它本质上仍然是一个固定模型，而不是语义语言的定义基础。

另一方面，能够被不同执行层解析和执行的语义，其中间形式必然是结构化数据。因此，**定义域**本身必须覆盖**数据处理领域**。

同时，作为构成语义的基本元素，Property、Relation、Function 也必须能够在定义域中被定义、组合和处理。这意味着定义域不能只是一个“描述工具”，它自身也必须能够被同一套语义机制描述。

因此，定义域最终需要完成自身的自举：

```text
定义原型
    ↓
定义语义类型
    ↓
定义语义实体
    ↓
定义 Property / Relation / Function
    ↓
定义语义本身的处理方式
    ↓
继续定义新的原型与类型
```

这也是定义域与普通元数据系统之间的重要区别。

它不只是描述业务数据，而是描述**如何描述数据本身**。

---

## 2. 语义原型

### 2.1 原型标识符

语义原型用于对实体进行分类，同时也是执行层确定如何解释和执行语义的关键。

执行层需要知道一个实体属于哪个原型，才能确定它对应的 Meta、可以挂载哪些 Property，以及如何加载和处理由该原型定义的语义类型。

因此，无论语义空间还是执行层，都必须对原型存在明确的认知。

所以我们无需关注原型的抽象定义，只需要基于它们可以区分实体，实体所属的原型即可。为了跨语言和跨平台的兼容性，我们采用唯一的字符串标识来代表它。

例如上一章中使用到的`struct`，`enum`，`array`等，但直接使用它们会占据顶层原型名，并不是一个好的实践。

---

### 2.2 原型族

在前面讨论的语义方言中，每种方言都可能定义自己的原型。为了避免不同方言之间产生命名冲突，原型标识符需要具有明确的作用域。

因此，可以类似命名空间一样，为每个定义域或语义方言建立一个顶层原型分类，再在其下定义具体原型，从而形成一个**原型族**。

SchemaNode Core 当前采用 `node` 作为顶层原型名：

```text
node
├── scalar
├── int
├── enum
├── array
├── struct
│   └── field
├── function
│   └── arg
└── ...
```

对应的完整标识符可以表示为：

```text
node.struct
node.enum
node.array
node.struct.field
node.function
node.function.arg
```

这里的 `node` 是 SchemaNode Core 公共定义域所选择的唯一顶层原型名, `node.struct` 则是`node` 原型族下代表结构体数据类型的原型。

语义声明虽然依赖原型提供的 Meta 和 Property 进行解释，但语义声明本身通常并不需要携带所属原型的完整描述。

这意味着，语义声明与具体原型实现之间可以保持相对独立：

```text
语义声明
    │
    ├── Meta
    └── Property
          │
          ↓
      原型解释
```

因此，SchemaNode Core 选择 `node` 作为中性的顶层原型名，也为未来留下了替换空间。

如果将来出现一套更完善、但仍然兼容 `node` 公共语义的定义域实现，那么原有语义声明理论上可以直接由新的原型体系解释，而不需要在语义数据中修改大量与具体实现相关的信息。

这也是公共语义共识与具体定义域实现之间应当保持的边界。

---

### 2.3 如何设计原型

当我们对语义实体进行分类时，就是在定义原型。

原型并不是对某种数据的简单命名，而是规定了这种实体具有什么确定的结构，以及能够承载哪些语义属性。

例如，一个函数参数除了具有名称和类型之外，还可能具有 `Require`、`Variadic` 等语义。

以函数：

```text
add(1, 2, 3, 4)
```

为例，它的语义声明可以类似：

```jsonc
{
  "name": "add",
  "args": [
    {
      "name": "values",
      "type": "int",
      "variadic": true
    }
  ]
}
```

对于 `func.arg` 来说：

```text
Meta
├── name
└── type

Property
└── variadic
```

其中，`name` 和 `type` 是参数结构中确定存在的 Meta，而 `variadic` 是可选的语义 Property。

`variadic` 本身并没有特殊的数据表示。它之所以能够被理解，是因为 `func.arg` 原型定义了 `Variadic` Property，执行层因此知道这个属性代表参数是否为可变参数。

这也是原型存在的意义：

> **原型不是携带语义，而是规定一组语义如何被解释。**

一个数据值只有在被放入确定的语义结构中之后，原本没有特殊意义的数据字段才会获得明确的语义。

因此，设计原型时需要考虑的不是“业务上有哪些对象”，而是：

> **某类语义实体需要具有什么确定的结构，以及它需要允许哪些可组合的语义扩展？**

---

## 3. 语义类型

原型解决的是“如何定义一类语义实体”，语义类型则是在原型基础上进一步形成可以被复用的具体语义定义。

例如：

```text
Prototype
    ↓
node.int
    ↓
   int
    ↓
   age
    ├── unit: '岁'
    └── lowlimit: 0
```

`node.int` 规定整数节点具有怎样的结构和能力；`age` 则是在这个基础上定义出来的一个具体语义类型。

语义类型本身仍然是结构化语义数据，因此它可以继续被组合、继承或扩展，并最终用于构造实际语义实体。


### 3.1 语义类型的执行层承载

从本节开始，我会使用 SchemaNode Core 实现的执行层来展示语义类型的定义与管理。因为这部分是代码层面的语义申明，相比解说要更容易理解，但请注意，这只是 SchemaNode 在对多维语义空间的探索中构建的一种实现。

SchemaNode Core 申明的`node` 原型，用于实现类型的定义与管理。它提供类型的命名空间管理，它关注于类型的组织而非具体的功能。它的申明代码如下：

```C#
// 申明 node 原型，并主动将 Display 属性注册到原型中
[Meta<SchemaKind>("node")]
[Meta<Append>(typeof(Display))]
public sealed class NodeKind;

// 申明 node 原型使用的 Meta，因为它是结构体类型，采用SchemaType注册为结构体语义类型
[Meta<SchemaType>("system.schema.node.schema")]
[Meta<Attach>("node")]  // 为这个结构体指明采用 node 原型，即它可以保存 node 的所有语义属性
public sealed class NodeSchema: PropertyOwner
{
    // 命名空间，通过 SchemaType 进一步限定它使用的语义类型
    [Meta<SchemaType>(typeof(NamespaceType))]
    public string? Namespace { get; set; }
    
    // 类型名称，Identifier限定它的正则表达式为字母、数字或下划线
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
        
    // 数据类型分类，稍后解释
    [Meta<SchemaType>(typeof(NodeKind))]
    public string Kind { get; set; } = null!;
}
```

这样，在配置UI上，只需要一行视图 `<schema-view v-model="nodeSchema" type='system.schema.node.schema' />` 即可生成语义类型的配置界面，而无需预先知道可用的数据类型以及这些类型的实际结构和属性。

![node-schema](./pic/04_node_define.png)

途中 Display 是定义`node`原型时主动注册的，而 Auths 则是App领域方言为 `node` 原型注册的，用于提供权限管理。

上述代码采用**代码即Schema**的设计模式，确保执行层代码和申明在一起定义，无需额外的对齐和注册机制。


### 3.2 命名空间语义类型

虽然 `node` 本身具有 `namespace` 和 `name` 字段，但它本身不算是语义类型，它只是为了类型通用管理提供的结构，它的语义基于 `kind` 字段提供，首先我们需要引入真正的命名空间语义类型。

```C#
[Meta<SchemaKind>("node.namespace")] // 申明 `node.namespace` 原型
[Meta<NodeKind>("namespace")]  // 向 `NodeKind` 申明一个 `namespace` 语义类型分类
[Meta<NodeType>(typeof(RuntimeNamespaceType))] //申明 `namespace` 类型的执行层实现
public sealed class NamespaceKind;
```

注意 `NodeSchema` 中 `kind` 的语义类型对应`NodeKind`，它是一个枚举类型，可以收集所有使用`Meta<NodeKind>(kind)` 注册的语义类型分类。这也是上图中 `kind` 输入框选择项目的由来。

命名空间表明它只是用于类型组织，所以不具有具体的功能，也没有进一步的设置。

---

### 3.3 数据类型

以 `struct` 为例，说明下数据语义类型的定义规则，也实质展示四原语如何完成语义的表达。

```C#
[Meta<SchemaKind>("node.struct")] // 声明 `node.struct` 原型
[Meta<NodeKind>("struct")] // 向 `NodeKind` 申明一个 `struct` 语义类型分类
[Meta<NodeType>(typeof(RuntimeStructType))] // 申明 `struct` 类型的执行层实现
[Meta<SchemaGenerator>(typeof(StructGenerator))] // 申明 `struct` 类型的生成器，基于反射从C#代码生成语义结构体类型
[Meta<Append>(typeof(Generics), typeof(Relations))] // 申明 `struct` 原型具有泛型和关系属性
public sealed class StructKind;

// 申明 `struct` 类型的 Meta 定义结构体
[Meta<SchemaType>($"system.schema.struct.schema")]
[Meta<Attach>("struct")]  // 为这个结构体指明采用 struct 原型，配置和保存都可以处理 struct 类型的语义属性
public sealed class StructSchema : PropertyOwner
{
    // 结构体字段
    public StructFieldSchema[] Fields { get; set; } = [];
}

// 为`node`原型定义 `struct` 属性
[Meta<ForSchema>("node")]  // 申明属性是为了`node`原型定义
[Meta<OfNodeKind>("property")] // 申明该class需要解析为`node`下的`property`语义类型
[Meta<SchemaType>("system.schema.prop.struct.struct")] // 申明该属性注册的语义类型名称
[Relation<Visible, Relation.Call>("struct", "system.logic.eq", $"@{nameof(NodeSchema.Kind)}", "struct")]
public sealed class StructProperty : Property<StructSchema>

// 申明 `node.struct.field` 原型
[Meta<SchemaKind>("node.struct.field")]
[Meta<Append>(typeof(Disable), typeof(Display), typeof(Visible), typeof(Require))]
public sealed class StructFieldKind;

// 申明 `node.struct.field` 原型的 Meta 定义结构体
[Meta<SchemaType>("node.struct.field.schema")]
[Meta<Attach>("node.struct.field")]
public sealed class StructFieldSchema : PropertyOwner
{
    // The field name
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = string.Empty;
    
    // The type name of the node.
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;
}
```

这里最特殊的是`StructProperty`属性，它以`struct`的定义体为值类型，然后挂载到 `node` 原型上,并且它申明了

```C#
[Relation<Visible, Relation.Call>("struct", "system.logic.eq", $"@{nameof(NodeSchema.Kind)}", "struct")]
```

它对应于关联描述:

```jsonc
{
    target: "struct", // 目标是struct属性自身
    property: "Visible",  // 关联的是Visible属性
    call: "system.logic.eq", // 执行采用函数调用，函数是相等判定
    args: [
        { source: "kind" },  // 挂载 `node` 原型附着的结构体上存在 `kind` 字段
        { value: "struct" }  // 实际意思就是当 `kind` 选择结构体时，`struct` 可见
    ]
}
```

![struct](./pic/04_struct_define.png)

这张图中选择`kind`为结构体后，下面的结构体字段(fields), 关系(Relation)等属于`struct`原型的字段和属性才变得可见。

上述结构体的定义相对传统编程，只是将`struct`的显示判定从前端代码转移到了关联描述中，但完成这一步后，前端配置UI变成了执行层，而无需依赖特殊代码实现对`struct`的支持。

当然，这里演示的关联是比较简单的，定义域的关联可以更加复杂。

类似这种形式，SchemaNode Core 提供了 `bool`, `int`, `decimal`, `string`, `date`, `enum`, `struct`, `array`这些结构化数据类型。这些在 `core` 的文档中进一步介绍。

下面是一个简单的结构体示例:

```jsonc
{
  "namespace": "test",
  "name": "minmax",
  "kind": "struct",
  "struct": {
    "fields": [
        {
          "name": "min",
          "type": "system.int",
          "display": "最小值"
        },
        {
          "name": "max",
          "type": "system.int",
          "display": "最大值"
        }
    ],
    "relations": [
      {
        // 将 min 赋值给 max.lowlimit
        "call": {
            "args": [
            {
                "type": "system.int",
                "source": "min"
            }
            ],
            "func": "system.intrinsic.assign<system.int>",
            "mode": "call"
        },
        "kind": "call",
        "target": "max",
        "property": "lowlimit",
      }
    ]
  }
}
```

这里也可以看到，实际的语义申明中并没有`node`相关的特殊标识。虽然 SchemaNode Core 定义了定义域的一个实现，但实质来说，它的修改或者变更都不会影响已经存在的语义申明。


---

### 3.4 属性类型

Property 是构成多维语义空间的重要原语。它申明自己支持的原型，

---

### 3.5 关联类型

当多个语义实体之间存在依赖时，仅仅定义 Property 仍然不足。

Relation 用于描述这些语义之间的关联，并通过 Function 产生确定的语义作用。

例如：

```text
minMax
├── min
└── max
     ↑
     │ lowlimit
     │
    min
```

这里 `max.lowlimit` 并不是一个静态值，而是由 `min` 动态提供。

因此：

```text
Relation
    ↓
Function
    ↓
语义作用
```

共同构成了语义空间中的动态关系。

Relation 本身也可以被定义为语义实体，并具有自己的 Property、类型和执行方式。

这使得关联不再依赖某个业务系统中隐藏的代码逻辑，而成为可以被定义、复用和解释的结构化语义。

---

### 3.6 函数类型

Function 是定义域中用于描述执行语义的基本原语。

函数不仅描述“调用一个方法”，还可以描述参数结构、返回值、调用条件、缓存策略、权限要求以及其他执行相关语义。

例如函数参数可以通过 `node.function.arg` 进行结构化定义：

```text
node.function
    └── node.function.arg
```

函数参数因此不再是执行语言中一个特殊的、无法进一步描述的参数列表，而是定义域中的语义实体。

这使得 Function 自身也可以继续被 Property、Relation 等语义机制描述。

最终，函数不再是定义域之外的一块黑盒执行代码，而成为语义空间中可以被理解和组合的一部分。

---

## 4. 公共语义共识

定义域解决的是如何定义和处理语义，而公共语义共识解决的是：

> **不同语义执行层之间，哪些定义能力必须能够被共同理解。**

它并不是一个固定的业务模型，也不是试图为所有领域规定统一的业务语义。

公共语义共识更接近一个**定义域的公共基础**。

它至少需要覆盖：

```text
语义原型
    ↓
语义类型
    ↓
结构与数据
    ↓
Property
    ↓
Relation
    ↓
Function
    ↓
定义与处理
```

因此，公共语义共识同时覆盖两个方面：

1. **数据处理域**

   语义定义本身是结构化数据，因此需要能够定义、读取、修改、组合和处理这些数据。

2. **语义原语**

   Meta、Property、Relation、Function 必须能够被定义域自身描述和处理。

这两个部分共同构成了语义语言能够自我描述、自我扩展的基础。

### 4.1 SchemaNode 的实现

SchemaNode Core 对这个公共定义域给出了一种具体实现。

其核心不是定义某个固定业务模型，而是提供一组能够继续构造语义的基础 Prototype、Node kind、Property、Relation 和 Function。

其中，`node` 是 Core 当前公共语义定义域所使用的顶级 Prototype：

```text
node
├── scalar
├── enum
├── struct
│   └── field
├── array
├── function
│   └── arg
└── ...
```

这些定义进一步构成 SchemaNode 自身的 System Schema。

System Schema 并不是一套业务 Schema，而是执行层能够理解和处理哪些语义的公开定义。

因此可以把 SchemaNode 当前的关系表示为：

```text
公共定义域
     │
     ↓
System Schema
     │
     ↓
Prototype / Node kind
     │
     ↓
语义类型
     │
     ↓
语义实体
     │
     ↓
执行层
```

执行层不需要预先知道所有业务类型。

它只需要理解公共定义域中的 Prototype 以及这些 Prototype 所规定的语义处理能力，就能够继续解释由这些能力构造出来的未知语义类型。

这也是定义域能够支持未知领域的关键。

---

### 4.2 定义域本身也是语义

到这里可以看到，SchemaNode 所实现的并不是一个“描述业务 Schema 的工具”。

它首先需要解决的是：

> **如何定义 Schema。**

而 Schema 本身又是结构化语义数据，因此定义 Schema 的语言也必须能够被 Schema 描述。

最终形成：

```text
定义语言
   ↓
定义原型
   ↓
定义类型
   ↓
定义语义
   ↓
定义语言自身
```

这就是定义域的自举。

一旦这个闭环成立，公共语义就不再依赖一套不可扩展的固定元模型，而可以在同一套机制中继续产生新的语义类型和新的语义方言。

---

### 4.3 从公共语义到语义方言

公共语义共识并不意味着所有领域都使用相同的业务模型。

它只规定构造和处理语义所需要的公共基础。

在此基础上，不同领域可以使用自己的语义原型、Property、Relation 和 Function，对公共能力进行组合和扩展，从而形成不同的语义方言。

因此：

```text
公共定义域
      │
      ↓
公共语义共识
      │
 ┌────┼─────┐
 ↓    ↓     ↓
 App  ETL  其他领域
 ↓
领域语义方言
```

SchemaNode Core 负责的是定义域和公共语义基础，而 App 则是建立在这些基础之上的一个具体语义方言。

App 中的实体、权限、数据源、视图、ETL、Workflow 等，并不是 SchemaNode 公共语义本身，而是公共语义能力在特定领域中的组织方式和使用方式。

因此，**方言即语言的具体领域实现，而定义域则是构造这些语言所共同依赖的基础。**

---

## 5. 尚未解决的问题：Prototype 与协议

当前实现中，Prototype 负责确定语义定义，而执行层 API 负责提供这些语义定义的获取与执行入口。

Prototype 与 API 之间存在明确的对应关系，但这种对应关系目前仍然由执行层管理，并没有成为 Prototype 本身的一部分。

这是有意保留的边界。

如果未来语义需要跨执行层、跨系统乃至跨网络传播，那么还需要进一步讨论：

> Prototype 是否应该成为协议描述的一部分？

HTTP、MCP 等协议可以解决不同执行节点之间如何通信的问题，但它们并不直接解决不同节点如何理解同一套语义定义的问题。

因此，未来可能还需要在：

```text
语义定义
    ↓
Prototype
    ↓
API
    ↓
通信协议
```

之间进一步建立公共的语义协议。

这已经超出了当前定义域的范围，将留到后续关于语义网络协议的讨论中。
