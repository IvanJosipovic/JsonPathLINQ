using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClientGo.JsonPath;

internal sealed class Parser
{
    private const char Eof = '\uffff';
    private const string LeftDelim = "{";
    private const string RightDelim = "}";

    private string _input = string.Empty;
    private int _pos;
    private int _start;
    private int _width;

    private static readonly Regex DictKeyRegex = new("^'([^']*)'$", RegexOptions.Compiled);
    private static readonly Regex SliceOperatorRegex = new("^(-?[\\d]*)(:-?[\\d]*)?(:-?[\\d]*)?$", RegexOptions.Compiled);
    private static readonly Regex FilterRegex = new("^([^!<>=]+)([!<>=]+)(.+?)$", RegexOptions.Compiled);

    private Parser(string name)
    {
        Root = new ListNode();
    }

    public ListNode Root { get; private set; }

    public static Parser Parse(string name, string text)
    {
        var parser = new Parser(name);
        parser.ParseInternal(text);
        return parser;
    }

    private static Parser ParseAction(string name, string text)
    {
        var parser = Parse(name, $"{LeftDelim}{text}{RightDelim}");
        parser.Root = parser.Root.Nodes[0] as ListNode ?? throw new JsonPathParseException("expected list node");
        return parser;
    }

    private void ParseInternal(string text)
    {
        _input = text;
        Root = new ListNode();
        _pos = 0;
        _start = 0;
        parseText(Root);
    }

    private string ConsumeText()
    {
        var value = _input[_start.._pos];
        _start = _pos;
        return value;
    }

    private char NextChar()
    {
        if (_pos >= _input.Length)
        {
            _width = 0;
            return Eof;
        }

        var ch = _input[_pos];
        _width = 1;
        _pos += _width;
        return ch;
    }

    private char PeekChar()
    {
        var ch = NextChar();
        Backup();
        return ch;
    }

    private void Backup()
    {
        _pos -= _width;
        if (_pos < _start)
        {
            _pos = _start;
        }
    }

    private void parseText(ListNode current)
    {
        while (true)
        {
            if (HasPrefix(LeftDelim))
            {
                if (_pos > _start)
                {
                    current.Append(new TextNode(ConsumeText()));
                }

                parseLeftDelim(current);
                return;
            }

            if (NextChar() == Eof)
            {
                break;
            }
        }

        if (_pos > _start)
        {
            current.Append(new TextNode(ConsumeText()));
        }
    }

    private void parseLeftDelim(ListNode current)
    {
        _pos += LeftDelim.Length;
        ConsumeText();
        var newNode = new ListNode();
        current.Append(newNode);
        parseInsideAction(newNode);
    }

    private void parseInsideAction(ListNode current)
    {
        while (true)
        {
            if (HasPrefix(RightDelim))
            {
                parseRightDelim(current);
                return;
            }

            if (HasPrefix("[?("))
            {
                parseFilter(current);
                return;
            }

            if (HasPrefix(".."))
            {
                parseRecursive(current);
                return;
            }

            var r = NextChar();
            switch (r)
            {
                case Eof:
                case '\n':
                case '\r':
                    throw new JsonPathParseException("unclosed action");
                case ' ':
                case '\t':
                    ConsumeText();
                    continue;
                case '@':
                case '$':
                    ConsumeText();
                    continue;
                case '[':
                    parseArray(current);
                    return;
                case '"':
                case '\'':
                    parseQuote(current, r);
                    return;
                case '.':
                    parseField(current);
                    return;
                case '+':
                case '-':
                    Backup();
                    parseNumber(current);
                    return;
                default:
                    if (char.IsDigit(r))
                    {
                        Backup();
                        parseNumber(current);
                        return;
                    }

                    if (IsAlphaNumeric(r))
                    {
                        Backup();
                        parseIdentifier(current);
                        return;
                    }

                    throw new JsonPathParseException($"unrecognized character in action: {r}");
            }
        }
    }

    private void parseRightDelim(ListNode current)
    {
        _pos += RightDelim.Length;
        ConsumeText();
        parseText(Root);
    }

