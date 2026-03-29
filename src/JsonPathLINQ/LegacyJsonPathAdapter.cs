namespace JsonPathLINQ;

internal static class LegacyJsonPathAdapter
{
    public static LegacyJsonPathParser Parse(string name, string text) =>
        LegacyJsonPathParser.Parse(name, text);

    internal static LegacyJsonPathParser ParseAction(string name, string text) =>
        LegacyJsonPathParser.ParseAction(name, text);

    internal static string UnquoteExtend(string value) =>
        LegacyJsonPathParser.UnquoteExtend(value);
}
