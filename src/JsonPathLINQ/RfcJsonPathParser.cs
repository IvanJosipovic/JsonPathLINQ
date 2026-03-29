using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JsonPathLINQ;

internal static class RfcJsonPathParser
{
    public static ParserResult Parse(string text)
    {
        var parser = new Impl(text);
        return parser.Parse();
    }

    internal sealed record ParserResult(ListNode Root);

    private sealed class Impl
    {
        private readonly string _text;
        private int _pos;

        public Impl(string text) => _text = text;

        public ParserResult Parse()
        {
            if (_text.Length == 0 || _text[0] != '$')
            {
                throw new JsonPathParseException("RFC JSONPath must start with '$'.");
            }

            var root = new ListNode();
            var current = new ListNode();
            root.Append(current);
            _pos = 1;
            while (_pos < _text.Length)
            {
                if (Match(".."))
                {
                    current.Append(new RecursiveNode());
                    continue;
                }

                if (Match("."))
                {
                    if (Peek() == '*')
                    {
                        _pos++;
                        current.Append(new WildcardNode());
                    }
                    else
                    {
                        current.Append(new FieldNode(ReadName()));
                    }

                    continue;
                }

                if (Match("["))
                {
                    ParseBracketSegment(current);
                    continue;
                }

                throw new JsonPathParseException($"unexpected character '{_text[_pos]}' at position {_pos}.");
            }

            return new ParserResult(root);
        }

        private void ParseBracketSegment(ListNode target)
        {
            SkipWhitespace();
            if (Match("?("))
            {
                target.Append(new PredicateNode(ParsePredicate(ReadUntilClosingParen())));
                Expect("]");
                return;
            }

            if (Peek() == '\'' || Peek() == '"')
            {
                var members = new List<INode>();
                do
                {
                    members.Add(new FieldNode(ParseQuotedString()));
                    SkipWhitespace();
                } while (Match(",") && SkipWhitespaceReturnTrue());

                Expect("]");
                target.Append(members.Count == 1
                    ? members[0]
                    : new UnionNode(members.Select(x => new ListNode { Nodes = { x } })));
                return;
            }

            if (Peek() == '*')
            {
                _pos++;
                Expect("]");
                target.Append(new WildcardNode());
                return;
            }

            var sliceOrIndex = ReadUntil(']');
            Expect("]");

            if (sliceOrIndex.Contains(':'))
            {
                target.Append(ParseSlice(sliceOrIndex));
                return;
            }

            if (int.TryParse(sliceOrIndex.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                target.Append(new ArrayNode([new ParamsEntry(true, index, false), new ParamsEntry(true, index + 1, true), new ParamsEntry(false, 0, false)]));
                return;
            }

            if (!string.IsNullOrWhiteSpace(sliceOrIndex))
            {
                target.Append(new FieldNode(sliceOrIndex.Trim()));
                return;
            }

            throw new JsonPathParseException("invalid empty bracket selector");
        }

        private static ArrayNode ParseSlice(string text)
        {
            var parts = text.Split(':');
            if (parts.Length is < 2 or > 3)
            {
                throw new JsonPathParseException($"invalid array index {text}");
            }

            var paramsEntries = new ParamsEntry[3];
            for (var i = 0; i < 3; i++)
            {
                if (i >= parts.Length || string.IsNullOrWhiteSpace(parts[i]))
                {
                    paramsEntries[i] = new ParamsEntry(false, 0, false);
                    continue;
                }

                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    throw new JsonPathParseException($"array index {parts[i]} is not a number");
                }

                paramsEntries[i] = new ParamsEntry(true, value, false);
            }

            return new ArrayNode(paramsEntries);
        }

        private FilterExpression ParsePredicate(string text)
        {
            text = text.Trim();
            if (text.Contains("||", StringComparison.Ordinal))
            {
                return SplitTopLevel(text, "||").Select(ParsePredicate).Aggregate((left, right) => new FilterLogicalExpression(left, "||", right));
            }

            if (text.Contains("&&", StringComparison.Ordinal))
            {
                return SplitTopLevel(text, "&&").Select(ParsePredicate).Aggregate((left, right) => new FilterLogicalExpression(left, "&&", right));
            }

            var match = FilterRegex.Match(text);
            if (match.Success)
            {
                return new FilterComparisonExpression(ParseOperand(match.Groups[1].Value), match.Groups[2].Value, ParseOperand(match.Groups[3].Value));
            }

            return ParseOperand(text) switch
            {
                FilterPathExpression path => new FilterComparisonExpression(path, "exists", new FilterLiteralExpression(null)),
                _ => throw new JsonPathParseException("unsupported RFC filter expression.")
            };
        }

