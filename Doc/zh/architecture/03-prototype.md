# 03. 语义原型: 和执行层的最小契约

前一章中，我们看到，一个完整的语义实体并不是一组孤立的字段。

它可以通过结构组合形成实体，通过 Property 增加语义维度，通过 Relation 建立语义关联，再通过 Function 产生可执行的语义作用。

但如果每一个实体都重新定义这些语义，语义空间仍然无法真正复用。

我们需要先定义实体类型。类似代码中使用 `class` 定义新类型一样，我们首先需要解决的问题，是如何定义这个 `class` 本身。

因此还需要一个更基础的概念：

> **Prototype（原型）是由语义原语构造出的、可以被复用和继续扩展的语义类型定义。**

在 SchemaNode 中，类型并不是最初存在的。

**先有原型，才有类型；有了类型，数据才可以使用类型所提供的语义。**

```text
                Prototype
                    │
          ┌─────────┼─────────┐
          ↓         ↓         ↓
       Property  Relation  Function
          │         │         │
          └─────────┼─────────┘
                    ↓
                   Type
                    │
          ┌─────────┼─────────┐
          ↓         ↓         ↓
       数据结构    静态语义    动态语义
          │         │         │
          └─────────┼─────────┘
                    ↓
                   Data
```

因此，SchemaNode 中的 Property、Relation 和 Function，并不是散落在类型和数据上的配置，而是围绕原型构造和注册的语义能力。

类型为数据提供默认的静态语义；数据使用这些语义，并在运行过程中通过 Relation 和 Function 产生动态语义。

传统面向对象中，Class 是 Object 的原型，开发者通过 Class 定义对象的结构和行为。

而在 SchemaNode 中，类型系统本身也需要被抽象。因此我们引入 Prototype 作为 Type 的原型，由此构成了一个完整的三级结构：

```text
Prototype (原型)  —— 定义“类型”如何被构造（如 struct、scalar、function）
      ↓
Type (类型)       —— 使用原型构造出的具体业务语义类型（如 address、minMax）
      ↓
Data (对象/数据)  —— 运行时承载具体业务数据、产生动态语义的实例
```

类不是对象的终极形态，原型才是类的真正起点。Prototype 成为了语义层与执行层之间最稳定、最小化的契约。

---

## 1. 原型即实体分类

原型首先是对实体本质的分类。

例如，对于整数来说，自然数、整数、年份、月份等都可以进一步形成不同的语义类型。但无论为这些类型附加多少语义，它们的数据结构本质上仍然是整数。

因此：

> **原型定义了类型的 Meta。**

例如，`struct` 原型规定结构体类型的 Meta 是：

```jsonc
{
  "fields": {
    name: string,
    type: value_type,
  }[]
}
```

也就是说，一个实体如果属于 `struct` 原型，就具有由字段列表描述自身结构的方式。

原型不仅决定实体如何被解释，也决定附着在实体上的 Property 应该如何解释。

例如，一个名为 `uplimit` 的属性：

```text
uplimit = 10
```

对于整数类型，它表示`最大值`；对于字符串类型，它可能表示`最大长度`。

属性值本身并不能决定完整语义。

属性需要声明自己服务于哪些原型，执行层才能根据实体所属的原型解释它。

```text
             Property
                 │
          声明服务于原型
                 │
       ┌─────────┴─────────┐
       ↓                   ↓
      int                string
       │                   │
   uplimit=10          uplimit=10
       │                   │
     最大值              最大长度
```

因此，同名 Property 服务于不同 Prototype 是允许的：

```text
int.uplimit
string.uplimit
```

它们名字相同，但语义解释由所属原型决定。

而同一个 Prototype 内部，则不允许出现两个同名 Property，因为它们无法形成明确的语义定义。

为了在整个实体空间中区分不同原型，每个 Prototype 需要具有唯一标识符。

这种标识方式与代码中的类型名称类似，使用唯一字符串即可：

```text
int
string
enum
struct
workflow
...
```

只要能够在原型空间中唯一确定其身份即可。

因此，Prototype 首先解决的是：

> **一个实体属于什么基本类别，以及这种类别应该如何被解释。**

---

## 2. 原型关联语义属性

确定实体属于什么原型之后，还需要描述这个原型可以具有什么语义。

