using JsonPathLINQ;

namespace JsonPathLINQ.Tests;

public sealed class ParserTests
{
    // Ported from:
    // https://github.com/kubernetes/client-go/blob/764b57d77172907a6261543ac724c738ec00e83d/util/jsonpath/parser_test.go
    public static IEnumerable<object[]> ParserCases()
    {
        yield return
        [
            new ParserTestCase("plain", "hello jsonpath", [Text("hello jsonpath")], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("variable", "hello {.jsonpath}", [Text("hello "), List(), Field("jsonpath")], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("arrayfiled", "hello {['jsonpath']}", [Text("hello "), List(), Field("jsonpath")], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("quote", "{\"{\"}", [List(), Text("{")], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("array", "{[1:3]}", [List(), Array(P(1, true, false), P(3, true, false), P(0, false, false))], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "allarray",
                "{.book[*].author}",
                [List(), Field("book"), Array(P(0, false, false), P(0, false, false), P(0, false, false)), Field("author")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("wildcard", "{.bicycle.*}", [List(), Field("bicycle"), Wildcard()], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "filter",
                "{[?(@.price<3)]}",
                [List(), Filter("<"), List(), Field("price"), List(), Int(3)],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("recursive", "{..}", [List(), Recursive()], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("recurField", "{..price}", [List(), Recursive(), Field("price")], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("arraydict", "{['book.price']}", [List(), Field("book"), Field("price")], ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "union",
                "{['bicycle.price', 3, 'book.price']}",
                [
                    List(),
                    Union(),
                    List(),
                    Field("bicycle"),
                    Field("price"),
                    List(),
                    Array(P(3, true, false), P(4, true, true), P(0, false, false)),
                    List(),
                    Field("book"),
                    Field("price")
                ],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "range",
                "{range .items}{.name},{end}",
                [List(), Identifier("range"), Field("items"), List(), Field("name"), Text(","), List(), Identifier("end")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase("malformat input", @"{\\\}", [], ShouldError: true)
        ];

        yield return
        [
            new ParserTestCase(
                "paired parentheses in quotes",
                "{[?(@.status.nodeInfo.osImage == \"()\")]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("()")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "paired parentheses in double quotes and with double quotes escape",
                "{[?(@.status.nodeInfo.osImage == \"(\\\"\\\")\")]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("(\"\")")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "unregular parentheses in double quotes",
                "{[?(@.test == \"())(\")]}",
                [List(), Filter("=="), List(), Field("test"), List(), Text("())(")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "plain text in single quotes",
                "{[?(@.status.nodeInfo.osImage == 'Linux')]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("Linux")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "test filter suffix",
                "{[?(@.status.nodeInfo.osImage == \"{[()]}\")]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("{[()]}")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "double inside single",
                "{[?(@.status.nodeInfo.osImage == \"''\")]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("''")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "single inside double",
                "{[?(@.status.nodeInfo.osImage == '\"\"')]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("\"\"")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "single containing escaped single",
                @"{[?(@.status.nodeInfo.osImage == '\\\'')]}",
                [List(), Filter("=="), List(), Field("status"), Field("nodeInfo"), Field("osImage"), List(), Text("\\'")],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "negative index slice, equals a[len-5] to a[len-1]",
                "{[-5:]}",
                [List(), Array(P(-5, true, false), P(0, false, false), P(0, false, false))],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "negative index slice, equals a[len-1]",
                "{[-1]}",
                [List(), Array(P(-1, true, false), P(0, true, true), P(0, false, false))],
                ShouldError: false)
        ];

        yield return
        [
            new ParserTestCase(
                "negative index slice, equals a[1] to a[len-1]",
                "{[1:-1]}",
                [List(), Array(P(1, true, false), P(-1, true, false), P(0, false, false))],
                ShouldError: false)
        ];
    }

    public static IEnumerable<object[]> FailParserCases()
    {
        yield return [new FailParserTestCase("unclosed action", "{.hello", "unclosed action")];
        yield return [new FailParserTestCase("unrecognized character", "{*}", "unrecognized character in action")];
        yield return [new FailParserTestCase("invalid number", "{+12.3.0}", "cannot parse number +12.3.0")];
        yield return [new FailParserTestCase("unterminated array", "{[1}", "unterminated array")];
        yield return [new FailParserTestCase("unterminated filter", "{[?(.price]}", "unterminated filter")];
        yield return [new FailParserTestCase("invalid multiple recursive descent", "{........}", "invalid multiple recursive descent")];
    }

    [Theory]
    [MemberData(nameof(ParserCases))]
    public void ParserCasesFromClientGo(ParserTestCase testCase)
    {
        LegacyJsonPathParser? parser = null;
        Exception? error = null;

        try
        {
            parser = LegacyJsonPathAdapter.Parse(testCase.Name, testCase.Text);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        if (testCase.ShouldError)
        {
            Assert.NotNull(error);
            return;
        }

        Assert.Null(error);
        Assert.NotNull(parser);

        var result = CollectNodes([], parser.Root).Skip(1).ToArray();

        Assert.Equal(testCase.Nodes.Length, result.Length);
        for (var i = 0; i < testCase.Nodes.Length; i++)
        {
            Assert.Equal(testCase.Nodes[i].ToString(), result[i].ToString());
        }
    }

    [Theory]
    [MemberData(nameof(FailParserCases))]
    public void FailParserCasesFromClientGo(FailParserTestCase testCase)
    {
        Exception? error = null;
        try
        {
            _ = LegacyJsonPathAdapter.Parse(testCase.Name, testCase.Text);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        Assert.NotNull(error);
        Assert.Contains(testCase.Error, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"\\b\\f\\n\\r\\t\\\\\\\"\\'\"", "\b\f\n\r\t\\\"'")]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("'plain'", "plain")]
    public void UnquoteExtendParsesEscapes(string input, string expected)
    {
        Assert.Equal(expected, LegacyJsonPathAdapter.UnquoteExtend(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"unterminated")]
    [InlineData("x")]
    [InlineData("\"\\u12\"")]
    [InlineData("\"\\uZZZZ\"")]
    [InlineData("\"abc\\\"")]
    public void UnquoteExtendRejectsInvalidInput(string input)
    {
        Assert.ThrowsAny<Exception>(() => LegacyJsonPathAdapter.UnquoteExtend(input));
    }

    [Fact]
    public void JsonPathParseExceptionSupportsInnerException()
    {
        var inner = new InvalidOperationException("boom");
        var exception = new JsonPathParseException("outer", inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void ParseActionParsesListRoot()
    {
        var parser = LegacyJsonPathAdapter.ParseAction("ok", ".Name");

        var field = Assert.IsType<FieldNode>(Assert.Single(parser.Root.Nodes));
        Assert.Equal("Name", field.Value);
    }

    [Theory]
    [InlineData("{.Name", "unclosed action")]
    [InlineData("{+}", "cannot parse number")]
    [InlineData("{.Name[abc]}", "invalid array index")]
    [InlineData("{\"unterminated}", "unterminated quoted string")]
    public void ParserCoversAdditionalFailureCases(string text, string messageFragment)
    {
        var exception = Assert.Throws<JsonPathParseException>(() => LegacyJsonPathAdapter.Parse("extra", text));
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParserParsesTabWhitespaceAndAtRoot()
    {
        var parser = LegacyJsonPathAdapter.Parse("tab", "{\t@.Name}");

        var root = Assert.IsType<ListNode>(Assert.Single(parser.Root.Nodes));
        var field = Assert.IsType<FieldNode>(Assert.Single(root.Nodes));
        Assert.Equal("Name", field.Value);
    }

    private static List<INode> CollectNodes(List<INode> nodes, INode current)
    {
        nodes.Add(current);
        switch (current)
        {
            case ListNode listNode:
                foreach (var node in listNode.Nodes)
                {
                    CollectNodes(nodes, node);
                }

                break;
            case FilterNode filterNode:
                CollectNodes(nodes, filterNode.Left);
                CollectNodes(nodes, filterNode.Right);
                break;
            case UnionNode unionNode:
                foreach (var node in unionNode.Nodes)
                {
                    CollectNodes(nodes, node);
                }

                break;
        }

        return nodes;
    }

    public sealed record ParserTestCase(string Name, string Text, INode[] Nodes, bool ShouldError);

    public sealed record FailParserTestCase(string Name, string Text, string Error);

    private static TextNode Text(string text) => new(text);

    private static FieldNode Field(string value) => new(value);

    private static ListNode List() => new();

    private static FilterNode Filter(string @operator) => new(new ListNode(), new ListNode(), @operator);

    private static IntNode Int(int value) => new(value);

    private static WildcardNode Wildcard() => new();

    private static RecursiveNode Recursive() => new();

    private static UnionNode Union() => new([]);

    private static IdentifierNode Identifier(string name) => new(name);

    private static ArrayNode Array(ParamsEntry first, ParamsEntry second, ParamsEntry third) => new([first, second, third]);

    private static ParamsEntry P(int value, bool known, bool derived) => new(known, value, derived);
}

