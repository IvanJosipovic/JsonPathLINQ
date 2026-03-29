# JsonPathLINQ
Generate LINQ expressions from Kubernetes JSONPath paths over CLR and `System.Text.Json` object graphs.

[![Nuget](https://img.shields.io/nuget/vpre/JsonPathLINQ.svg?style=flat-square)](https://www.nuget.org/packages/JsonPathLINQ)
[![Nuget)](https://img.shields.io/nuget/dt/JsonPathLINQ.svg?style=flat-square)](https://www.nuget.org/packages/JsonPathLINQ)
[![codecov](https://codecov.io/gh/IvanJosipovic/JsonPathLINQ/branch/alpha/graph/badge.svg?token=I8ARskux8f)](https://codecov.io/gh/IvanJosipovic/JsonPathLINQ)

## What it does

`JsonPathLINQ` converts Kubernetes JSONPath-style paths into LINQ expressions:

```csharp
Expression<Func<T, TResult>>
```

It is intended for querying:

- regular CLR object graphs
- dictionaries
- collections
- mixed CLR + `System.Text.Json` object graphs

The public API is:

```csharp
var expression = JsonPath.GetExpression<T>(jsonPath, addNullChecks: false);
var typedExpression = JsonPath.GetExpression<T, TResult>(jsonPath, addNullChecks: false);
```

This library is focused on expression generation for the Kubernetes JSONPath dialect used by `kubectl`.

- Inputs follow the Kubernetes JSONPath template/path dialect used by `kubectl`.
- RFC 9535 style paths beginning with `$` are supported only for the subset implemented by the expression generator.
- The primary goal of the package is expression generation over CLR and `System.Text.Json` object graphs, not complete JSONPath evaluation compatibility.

## Install

```powershell
Install-Package JsonPathLINQ
```

## Supported Kubernetes JSONPath Features

The current expression generator supports:

- field/property access: `.name`, `.parent.child`
- case-insensitive CLR member lookup
- dictionary key access: `.labels.key`, `.labels.crossplane\.io/external-name`
- array index and slice access: `.items[0]`, `.items[-1]`, `.items[1:4]`, `.items[0:6:2]`
- wildcard segments: `*`
- recursive descent: `..`
- unions
- collection filtering with `FirstOrDefault`: `.items[?(@.status=="Ready")]`
- filter operators: `==`, `!=`, `<`, `>`, `<=`, `>=`
- null literal in filters: `.items[?(@.nullable==null)]`
- optional null-propagation via `addNullChecks: true`
- `System.Text.Json` traversal through:
  - `JsonElement`
  - `JsonDocument`
  - `JsonNode`
  - `JsonObject`
  - `JsonArray`
  - `JsonValue`

When the terminal JSON value is a scalar, the compiled expression returns a natural CLR value where practical:

- string -> `string`
- number -> numeric CLR type
- boolean -> `bool`
- null -> `null`

When the terminal JSON value is an object or array, the expression keeps it as an STJ container so it can still be traversed naturally:

- `JsonElement` containers remain `JsonElement`
- `JsonObject` / `JsonArray` remain node containers

## Not supported by the expression generator

The library does not currently generate expressions for:

- multi-select roots or templates with multiple root actions
- full Kubernetes template evaluation semantics
- full RFC 9535 JSONPath compliance

The broader tests in this repository cover more parser and template behavior, but the generated expression API is still intentionally scoped to the features listed above.

## Examples

### Basic CLR access

```csharp
public sealed class TestObject
{
    public string? StringValue { get; set; }
}

var expression = JsonPath.GetExpression<TestObject>(".StringValue");
var compiled = expression.Compile();

var result = compiled(new TestObject { StringValue = "hello" });
// "hello"
```

### Filter over a CLR collection

```csharp
public sealed class Pod
{
    public List<Container> Containers { get; set; } = [];
}

public sealed class Container
{
    public string? Name { get; set; }
    public string? Status { get; set; }
}

var expression = JsonPath.GetExpression<Pod>(
    ".Containers[?(@.Status==\"Ready\")].Name");

var compiled = expression.Compile();
```

### Mixed CLR + System.Text.Json

```csharp
public sealed class MyClass
{
    public JsonNode? MyNode { get; set; }
    public JsonElement MyElement { get; set; }
    public JsonDocument MyDocument { get; set; } = JsonDocument.Parse("{}");
}

var instance = new MyClass
{
    MyNode = JsonNode.Parse("""{ "foo": { "bar": "node" } }"""),
    MyElement = JsonDocument.Parse("""{ "foo": { "bar": "element" } }""").RootElement.Clone(),
    MyDocument = JsonDocument.Parse("""{ "foo": { "bar": "document" } }""")
};

var fromNode = JsonPath
    .GetExpression<MyClass>(".MyNode.foo.bar")
    .Compile()(instance);

var fromElement = JsonPath
    .GetExpression<MyClass>(".MyElement.foo.bar")
    .Compile()(instance);

var fromDocument = JsonPath
    .GetExpression<MyClass>(".MyDocument.foo.bar")
    .Compile()(instance);
```

### Null-safe access

```csharp
var expression = JsonPath.GetExpression<MyClass>(
    ".MyNode.foo.missing.value",
    addNullChecks: true);

var compiled = expression.Compile();
var result = compiled(instance);
// null
```

## Notes

- Kubernetes-style paths may be passed with or without outer `{}`.
- Template-style inputs such as `hello {.Name}` are part of the Kubernetes JSONPath dialect, not RFC 9535 JSONPath.
- `$...` paths use the RFC-oriented parser path, but only the subset listed above is currently supported for expression generation.
- The generator expects a single root action.
- The generated filter behavior is based on `Enumerable.FirstOrDefault(...)`, so a filter selects one matching item rather than projecting all matches.
- Paths that naturally project multiple values such as wildcards, unions, recursive descent, and slices return an `object?[]` when used through `GetExpression<T>(...)`.