    private void parseIdentifier(ListNode current)
    {
        while (true)
        {
            var r = NextChar();
            if (IsTerminator(r))
            {
                Backup();
                break;
            }
        }

        var value = ConsumeText();
        if (IsBool(value))
        {
            if (!bool.TryParse(value, out var parsed))
            {
                throw new JsonPathParseException($"cannot parse bool '{value}'");
            }

            current.Append(new BoolNode(parsed));
        }
        else
        {
            current.Append(new IdentifierNode(value));
        }

        parseInsideAction(current);
    }

    private void parseRecursive(ListNode current)
    {
        if (current.Nodes.LastOrDefault()?.Type == NodeType.Recursive)
        {
            throw new JsonPathParseException("invalid multiple recursive descent");
        }

        _pos += "..".Length;
        ConsumeText();
        current.Append(new RecursiveNode());
        if (IsAlphaNumeric(PeekChar()))
        {
            parseField(current);
            return;
        }

        parseInsideAction(current);
    }

    private void parseNumber(ListNode current)
    {
        var r = PeekChar();
        if (r == '+' || r == '-')
        {
            NextChar();
        }

        while (true)
        {
            r = NextChar();
            if (r != '.' && !char.IsDigit(r))
            {
                Backup();
                break;
            }
        }

        var value = ConsumeText();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            current.Append(new IntNode(i));
            parseInsideAction(current);
            return;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            current.Append(new FloatNode(d));
            parseInsideAction(current);
            return;
        }

