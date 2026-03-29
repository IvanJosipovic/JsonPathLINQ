using System.Text.Json;
using JsonPathLINQ;

namespace JsonPathLINQ.Tests;

public sealed class RfcJsonPathTests
{
    [Theory]
    [InlineData("$.stringValue", "TestString")]
    [InlineData("$.subClass.Type", "Type1")]
    [InlineData("$.subClassList[2].Status", "Starting")]
    [InlineData("$.dictionary.key", "value")]
    public void GetExpressionSupportsBasicRfcPaths(string jsonPath, object expected)
    {
        var expression = JsonPath.GetExpression<JsonPathTests.ExpressionTestObject>(jsonPath);
        var result = expression.Compile()(JsonPathSharedTestData.CreateExpressionTestObject());

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetExpressionSupportsRfcJsonDocument()
    {
        var document = JsonDocument.Parse("""
            {
              "items": [
                { "name": "one", "ready": false },
                { "name": "two", "ready": true }
              ]
            }
            """);

        var expression = JsonPath.GetExpression<JsonDocument>("$.items[1].name");
        var result = expression.Compile()(document);

        Assert.Equal("two", result);
    }

    [Fact]
    public void GetExpressionSupportsLogicalAndFilters()
    {
        var expression = JsonPath.GetExpression<JsonPathTests.ExpressionTestObject>("$.subClassList[?(@.intValue==7&&@.boolValue==true)].Type");
        var result = expression.Compile()(JsonPathSharedTestData.CreateExpressionTestObject());

        Assert.Equal("3", result);
    }

    [Fact]
    public void GetExpressionSupportsLogicalOrFilters()
    {
        var expression = JsonPath.GetExpression<JsonPathTests.ExpressionTestObject>("$.subClassList[?(@.intValue==6||@.intValue==7)].Type");
        var result = expression.Compile()(JsonPathSharedTestData.CreateExpressionTestObject());

        Assert.Equal("1", result);
    }

    [Fact]
    public void LegacyJsonPathStillWorks()
    {
        var expression = JsonPath.GetExpression<JsonPathTests.ExpressionTestObject>(".stringValue");
        Assert.Equal("TestString", expression.Compile()(JsonPathSharedTestData.CreateExpressionTestObject()));
    }
}