一个 Prototype 可以声明多个 Property。

只有原型已经声明的 Property，才能在它派生出的类型中使用。

因此：

```text
Prototype
   │
   ├── Property A
   ├── Property B
   ├── Property C
   └── ...
          ↓
        Type
          ↓
        Data
```

Property 因此成为实体可以拥有的语义维度。

这也意味着，Property 并不是一个全局无限开放的键值表。

同名 Property 服务于不同 Prototype 是允许的，但同一个 Prototype 内部需要保持唯一。

一个 Property 也可以同时服务于多个 Prototype。

例如 `Relations` 属性描述的是实体上保存的关联定义，它并不属于某一种具体数据结构，而可以服务于多种原型。

```text
Relations
   │
   ├── struct
   ├── array
   ├── function
   └── ...
```

对于 `function` 而言，它甚至可以用于描述函数参数在被使用时具有什么语义关联。

因此，从“挂载属性”的角度来看，如果一个定义除了自身的数据结构之外，还需要更多可组合的语义描述，那么就可以将这种定义本身抽象为一个原型。

例如，`struct` 的 Meta 是：

```json
{
  "fields": []
}
```

其中一个 field 的语义描述可以是：

```jsonc
{
  // Meta
  "name": "age",
  "type": "int",

  // Property
  "display": "年龄",
  "lowlimit": 0,
  "uplimit": 200,
  "unit": "岁"
}
```

这里的 `field` 已经不只是一个名称和类型。

它在 `int` 类型的基础上，又携带了更多 Property 作为自身的语义描述。

因此，结构体字段本身也可以作为一个语义挂载点：

```text
struct
  │
  └── field
       │
       ├── Meta
       └── Property
```

于是可以为它定义 `struct.field` 原型。

函数参数也是类似的情况：

```text
function
  │
  └── arg
       │
       ├── Meta
       └── Property
```

因此可以定义 `function.arg` 原型。

这种方式并不是为了增加抽象层次，而是因为这些结构本身已经具有独立的语义维度，需要能够挂载 Property。

---

## 3. 原型不定义 Relation 机制

Prototype 负责定义实体的基本类别，以及这个类别可以挂载哪些语义属性。

它并不需要为每一种 Prototype 单独定义一套 Relation 机制。

如果一个类型需要建立关联，只需要拥有 `Relations` 这样的 Property 即可。

```text
Prototype
    │
    └── Relations
           │
           ↓
       Relation
           │
           ↓
      语义空间中的实体
```

因此：

> **Relation 是语义空间的行为，而不是某个特定 Prototype 的内部机制。**

原型只需要定义：

> 这种类型的实体支持`Relations`属性。

至于这些关联连接到什么实体、如何产生作用，则属于语义空间本身。

这也解释了为什么同一种 Relation 机制可以跨越不同类型的实体。

例如：

```text
struct
   │
   └── Relations
          ↓
       Relation
          ↓
function
   │
   └── Relations
          ↓
       Relation
```

原型提供的是统一的语义承载能力，而不是为每一种实体重新发明关联机制。

---

## 4. Function、Relation、Property 也是语义实体

在前面的讨论中，Property、Relation 和 Function 被作为构造语义的基本原语。

但它们本身也不是不可描述的黑盒。

函数、关联和属性本身同样是语义实体，也可以具有自己的属性和类型。

### Function

函数可以声明自身的执行约束。

例如：

* ServerOnly：仅允许在服务器执行；
* NoCache：客户端不应缓存该函数的数据；

函数参数与结构体字段具有类似的语义结构，同样可以拥有多种描述。

* Require：参数必须提供；
* Default：参数具有默认值；
* Variadic：参数数量可以变化；
* Display：函数或参数的显示语义。

因此，一个 Function 并不是：

函数名 + 一段代码

而是一个可以被语义系统完整描述的实体：

```text
Function
 ├── Meta
 ├── Property
 └── Arguments
       ├── Meta
       └── Property
```

这使得函数的调用约束也可以从函数实现内部的隐式判断，提升为可以被语义系统直接理解和处理的定义。

### Relation

关联的执行方式同样不是固定的。

SchemaNode Core 默认提供几种基本的 Relation 执行方式，例如：

