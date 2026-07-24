# SchemaNode.Core — Feature Guide

Practical examples for working with SchemaNode.Core's four pillars.

---

## 1. Defining a Schema Kind with Meta

Use `[Meta<T>]` attributes to declare a new schema kind:

```csharp
// Define a new struct type
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeType>(typeof(RuntimeStructType))]
[Meta<SchemaGenerator>(typeof(StructGenerator))]
[Meta<SchemaType>("system.schema.struct.schema")]
[Meta<Attach>(SCHEMA_KIND_STRUCT)]
public sealed class StructSchema : ExtensibleSchema
{
    public StructFieldSchema[] Fields { get; set; } = [];
}
```

The key Meta declarations:
- `SchemaKind` — registers the kind in the global registry
- `NodeSchemaKind` / `ValueSchemaKind` — categorizes the kind within the Node family
- `NodeType` — maps to the C# runtime type
- `SchemaGenerator` — specifies how to generate this schema from C# code
- `SchemaType` — the schema's own type name (self-describing)
- `Attach` — inherits properties from another schema kind

---

## 2. Creating Custom Properties

### Basic Property

```csharp
// Define a custom display property
[Meta<ForSchema>("struct")]
[Meta<OfSchema>("property")]
public sealed class MyCustomDisplay : Property<string>
{
    // Value is inherited from Property<T>.Value
}
```

### Constraint Property (Validation)

```csharp
public sealed class RangeLimit : ConstraintProperty<(double Min, double Max)>
{
    public override async Task ValidateAsync(
        SchemaContext context, DataNode node, IValueTypeAccess? access)
    {
        if (node is NumericNode numNode && numNode.Value is double val)
        {
            if (val < Value.Min || val > Value.Max)
                node.SetViolated($"Value {val} outside range [{Value.Min}, {Value.Max}]");
        }
    }
}
```

### Usage

```csharp
// Apply to a struct field
[Meta<RangeLimit>((0, 100))]
public double Score { get; set; }
```

---

## 3. Defining Relations

### Assign — Static Assignment

```csharp
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, "$Kind", "assign")]
public class AssignProperty : Property<Assign>;
```

### Call — Dynamic Computation

```csharp
// Look up manager by store ID
[Relation<Display>("lookup_manager", "$store_id")]
public string ManagerName { get; set; }
```

The relation system resolves `$store_id` from the current data context and calls `lookup_manager` to compute the display value.

### Custom Relation Process

```csharp
public class MyCustomRelation : IRelationProcess
{
    public Task LoadAsync(SchemaContext context, IValueTypeAccess owner)
    {
        // Validate configuration at load time
        return Task.CompletedTask;
    }

    public async Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner)
    {
        // Compute value based on current data
        var data = owner.GetAccessValue("someField");
        return await ComputeResult(data);
    }
}
```

---

## 4. Defining Functions

### Atomic Function (C# Registration)

```csharp
public static class MyBusinessFunctions
{
    [Function("calc_tax", "myapp.calc_tax", "decimal", "decimal", "decimal")]
    public static decimal CalculateTax(decimal amount, decimal rate)
        => amount * rate;
}
```

The `[Function]` attribute declares: name, full namespace, return type, and parameter types.

### Semantic Function (Schema-Defined)

```json
{
  "kind": "function",
  "name": "is_eligible",
  "return": "bool",
  "args": [
    { "name": "user", "schemaType": "myapp.user" },
    { "name": "threshold", "schemaType": "int" }
  ],
  "exps": [
    { "func": "system.logic.and", "args": [
      { "func": "system.logic.and", "args": [
        { "field": "active" },
        { "func": "system.logic.ge", "args": [
          { "field": "score" },
          { "arg": "threshold" }
        ]}
      ]},
      { "func": "system.logic.not", "args": [
        { "field": "blocked" }
      ]}
    ]}
  ]
}
```

This is equivalent to: `user.active && user.score >= threshold && !user.blocked`

---

## 5. Implementing a Custom CompileContext

The CompileContext system allows different interpretations of the same function:

```csharp
public class MyCompileContext : CompileContext
{
    public override async Task<SchemaExp> VisitSchemaExpAsync(SchemaExp exp)
    {
        // Transform the expression tree for your target
        if (exp is FuncCallExp { Function: "system.logic.eq" } eqExp)
        {
            // Replace equality with target-specific representation
            return new MyTargetEqualityExp(eqExp.Args[0], eqExp.Args[1]);
        }
        return await base.VisitSchemaExpAsync(exp);
    }

    public override Task<Expression> CompileSchemaExpAsync(
        SchemaExp exp, Type? expectedType = null)
    {
        // Compile to target expression tree
        return exp switch
        {
            MyTargetEqualityExp eq => /* emit target code */,
            _ => base.CompileSchemaExpAsync(exp, expectedType)
        };
    }
}
```

---

## 6. Working with the Runtime

### Loading a Schema

```csharp
var context = serviceProvider.GetRequiredService<SchemaContext>();
var userType = await context.GetNodeTypeAsync("myapp.user") as StructType;

// Access fields
foreach (var field in userType.Fields)
{
    Console.WriteLine($"{field.Name}: {field.SchemaType.Name}");
}
```

### Creating and Validating Data

```csharp
var userData = new StructNode(userType);
userData.TrySetValue("name", "John Doe");
userData.TrySetValue("score", 85);

// Validate against constraints
foreach (var constraint in userType.GetProperties<IConstraintProperty>())
{
    await constraint.ValidateAsync(context, userData, null);
}

if (userData.Violated.Length > 0)
{
    Console.WriteLine($"Validation failed: {string.Join(", ", userData.Violated)}");
}
```

### Compiling a Function

```csharp
var funcType = await context.GetNodeTypeAsync("myapp.is_eligible") as FunctionType;
var compiledFunc = funcType.Compile<Func<StructNode, int, bool>>();

var result = compiledFunc(userData, 60); // true if score >= 60 and not blocked
```

---

## 7. Extension Points Summary

| What | How | Example |
|------|-----|---------|
| New schema kind | `[Meta<SchemaKind>]` on a class | Custom "workflow" kind |
| New property | Subclass `Property<T>` | `MyCustomDisplay` |
| New constraint | Implement `IConstraintProperty` | `RangeLimit` |
| New relation | Implement `IRelationProcess` | Custom data lookup |
| New atomic function | `[Function]` on static method | `CalculateTax` |
| New compile context | Subclass `CompileContext` | GraphQL transpiler |
