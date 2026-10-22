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
            return ":scope " + trimmed;
        }
        return selector;
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
        try
        {
            var normalized = DomShim.NormalizeSelector(selector);
            var el = _doc.QuerySelector(normalized);
            return el != null ? new DomElement(el) : null;
        }
        catch
        {
            return null;
        }
    }

    public DomElement[] querySelectorAll(string selector)
    {
        try
        {
            var normalized = DomShim.NormalizeSelector(selector);
            var elements = _doc.QuerySelectorAll(normalized);
            var result = new DomElement[elements.Length];
            for (var i = 0; i < elements.Length; i++)
            {
                result[i] = new DomElement(elements[i]);
            }
            return result;
        }
        catch
        {
            return Array.Empty<DomElement>();
        }
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
        try
        {
            var normalized = DomShim.NormalizeSelector(selector);
            var child = _el.QuerySelector(normalized);
            return child != null ? new DomElement(child) : null;
        }
        catch
        {
            return null;
        }
    }

    public DomElement[] querySelectorAll(string selector)
    {
        try
        {
            var normalized = DomShim.NormalizeSelector(selector);
            var elements = _el.QuerySelectorAll(normalized);
            var result = new DomElement[elements.Length];
            for (var i = 0; i < elements.Length; i++)
            {
                result[i] = new DomElement(elements[i]);
            }
            return result;
        }
        catch
        {
            return Array.Empty<DomElement>();
        }
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
