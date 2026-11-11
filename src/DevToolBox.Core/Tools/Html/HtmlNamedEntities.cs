namespace DevToolBox.Core.Tools.Html;

/// <summary>Named entities used when encoding: the HTML 4 Latin-1 set plus common typographic symbols.</summary>
internal static class HtmlNamedEntities
{
    // Code points U+00A0 … U+00FF, in order.
    private static readonly string[] Latin1 =
    [
        "nbsp", "iexcl", "cent", "pound", "curren", "yen", "brvbar", "sect", "uml", "copy", "ordf", "laquo",
        "not", "shy", "reg", "macr", "deg", "plusmn", "sup2", "sup3", "acute", "micro", "para", "middot",
        "cedil", "sup1", "ordm", "raquo", "frac14", "frac12", "frac34", "iquest", "Agrave", "Aacute", "Acirc",
        "Atilde", "Auml", "Aring", "AElig", "Ccedil", "Egrave", "Eacute", "Ecirc", "Euml", "Igrave", "Iacute",
        "Icirc", "Iuml", "ETH", "Ntilde", "Ograve", "Oacute", "Ocirc", "Otilde", "Ouml", "times", "Oslash",
        "Ugrave", "Uacute", "Ucirc", "Uuml", "Yacute", "THORN", "szlig", "agrave", "aacute", "acirc", "atilde",
        "auml", "aring", "aelig", "ccedil", "egrave", "eacute", "ecirc", "euml", "igrave", "iacute", "icirc",
        "iuml", "eth", "ntilde", "ograve", "oacute", "ocirc", "otilde", "ouml", "divide", "oslash", "ugrave",
        "uacute", "ucirc", "uuml", "yacute", "thorn", "yuml",
    ];

    private static readonly Dictionary<int, string> Symbols = new()
    {
        [0x2013] = "ndash",
        [0x2014] = "mdash",
        [0x2018] = "lsquo",
        [0x2019] = "rsquo",
        [0x201C] = "ldquo",
        [0x201D] = "rdquo",
        [0x2022] = "bull",
        [0x2026] = "hellip",
        [0x20AC] = "euro",
        [0x2122] = "trade",
        [0x2190] = "larr",
        [0x2191] = "uarr",
        [0x2192] = "rarr",
        [0x2193] = "darr",
        [0x2264] = "le",
        [0x2265] = "ge",
        [0x2260] = "ne",
        [0x221E] = "infin",
        [0x2665] = "hearts",
    };

    public static IEnumerable<KeyValuePair<int, string>> All =>
        Latin1.Select((name, i) => KeyValuePair.Create(0xA0 + i, name)).Concat(Symbols);

    public static bool TryGetName(int codePoint, out string name)
    {
        if (codePoint is >= 0xA0 and <= 0xFF)
        {
            name = Latin1[codePoint - 0xA0];
            return true;
        }

        return Symbols.TryGetValue(codePoint, out name!);
    }
}