        throw new JsonPathParseException($"cannot parse number {value}");
    }

    private void parseArray(ListNode current)
    {
        while (true)
        {
            var r = NextChar();
            if (r == Eof || r == '\n' || r == '\r')
            {
                throw new JsonPathParseException("unterminated array");
            }

            if (r == ']')
            {
                break;
            }
        }

        var text = ConsumeText();
        text = text[1..^1];
        if (text == "*")
        {
            text = ":";
        }

        var parts = text.Split(',');
        if (parts.Length > 1)
        {
            var unionNodes = new List<ListNode>();
            foreach (var part in parts)
            {
                var parser = ParseAction("union", $"[{part.Trim()}]");
                unionNodes.Add(parser.Root);
            }

            current.Append(new UnionNode(unionNodes));
            parseInsideAction(current);
            return;
        }

        var dictMatch = DictKeyRegex.Match(text);
        if (dictMatch.Success)
        {
            var parser = ParseAction("arraydict", $".{dictMatch.Groups[1].Value}");
            foreach (var node in parser.Root.Nodes)
            {
                current.Append(node);
            }

            parseInsideAction(current);
            return;
        }

        var sliceMatch = SliceOperatorRegex.Match(text);
        if (!sliceMatch.Success)
        {
            throw new JsonPathParseException($"invalid array index {text}");
        }

        var parameters = new ParamsEntry[3];
        for (var i = 0; i < 3; i++)
        {
            var value = sliceMatch.Groups[i + 1].Value;
            if (!string.IsNullOrEmpty(value))
            {
                if (i > 0)
                {
                    value = value[1..];
                }

                if (i > 0 && string.IsNullOrEmpty(value))
                {
                    parameters[i] = new ParamsEntry(false, 0, false);
                }
                else
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        throw new JsonPathParseException($"array index {value} is not a number");
                    }

                    parameters[i] = new ParamsEntry(true, parsed, false);
                }
            }
            else
            {
                if (i == 1)
                {
                    parameters[i] = new ParamsEntry(true, parameters[0].Value + 1, true);
                }
                else
                {
                    parameters[i] = new ParamsEntry(false, 0, false);
                }
            }
        }

        current.Append(new ArrayNode(parameters));
        parseInsideAction(current);
    }

    private void parseFilter(ListNode current)
    {
        _pos += "[?(".Length;
        ConsumeText();
        var begin = false;
        var end = false;
        char pair = default;

        while (true)
        {
            var r = NextChar();
            switch (r)
            {
                case Eof:
                case '\n':
                case '\r':
                    throw new JsonPathParseException("unterminated filter");
                case '"':
                case '\'':
                    if (!begin)
                    {
                        begin = true;
                        pair = r;
                        continue;
                    }

                    if (_input[_pos - 2] != '\\' && r == pair)
                    {
                        end = true;
                    }

                    break;
                case ')':
                    if (begin == end)
                    {
                        goto FilterDone;
                    }

                    break;
            }
        }

    FilterDone:
        if (NextChar() != ']')
        {
            throw new JsonPathParseException("unclosed array expect ]");
        }

        var text = ConsumeText();
        text = text[..^2];
        var match = FilterRegex.Match(text);
        if (!match.Success)
        {
            var parser = ParseAction("text", text);
            current.Append(new FilterNode(parser.Root, new ListNode(), "exists"));
        }
        else
        {
            var leftParser = ParseAction("left", match.Groups[1].Value);
            var rightParser = ParseAction("right", match.Groups[3].Value);
            current.Append(new FilterNode(leftParser.Root, rightParser.Root, match.Groups[2].Value));
        }

        parseInsideAction(current);
    }

    private void parseQuote(ListNode current, char end)
    {
        while (true)
        {
            var r = NextChar();
            if (r == Eof || r == '\n' || r == '\r')
            {
                throw new JsonPathParseException("unterminated quoted string");
            }

            if (r == end && _input[_pos - 2] != '\\')
            {
                break;
            }
        }

        var value = ConsumeText();
        var unquoted = UnquoteExtend(value);
        current.Append(new TextNode(unquoted));
        parseInsideAction(current);
    }

    private void parseField(ListNode current)
    {
        ConsumeText();
        while (Advance())
        {
        }

        var value = ConsumeText();
        if (value == "*")
        {
            current.Append(new WildcardNode());
        }
        else
        {
            current.Append(new FieldNode(value.Replace("\\", string.Empty)));
        }

        parseInsideAction(current);
    }

    private bool Advance()
    {
        var r = NextChar();
        if (r == '\\')
        {
            NextChar();
        }
        else if (IsTerminator(r))
        {
            Backup();
            return false;
        }

        return true;
    }

    private bool HasPrefix(string prefix) =>
        _pos + prefix.Length <= _input.Length &&
        _input.AsSpan(_pos, prefix.Length).SequenceEqual(prefix);

    private static bool IsTerminator(char r) =>
        r == Eof || r == '.' || r == ',' || r == '[' || r == ']' || r == '$' || r == '@' || r == '{' || r == '}' || r == ' ' ||
        r == '\t' || r == '\r' || r == '\n';

    private static bool IsAlphaNumeric(char r) => r == '_' || char.IsLetterOrDigit(r);

    private static bool IsBool(string value) => value == "true" || value == "false";

    internal static string UnquoteExtend(string value)
    {
        if (value.Length < 2)
        {
            throw new JsonPathParseException("invalid syntax");
        }

        var quote = value[0];
        if (quote != value[^1])
        {
            throw new JsonPathParseException("invalid syntax");
        }

        var inner = value[1..^1];
        if (quote != '"' && quote != '\'')
        {
            throw new JsonPathParseException("invalid syntax");
        }

        var builder = new StringBuilder(inner.Length);
        for (int i = 0; i < inner.Length; i++)
        {
            var ch = inner[i];
            if (ch == '\\')
            {
                if (i == inner.Length - 1)
                {
                    throw new JsonPathParseException($"invalid escape sequence in {value}");
                }

                var next = inner[++i];
                switch (next)
                {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '\'':
                        builder.Append('\'');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 >= inner.Length)
                        {
                            throw new JsonPathParseException($"invalid unicode escape in {value}");
                        }

                        var hex = inner.Substring(i + 1, 4);
                        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            throw new JsonPathParseException($"invalid unicode escape in {value}");
                        }

                        builder.Append(char.ConvertFromUtf32(code));
                        i += 4;
                        break;
                    default:
                        builder.Append(next);
                        break;
                }
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}

internal sealed class JsonPathParseException : Exception
{
    public JsonPathParseException(string message)
        : base(message)
    {
    }

    public JsonPathParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
