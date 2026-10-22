using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace MangaDl.Core.Extensions;

/// <summary>
/// DOM wrapper objects exposed to JavaScript for DOMParser emulation.
/// Implements standard DOM traversal methods expected by manga/novel extensions.
/// </summary>
public sealed class DomShim
{
    private static readonly HtmlParser Parser = new();

    public static DomDocument ParseFromString(string html, string? mimeType = "text/html")
    {
        var doc = Parser.ParseDocument(html ?? string.Empty);
        return new DomDocument(doc);
    }

    public static string NormalizeSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return selector;
        var trimmed = selector.TrimStart();
        if (trimmed.StartsWith('>'))
        {
            trimmed = ":scope " + trimmed;
        }

        // Quote unquoted attribute values like img[src^=http] or a[href*=/series/]
        trimmed = Regex.Replace(trimmed, @"\[\s*([a-zA-Z0-9_\-]+)\s*([~|^$*]?=)\s*([^""'\]\s]+)\s*\]", "[$1$2\"$3\"]");

        return trimmed;
    }

    public static List<string> SplitSelectorList(string selector)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(selector)) return parts;

        var sb = new StringBuilder();
        int parenDepth = 0;
        int bracketDepth = 0;
        char inQuote = '\0';

        for (int i = 0; i < selector.Length; i++)
        {
            char c = selector[i];
            if (inQuote != '\0')
            {
                sb.Append(c);
                if (c == inQuote && (i == 0 || selector[i - 1] != '\\'))
                {
                    inQuote = '\0';
                }
            }
            else if (c == '\'' || c == '"')
            {
                inQuote = c;
                sb.Append(c);
            }
            else if (c == '(')
            {
                parenDepth++;
                sb.Append(c);
            }
            else if (c == ')')
            {
                if (parenDepth > 0) parenDepth--;
                sb.Append(c);
            }
            else if (c == '[')
            {
                bracketDepth++;
                sb.Append(c);
            }
            else if (c == ']')
            {
                if (bracketDepth > 0) bracketDepth--;
                sb.Append(c);
            }
            else if (c == ',' && parenDepth == 0 && bracketDepth == 0)
            {
                var s = sb.ToString().Trim();
                if (s.Length > 0) parts.Add(s);
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        var last = sb.ToString().Trim();
        if (last.Length > 0) parts.Add(last);
        return parts;
    }

    public static IElement? SafeQuerySelector(IParentNode node, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        try
        {
            var normalized = NormalizeSelector(selector);
            return node.QuerySelector(normalized);
        }
        catch
        {
            try
            {
                return SafeQuerySelectorAll(node, selector).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }

    public static IEnumerable<IElement> SafeQuerySelectorAll(IParentNode node, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return Enumerable.Empty<IElement>();
        var normalized = NormalizeSelector(selector);
        try
        {
            return node.QuerySelectorAll(normalized);
        }
        catch
        {
            var parts = SplitSelectorList(selector);
            if (parts.Count > 1)
            {
                var combined = new List<IElement>();
                foreach (var part in parts)
                {
                    try
                    {
                        combined.AddRange(SafeQuerySelectorAll(node, part));
                    }
                    catch { }
                }
                return combined.Distinct();
            }

            var containsMatch = Regex.Match(normalized, @"^(.*?):contains\((?:""([^""]*)""|'([^']*)'|([^)]*))\)(.*)$", RegexOptions.IgnoreCase);
            if (containsMatch.Success)
            {
                var baseSelector = (containsMatch.Groups[1].Value.Trim() + containsMatch.Groups[5].Value.Trim()).Trim();
                var text = containsMatch.Groups[2].Success ? containsMatch.Groups[2].Value
                    : (containsMatch.Groups[3].Success ? containsMatch.Groups[3].Value : containsMatch.Groups[4].Value);

                if (string.IsNullOrWhiteSpace(baseSelector)) baseSelector = "*";
                try
                {
                    var baseElements = node.QuerySelectorAll(baseSelector);
                    return baseElements.Where(e => e.TextContent != null && e.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase));
                }
                catch { }
            }

            return Enumerable.Empty<IElement>();
        }
    }
}

public sealed class DomDocument
{
    private readonly IHtmlDocument _doc;

    public DomDocument(IHtmlDocument doc)
    {
        _doc = doc;
    }

    public DomElement createElement(string tagName)
    {
        var el = _doc.CreateElement(tagName ?? "div");
        return new DomElement(el);
    }

    public DomElement? querySelector(string selector)
    {
        var el = DomShim.SafeQuerySelector(_doc, selector);
        return el != null ? new DomElement(el) : null;
    }

    public DomElement[] querySelectorAll(string selector)
    {
        var elements = DomShim.SafeQuerySelectorAll(_doc, selector).ToArray();
        var result = new DomElement[elements.Length];
        for (var i = 0; i < elements.Length; i++)
        {
            result[i] = new DomElement(elements[i]);
        }
        return result;
    }
}

public sealed class DomClassList
{
    private readonly IElement _el;

    public DomClassList(IElement el) => _el = el;

    public bool contains(string className) => _el.ClassList.Contains(className);
    public void add(string className) => _el.ClassList.Add(className);
    public void remove(string className) => _el.ClassList.Remove(className);
    public bool toggle(string className) => _el.ClassList.Toggle(className);
}

public sealed class DomNode
{
    private readonly INode _node;

    public DomNode(INode node) => _node = node;

    public string textContent => _node.TextContent;
    public string nodeName => _node.NodeName;
}

public sealed class DomNodeList
{
    private readonly INodeList _nodes;

    public DomNodeList(IElement el) => _nodes = el.ChildNodes;

    public int length => _nodes.Length;
    public DomNode? this[int index] => index >= 0 && index < _nodes.Length ? new DomNode(_nodes[index]) : null;
}

public sealed class DomElement
{
    private readonly IElement _el;

    public DomElement(IElement el)
    {
        _el = el;
    }

    public string tagName => _el.TagName.ToUpperInvariant();
    public string nodeName => _el.NodeName.ToUpperInvariant();
    public string textContent => _el.TextContent;
    public string innerHTML => _el.InnerHtml;
    public string outerHTML => _el.OuterHtml;

    public string? href => _el.GetAttribute("href");
    public string? src => _el.GetAttribute("src") ?? _el.GetAttribute("data-src") ?? _el.GetAttribute("data-lazy-src");

    public DomClassList classList => new(_el);
    public DomNodeList childNodes => new(_el);

    public string? getAttribute(string name) => _el.GetAttribute(name);
    public bool hasAttribute(string name) => _el.HasAttribute(name);
    public void setAttribute(string name, string value) => _el.SetAttribute(name, value);
    public void removeAttribute(string name) => _el.RemoveAttribute(name);

    public void remove() => _el.Remove();

    public DomElement? nextElementSibling => _el.NextElementSibling != null ? new DomElement(_el.NextElementSibling) : null;
    public DomElement? previousElementSibling => _el.PreviousElementSibling != null ? new DomElement(_el.PreviousElementSibling) : null;
    public DomElement? firstElementChild => _el.FirstElementChild != null ? new DomElement(_el.FirstElementChild) : null;
    public DomElement? lastElementChild => _el.LastElementChild != null ? new DomElement(_el.LastElementChild) : null;

    public DomElement[] children
    {
        get
        {
            var ch = _el.Children;
            var res = new DomElement[ch.Length];
            for (var i = 0; i < ch.Length; i++) res[i] = new DomElement(ch[i]);
            return res;
        }
    }

    public DomElement? querySelector(string selector)
    {
        var child = DomShim.SafeQuerySelector(_el, selector);
        return child != null ? new DomElement(child) : null;
    }

    public DomElement[] querySelectorAll(string selector)
    {
        var elements = DomShim.SafeQuerySelectorAll(_el, selector).ToArray();
        var result = new DomElement[elements.Length];
        for (var i = 0; i < elements.Length; i++)
        {
            result[i] = new DomElement(elements[i]);
        }
        return result;
    }

    public DomElement? closest(string selector)
    {
        try
        {
            var normalized = DomShim.NormalizeSelector(selector);
            var parent = _el.Closest(normalized);
            return parent != null ? new DomElement(parent) : null;
        }
        catch
        {
            return null;
        }
    }

    public DomElement? parentElement
    {
        get
        {
            var p = _el.ParentElement;
            return p != null ? new DomElement(p) : null;
        }
    }
}
