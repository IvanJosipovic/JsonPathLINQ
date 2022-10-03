# JsonPathLINQ
JsonPath to LINQ Expressions

[![Nuget](https://img.shields.io/nuget/vpre/JsonPathLINQ.svg?style=flat-square)](https://www.nuget.org/packages/JsonPathLINQ)
[![Nuget)](https://img.shields.io/nuget/dt/JsonPathLINQ.svg?style=flat-square)](https://www.nuget.org/packages/JsonPathLINQ)
[![codecov](https://codecov.io/gh/IvanJosipovic/JsonPathLINQ/branch/alpha/graph/badge.svg?token=I8ARskux8f)](https://codecov.io/gh/IvanJosipovic/JsonPathLINQ)

## What is this?

This project allows generating LINQ Expression from JsonPath queries.

## How to install?

```csharp
Install-Package JsonPathLINQ
```

## How to use?

```csharp
private class TestObject
{
   public string stringValue { get; set; }
}

var exp = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject>(".stringValue");
var compiled = exp.Compile();

var result = compiled.Invoke(new TestObject());
```

## Supported Operations

- Property Access
  - Examples
    - ".property"
    - ".property.subProperty"
- Filter
  - Examples
    - ".list[?(@.Type==\"1\")].Status"
  - Operators
    - Equals (==)
