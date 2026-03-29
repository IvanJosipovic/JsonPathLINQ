using System.Globalization;

namespace JsonPathLINQ;

public enum NodeType
{
    Text,
    Array,
    List,
    Field,
    Identifier,
    Filter,
    Int,
    Float,
    Wildcard,
    Recursive,
    Union,
    Bool,
}

public interface INode
{
    NodeType Type { get; }
}

public sealed class ListNode : INode
{
    public NodeType Type => NodeType.List;

    public List<INode> Nodes { get; } = new();

    public void Append(INode node) => Nodes.Add(node);

    public void ReplaceNodes(IEnumerable<INode> nodes)
    {
        Nodes.Clear();
        Nodes.AddRange(nodes);
    }

    public override string ToString() => Type.ToString();
}

public sealed class TextNode : INode
{
    public NodeType Type => NodeType.Text;

    public string Text { get; }

    public TextNode(string text) => Text = text;

    public override string ToString() => $"{Type}: {Text}";
}

public sealed class FieldNode : INode
{
    public NodeType Type => NodeType.Field;

    public string Value { get; }

    public FieldNode(string value) => Value = value;

    public override string ToString() => $"{Type}: {Value}";
}

public sealed class IdentifierNode : INode
{
    public NodeType Type => NodeType.Identifier;

    public string Name { get; }

    public IdentifierNode(string name) => Name = name;

    public override string ToString() => $"{Type}: {Name}";
}

public sealed class FilterNode : INode
{
    public NodeType Type => NodeType.Filter;

    public ListNode Left { get; }

    public ListNode Right { get; }

    public string Operator { get; }

    public FilterNode(ListNode left, ListNode right, string @operator)
    {
        Left = left;
        Right = right;
        Operator = @operator;
    }

    public override string ToString() => $"{Type}: {Left} {Operator} {Right}";
}

public sealed class IntNode : INode
{
    public NodeType Type => NodeType.Int;

    public int Value { get; }

    public IntNode(int value) => Value = value;

    public override string ToString() => $"{Type}: {Value}";
}

public sealed class FloatNode : INode
{
    public NodeType Type => NodeType.Float;

    public double Value { get; }

    public FloatNode(double value) => Value = value;

    public override string ToString() => $"{Type}: {Value.ToString(CultureInfo.InvariantCulture)}";
}

public sealed class BoolNode : INode
{
    public NodeType Type => NodeType.Bool;

    public bool Value { get; }

    public BoolNode(bool value) => Value = value;

    public override string ToString() => $"{Type}: {Value}";
}

public sealed class WildcardNode : INode
{
    public NodeType Type => NodeType.Wildcard;

    public override string ToString() => Type.ToString();
}

public sealed class RecursiveNode : INode
{
    public NodeType Type => NodeType.Recursive;

    public override string ToString() => Type.ToString();
}

public sealed class UnionNode : INode
{
    public NodeType Type => NodeType.Union;

    public List<ListNode> Nodes { get; }

    public UnionNode(IEnumerable<ListNode> nodes)
    {
        Nodes = [.. nodes];
    }

    public override string ToString() => Type.ToString();
}

public sealed class ArrayNode : INode
{
    public NodeType Type => NodeType.Array;

    public ParamsEntry[] Params { get; }

    public ArrayNode(ParamsEntry[] @params)
    {
        Params = [.. @params];
    }

    public override string ToString() => $"{Type}: {string.Join(",", Params.Select(p => p.ToString()))}";
}

public struct ParamsEntry
{
    public bool Known { get; set; }

    public int Value { get; set; }

    public bool Derived { get; set; }

    public ParamsEntry(bool known, int value, bool derived)
    {
        Known = known;
        Value = value;
        Derived = derived;
    }

    public override readonly string ToString() =>
        $"{(Known ? Value.ToString(CultureInfo.InvariantCulture) : "?")}" +
        (Derived ? " (derived)" : string.Empty);
}
