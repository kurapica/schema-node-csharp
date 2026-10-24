# 03. 标量原型

`Schema/Scalar/` 目录下定义了标量类型相关的原型，包括 `node.bool`（布尔类型）、`node.int`（整数类型）、`node.decimal`（小数类型）、`node.string`（字符串类型）和 `node.date`（日期类型）。

这些原型用于定义基本数据类型的语义结构。

它们的定义结构与上一章介绍的 `IntSchema` 相同，都支持通过 `base` 字段指定基础类型。例如，`system.year` 的基础类型是 `system.int`。通过这种方式，语义类型可以沿继承链复用基础类型的语义属性。

上一章已经介绍了 `int` 分类如何挂载到 `node` 原型上，本章不再重复这一过程，而是进一步介绍公共标量语义类型的声明，以及它们如何通过语义属性构建类型约束。

## 系统语义类型的声明

系统定义的公共标量语义类型可以在 `Scalar` 目录下找到。下面以整数类型 `Int` 为例：

```csharp
/// <summary>
/// Represents the int scalar value type (root of the int type family)
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_INT)]
[Meta<ClrEquivalent>(typeof(int))]
[Meta<ClrEquivalent>(typeof(short))]
[Meta<ClrEquivalent>(typeof(sbyte))]
[Meta<ClrEquivalent>(typeof(byte))]
[Meta<ClrEquivalent>(typeof(ushort))]
[Meta<ClrEquivalent>(typeof(uint))]
[Meta<ClrEquivalent>(typeof(ulong))]
[Meta<ClrEquivalent>(typeof(Int64))]
[Meta<ClrEquivalent>(typeof(UInt16))]
[Meta<OfNodeKind>(NODE_KIND_INT)]
public class Int : Number, IScalarType<long>;
```

这里，`OfNodeKind` 将 `Int` 声明为 `node.int` 分类下的语义类型。多个 `ClrEquivalent` 元数据则声明了与 `system.int` 对应的 C# 类型。在基于反射注册语义类型时，这些 CLR 类型会映射到 `system.int`，从而使宿主语言中的多种整数类型能够使用统一的语义类型表达，`ClrEquivalent`个属性是C#执行层内部用的，并不对外暴露。

`IScalarType<long>` 则表明，`Int` 对应的标量处理类型为 C# 的 `long`。在处理该语义类型的数据时，执行层可以基于这一约定进行相应的类型转换与处理。

通过这种方式，语义层的类型声明与执行层的类型处理建立了明确的映射关系。在基于 C# 声明类型和函数时，开发者不必在每处代码中重复描述这些类型之间的对应关系。

## 字符串类型与命名空间名称类型

在标量原型中，字符串类型的声明相对简单：

```csharp
[Meta<SchemaType>(NS_SYSTEM_STRING)]
[Meta<OfNodeKind>(NODE_KIND_STRING)]
public class String : IScalarType<string>;
```

它将 `system.string` 声明为 `node.string` 分类下的公共语义类型，并指定其对应的 CLR 标量类型为 `string`。

字符串不仅用于表示文本，也经常用于引用其他语义类型。例如，`IntSchema` 中的 `base` 字段用于指定整数类型的基础类型：

```csharp
[Meta<SchemaType>(typeof(IntType))]
public override string? Base { get; set; }
```

虽然该字段在 C# 中声明为 `string?`，但它在语义上使用的是 `IntType`，而不是普通的字符串类型。这是因为该字段需要表达的不是任意文本，而是对特定语义类型的引用。

继续查看 `IntType` 及其基类，可以看到以下继承链：

```csharp
// Int type
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, NODE_SELF, false, NODE_KIND_INT)]
public class IntType : ValueType;

// Value type
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.valuetype")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, NODE_SELF)]
public class ValueType : AnyType;

// Any type
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.type")]
[Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.gettypeentries)}", NODE_SELF)]
public class AnyType : String;
```

这条继承链展示了如何通过语义属性逐层扩展类型定义。

`AnyType` 表示任意语义类型。它通过 `EntrySource` 提供级联选择的数据源，使定义域能够从系统中选择语义类型；同时继承 `String` 的基础语义。

`ValueType` 在 `AnyType` 的基础上增加了 `Valid` 约束，用于验证所选语义类型是否属于 `node.value` 分类。它继承了 `AnyType` 的选择能力，无需重复声明。

`IntType` 则进一步增加 `Valid` 约束，将可选范围收窄到 `node.int` 分类。

由此可见，语义类型的定义不必把所有属性和约束集中写在一个类型中，而可以通过继承逐层组合：基础类型提供通用能力，派生类型增加更具体的约束。

**标量语义类型正是通过原型、类型映射、继承和语义属性的组合，建立了从类型声明到执行处理的统一约定。**

不过，AnyType 这些类型是定义域的语义类型，并非公共语义类型。这里说明是对标量语义类型的导读。

因为标量类型非常简单，这里就不再展开说明了。