        private static FilterExpression ParseOperand(string text)
        {
            text = text.Trim();
            if (text == "null")
            {
                return new FilterLiteralExpression(null);
            }

            if (text == "true" || text == "false")
            {
                return new FilterLiteralExpression(bool.Parse(text));
            }

            if ((text.StartsWith('"') && text.EndsWith('"')) || (text.StartsWith('\'') && text.EndsWith('\'')))
            {
                return new FilterLiteralExpression(text[1..^1]);
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return new FilterLiteralExpression(intValue);
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return new FilterLiteralExpression(doubleValue);
            }

            if (text.Length > 0 && text[0] == '@')
            {
                text = "$" + text[1..];
            }

            if (text.Length > 0 && text[0] != '$')
            {
                text = "$" + text;
            }

            var parsed = RfcJsonPathParser.Parse(text);
            return new FilterPathExpression(parsed.Root.Nodes[0] as ListNode ?? new ListNode());
        }

        private static List<string> SplitTopLevel(string text, string separator)
        {
            var parts = new List<string>();
            var start = 0;
            var depth = 0;
            var quote = '\0';

            for (var i = 0; i <= text.Length - separator.Length; i++)
            {
                var ch = text[i];
                if (quote != '\0')
                {
                    if (ch == quote && (i == 0 || text[i - 1] != '\\'))
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch is '\'' or '"')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')')
                {
                    depth--;
                    continue;
                }

                if (depth == 0 && text.AsSpan(i, separator.Length).SequenceEqual(separator))
                {
                    parts.Add(text[start..i].Trim());
                    start = i + separator.Length;
                    i += separator.Length - 1;
                }
            }

            parts.Add(text[start..].Trim());
            return parts.Where(p => p.Length > 0).ToList();
        }

        private string ReadName()
        {
            var start = _pos;
            while (_pos < _text.Length)
            {
                var ch = _text[_pos];
                if (ch is '.' or '[' or ']')
                {
                    break;
                }

                _pos++;
            }

            if (_pos == start)
            {
                throw new JsonPathParseException("expected name.");
            }

            return _text[start.._pos];
        }

        private string ParseQuotedString()
        {
            var quote = _text[_pos++];
            var sb = new StringBuilder();
            while (_pos < _text.Length)
            {
                var ch = _text[_pos++];
                if (ch == quote)
                {
                    return sb.ToString();
                }

                if (ch == '\\' && _pos < _text.Length)
                {
                    sb.Append(_text[_pos++]);
                    continue;
                }

                sb.Append(ch);
            }

            throw new JsonPathParseException("unterminated quoted string");
        }

        private string ReadUntilClosingParen()
        {
            var start = _pos;
            var depth = 1;
            var quote = '\0';
            while (_pos < _text.Length)
            {
                var ch = _text[_pos++];
                if (quote != '\0')
                {
                    if (ch == quote && _text[_pos - 2] != '\\')
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch is '"' or '\'')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return _text[start..(_pos - 1)];
                    }
                }
            }

            throw new JsonPathParseException("unterminated filter.");
        }

        private string ReadUntil(char end)
        {
            var start = _pos;
            while (_pos < _text.Length && _text[_pos] != end)
            {
                _pos++;
            }

            return _text[start.._pos];
        }

        private void Expect(string value)
        {
            if (!Match(value))
            {
                throw new JsonPathParseException($"expected '{value}'.");
            }
        }

        private bool Match(string value)
        {
            if (_text.AsSpan(_pos).StartsWith(value, StringComparison.Ordinal))
            {
                _pos += value.Length;
                return true;
            }

            return false;
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }

        private bool SkipWhitespaceReturnTrue()
        {
            SkipWhitespace();
            return true;
        }

        private static readonly Regex FilterRegex = new(@"^\s*(.+?)(==|!=|<=|>=|<|>)\s*(.+?)\s*$", RegexOptions.Compiled);
    }
}