* assign
    > 直接将来源值赋给目标属性

* call
    > 通过 Function 执行关联

* any
   > 调用多个 Function，
   > 任意一个返回 true 即满足关联

这些只是系统提供的基础语义。

执行层或其他语义方言仍然可以根据自身需求扩展 Relation 的执行方式。

由于这些扩展本身也是通过语义定义完成的，因此它们不需要成为语义编辑器中的特殊功能。只要语义被定义出来，语义配置器就可以按照定义呈现它。

### Property

Property 本身也可以具有描述自身行为的 Property。

例如：

* Static 
   > 表示该 Property 的值是静态定义，不允许被 Relation 在运行时修改。
   > 当配置一个 Relation 时，目标实体上属于 Static 的 Property 不会作为可选项目出现。

这里形成了一个重要的递归：

```text
Property
   │
   └── Property
          │
          └── 描述 Property 自身的语义行为
```

Property 不再只是一个简单的键值，而成为可以被语义系统自身描述和处理的语义实体。

Property 还具有明确的值类型。

例如，一个 Display Property 需要字符串，一个 Static Property 是布尔值，而某些 Property 的值本身又可以是结构化语义。

其中比较特殊的是函数调用。

函数调用本身可以作为一种语义值，成为语义实体获得动态能力的重要来源。后续章节中会通过具体的语义类型看到它如何参与 Relation、Property 以及其他语义定义。

因此，Function、Relation 和 Property 并不是处于语义系统之外的特殊机制。

它们本身也处于同一个语义空间中：

```text
      Semantic Entity
             │
 ┌───────────┼─────────┐
 ↓           ↓         ↓
Property  Relation   Function
 │           │         │
 └───────────┼─────────┘
             ↓
        可以继续被描述
```

具体到实际的类型定义形式，将在后续的语义类型实现章节中展开。

---

## 5. 从 Prototype 到 Type

Prototype 本身不是最终被业务数据直接使用的实体。它只定义了一个语义类型具有的结构和允许挂载的属性。

在此基础上，可以进一步定义具体 Type。

```text
Prototype
    ↓
类型定义
    ↓
Type
    ↓
Data
```

例如，`int` 可以作为一个原型所对应的类型基础。

在这个基础上，还可以继续定义更具体的类型：

```text
int
 │
 ├── natural
 ├───── lowlimit = 0
 |
 ├── year
 ├───── lowlimit = 1900
 |
 ├── month
 ├───── lowlimit = 1
 ├───── uplimit = 12
 └── ...
```

这些类型仍然保持整数的数据结构，但可以继续增加自己的语义 Property、Relation。

因此，类型系统不是一个预先固定的列表。

> **新的类型可以继续基于已有 Prototype 构造。**

这也是 Prototype 存在的真正意义：

它把“如何构造一种类型”从“类型本身”中抽离出来，使类型系统具备持续扩展的能力。内核允许第三方定义新的原型和相关属性，而无需修改内核自身。

---

## 6. 类型与数据

有了 Type，数据才有了可以依附的语义定义。

类型为数据提供默认的结构和静态语义：

```text
Type
 │
 ├── Meta
 ├── Property
 ├── Relation
 └── Function
        │
        ↓
      Data
```

例如一个类型可以定义：

```text
display = "年龄"
lowlimit = 0
uplimit = 200
unit = "岁"
```

数据使用这个类型后，不需要重新定义这些语义。

因此：

> **类型提供默认语义，数据使用类型语义。**

而运行时，数据还可以通过 Relation 和 Function 产生动态语义。

例如：

```text
Data A
   │
 Relation
   ↓
Data B.Property
```

因此可以将二者区分为：

```text
Type
 └── 定义静态语义

Data
 ├── 使用类型提供的静态语义
 └── 在运行时产生动态语义
```

这也是 SchemaNode 中“类型”与普通数据结构模板之间的重要区别。

类型不只是规定数据应该长什么样，它同时规定了数据可以具有什么语义能力。

---

## 7. Prototype 是执行层的最小契约

语义类型本身对于具体执行层而言并不需要被预先认识。

执行层真正需要知道的是：

> **这个类型属于什么 Prototype。**

因为 Prototype 决定了两个最基本的问题：

