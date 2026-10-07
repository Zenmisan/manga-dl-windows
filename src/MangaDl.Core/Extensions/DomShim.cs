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
}

public sealed class DomDocument
{
    private readonly IHtmlDocument _doc;

    public DomDocument(IHtmlDocument doc)
    {
        _doc = doc;
    }

    public DomElement? querySelector(string selector)
    {
        try
        {
            var el = _doc.QuerySelector(selector);
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
            var elements = _doc.QuerySelectorAll(selector);
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

    public string? getAttribute(string name) => _el.GetAttribute(name);

    public bool hasAttribute(string name) => _el.HasAttribute(name);

    public DomElement? querySelector(string selector)
    {
        try
        {
            var child = _el.QuerySelector(selector);
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
            var elements = _el.QuerySelectorAll(selector);
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
            var parent = _el.Closest(selector);
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
