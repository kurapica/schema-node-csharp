# 09. 关系原型

关系不是独立存在的，它通过定义`Relations`属性来承载一组关系。而类似`node.struct`, `node.array`, `node.function`这样的原型申明具有它，来使用关系这种功能。

当我们基于Meta和Property描述实体时，我们得到的是静态的语义实体快照。而当我们用关系将实体数据和其它的实体的属性关联时，我们得到的就是实时运作的语义空间。

这里特别设计仅允许`data-relation->property`这个模式，是必须避免循环依赖的产生。如果一个实体的数据只是根据另一个或者多个实体给出，那么我们可以采用为`default`属性写入值来表达。

综上来说，关系本身的设计是原型无关的，不过它基于实体提供的数据访问路径来工作。它的形式如下:

```json
"relations": [
  {
    "target": "max",
    "property": "lowlimit"
    "kind": "call",
    "call": {
        "args": [
        {
            "type": "system.int",
            "source": "min"
        }
        ],
        "func": "system.intrinsic.assign<system.int>",
        "mode": "call"
    }
  }
]
```

这是测试中将最小值作为最大值的下限使用的关联。稍后和关系的执行过程一起讨论。

相关实现主要位于：

- `Schema/RelationSchema.cs`：关系定义体；
- `Relation/`：关系执行过程；
- `Runtime/Type/RelationType.cs`：关系的运行时解析与执行；
- `Attribute/RelationAttribute.cs`：C# 声明语法。
- `Relation/AnyProcesscs`, any 处理的申明。
- `Relation/CallProcesscs`, call 处理的申明。
- `Relation/AssignProcesscs`, assign 处理的申明。

## 1. 关系定义

关系定义体 `RelationSchema` 描述关系的目标、写入属性和关系种类。

| 字段 | 含义 |
| --- | --- |
| `Target` | 关系作用的目标，可以是类型名或语义访问路径 |
| `Property` | 关系要写入的属性名 |
| `Kind` | 关系种类 |
| `TargetType`、`TargetKind` | 从目标推导出的类型及其 Schema 种类 |
| `OwnerKind` | 关系拥有者的 Schema 种类 |
| `ValueType` | 根据目标和属性信息推导出的值类型 |

其中，`Target`、`Property` 和 `Kind` 是关系的主要声明信息，其余字段由关系定义自身的语义推导得到，即它们是不保存的。

之前例子中的关联的核心就是

```json
{
    "target": "max",
    "property": "lowlimit"
    "kind": "call",
}
```

它指明了关系的作用目标是 `max`，写入属性是 `lowlimit`，关系种类是 `call`。因为它作用在结构体中，它的`Kind`会被推导为`node.struct.field`(基于原型加载属性时指定)，`TargetKind` 则基于`max`的类型推导为`node.int`，这样`lowlimit`就可以被解析为`system.schema.node.int.lowlimit`属性，即整数用下限属性。

注意这个解析是执行层完成的，语义申明本身不需要含有。所以具体语义属性类型是哪个，也是执行层确定即可。

因为作用于结构体，所以结构体访问`max`路径自然拿到它的`max`字段，关系自身并不管`target`是否存在，它基于`IValueAccess`接口来访问目标值并应用关系。


## 2. 三种关系种类

关系的 `Kind` 决定结果如何产生。目前定义了三种关系：

| 种类 | 作用 | 典型用途 |
| --- | --- | --- |
| `assign` | 返回常量值 | 固定默认值、标记属性 |
| `call` | 调用函数生成结果 | 动态默认值、可见性条件、类型覆盖、校验 |
| `any` | 描述多个候选函数 | 任意函数返回true，结果就为true |

`call`是比较常用的，因为基于调用函数的参数将其它实体的数据作为输入，产生了关联关系。

现在看上面的例子中：

```json
"call": {
    "func": "system.intrinsic.assign<system.int>",
    "mode": "call"
    "args": [
      {
        "type": "system.int",
        "source": "min"
      }
    ],
}
```

这就是一个典型的函数调用，它的参数只有一个，即`min`访问路径，在结构体中表示`min`字段。

其它两类不多做介绍，因为配置时，很容易理解。

注意关系的执行并非固定的，和注册`struct`到`node`一样，可以无限制的扩展关系的执行方式，这点参考`CallProcess.cs`即可。


## 3. C# 声明语法

C# 通过泛型特性 `RelationAttribute<TP, TR>` 声明关系：

- `TP` 指定关系写入的属性类型，例如 `Require`、`Display` 或 `Visible`；
- `TR` 指定关系种类，例如 `Assign` 或 `Call`。

例如：

```csharp
[Relation<Require, Assign>(nameof(Name), true)]
```

表示将当前对象的 `require` 属性设为 `true`。

用`Relation`赋值和`Meta`属性赋值的区别在于:`Meta`仅仅是直接赋予属性，而`Relation`会记录属性的提供者。特别配置`EntrySource`时，往往需要访问其它字段，只有能定位属性的提供者，才能确保关系中对其它路径的访问能正常完成。

```csharp
[Relation<Display, Call>(
    NODE_SELF,
    "example.getDisplay",
    "@name")]
```

表示调用 `example.getDisplay`，从 `name` 路径读取参数，并将结果写入当前对象的 `display` 属性。

目标属性名由属性类型推导，关系种类也由对应类型的元数据确定。这样，C# 声明与 Schema 中的关系定义使用同一套语义结构。

## 4. 运行时处理

`RelationType` 是关系的运行时表示，由节点类型在加载过程中使用，而不是一种独立的节点类型。

加载阶段主要完成三件事：

1. 根据关系拥有者的 Schema 种类解析目标属性类型；
2. 根据 `Kind` 找到并加载对应的关系执行过程；
3. 建立函数等依赖引用，并检查目标路径和函数是否有效。

执行时，`RelationType` 创建目标属性实例，调用关系过程得到结果，再将结果写入属性，最终返回 `IProperty`。

整个过程可以概括为：

`RelationSchema → RelationType → IRelationProcess → IProperty`

关系只负责生成属性结果。结果最终合并到定义体、字段或使用形式的哪个属性集合中，由调用方决定。

C#后端通常只处理和`IConstraintProperty`属性相关的关系，用于校验。不过App项目中，会针对标注了`DisplayOnly`属性的字段，处理它的`Default`相关的关系，这往往可以用来追踪表之间的依赖关联，当然具体在App层说明。


## 5. 小结

关系原型将语义推导统一为三个核心要素：**目标、属性和关系种类**。定义体负责描述关系，运行时负责解析和加载，具体关系过程负责生成结果。

它与属性、函数及其他原型协同，使默认值、可见性、类型覆盖、校验和候选来源等不同语义能够通过统一机制组合起来。

因此，关系原型并不是又一种业务对象，而是 SchemaNode 用于连接已有语义、形成派生语义的通用机制。