```text
Prototype
    │
    ├── Meta
    │      ↓
    │   如何加载和解释结构
    │
    └── Property
           ↓
       如何消费语义
```

例如，对于 `struct` 原型，某个执行层可以定义对应的运行时类型：

```text
struct
  ↓
StructType
```

执行层通过类型申明中的 `fields` 获取字段声明。

而字段本身又可以属于：

```text
struct.field
      ↓
StructFieldType
```

于是一个字段可以被加载为运行时对象，而字段上声明的 Property 则进一步决定它的执行行为。

例如在前端执行层，这个字段可以展示为年龄输入框，限制了最小最大值，并且后面显示`岁`表示单位：

```text
field
 ├── type = int
 ├── lowlimit = 0
 ├── uplimit = 200
 ├── unit = '岁'
 └── display = '年龄'
```


执行层并不需要知道某一个具体业务类型是什么，只需要知道：

```text
这是一个 struct 类型
这是一个 struct.field
这个字段具有 lowlimit Property
```

于是，它就可以消费所有属于自己支持的 Prototype 的语义类型。

```text
             Semantic Types
          ┌───────┼────────┐
          ↓       ↓        ↓
        Type A  Type B   Type C
          │       │        │
          └───────┼────────┘
                  ↓
              Prototype
                  ↓
          Execution Layer
```

因此，Prototype 成为了**语义层与执行层之间最小而稳定的契约**。

语义层可以不断基于 Prototype 扩展新的类型，而执行层只需要实现自己支持的 Prototype，就能够消费这一整类语义类型。

---

## 8. 原型申明的Meta本身也是结构化语义

无论是数据类型，还是 Function 这样的功能类型，它们的 Meta 本身都是结构化数据。

例如，一个结构体类型可以通过：

```json
{
  "fields": []
}
```

描述其基本结构。

但 fields 并不只是一个普通的数据数组。

其中每一个 field 还有自己的 Meta、Property 和其他语义定义，因此它实际上是一套完整的定义结构。

这意味着：

原型的 Meta 本身也是一种语义类型。

通常，这类结构可以使用 struct 原型进行描述。

于是：

```text
Prototype
   │
   └── Meta
        │
        ↓
      struct
        │
        ├── fields
        ├── Property
        └── Relation
```

这里产生了一个重要的递归关系：

```text
用 Prototype 定义 Type
       ↓
Type 的 Meta 本身也是结构化数据
       ↓
这种结构化数据也可以由 Type 描述
```

因此，SchemaNode 的语义编辑器并不需要为“定义原型”准备一套完全特殊的编辑机制。

原型定义本身也是一种结构化语义。

对于编辑器而言，它与填写一个普通的结构化实体并没有本质区别：

```text
原型定义
    ↓
结构化数据
    ↓
按照对应 Type 编辑
```

这也是 SchemaNode 能够逐步实现自举的基础之一：

描述类型的语言，本身也可以成为被描述的类型。

---

## 9. 从 Prototype 到 Core 类型

Prototype 解决的是：

> **类型如何被构造。**

而哪些 Prototype 和 Type 已经由 SchemaNode Core 提供，则是另一个问题。

Core 会在原型机制之上提供一组公共 Node 类型，并通过命名空间和类型注册机制组织它们。

这些具体类型包括不同的数据结构和语义承载方式。

下一章将进入这一层，介绍：

* Core 提供的 Node 类型族；
* 类型如何注册到命名空间；
* `scalar`、`enum`、`struct` 等具体类型；
* 一个具体类型完整的定义结构；
* 以及这些类型如何最终被数据使用。

因此，本章留下的是**类型系统的构造机制**，而下一章开始介绍**SchemaNode 已经提供的具体类型系统**。

```text
Prototype
    ↓
定义类型的能力
    ↓
Core Type System
    ↓
具体 Type
    ↓
Data
```

**Prototype 让类型成为一种可以继续构造和扩展的语义定义，而不是一个预先固定的名称集合。**


## 结语

Prototype 解决的是“语义类型如何被定义和复用”的问题，但它并没有规定具体的语言实现。

接下来需要解决的问题是：如果要真正构造这样一个可执行的语义语言，哪些 Prototype、Property、Relation 和 Function 应当成为公共语义基础？SchemaNode 对此给出了一种具体实现。