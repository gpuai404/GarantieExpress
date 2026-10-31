using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;

namespace GarantieExpress.Application.Categories;

public sealed partial class SvgSanitizer(ILocalizationService localization)
{
    public const int MaximumBytes = 50 * 1024;

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "defs", "linearGradient", "radialGradient", "stop", "clipPath", "title"
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "viewBox", "preserveAspectRatio", "d", "x", "y", "x1", "y1", "x2", "y2",
        "cx", "cy", "r", "rx", "ry", "width", "height", "points", "fill", "stroke",
        "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-opacity", "fill-opacity",
        "opacity", "transform", "offset", "stop-color", "stop-opacity", "id", "class",
        "clip-path", "gradientUnits", "gradientTransform", "fx", "fy"
    };

    public OperationResult<string> Sanitize(string svg, bool useThemeColor)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            return Failure("Category.SvgEmpty", "Categories_SvgEmpty");
        }

        if (Encoding.UTF8.GetByteCount(svg) > MaximumBytes)
        {
            return Failure("Category.SvgTooLarge", "Categories_SvgTooLarge");
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumBytes
            };
            using var textReader = new StringReader(svg);
            using var xmlReader = XmlReader.Create(textReader, settings);
            document = XDocument.Load(xmlReader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            return Failure("Category.SvgMalformed", "Categories_SvgMalformed");
        }

        var root = document.Root;
        if (root is null || root.Name.LocalName != "svg")
        {
            return Failure("Category.SvgRootRequired", "Categories_SvgRootRequired");
        }

        if (root.Attribute("viewBox") is null || string.IsNullOrWhiteSpace(root.Attribute("viewBox")!.Value))
        {
            return Failure("Category.SvgViewBoxRequired", "Categories_SvgViewBoxRequired");
        }

        foreach (var node in document.DescendantNodes().Where(node => node is XComment or XProcessingInstruction).ToList())
        {
            node.Remove();
        }

        foreach (var element in root.DescendantsAndSelf().Reverse().ToList())
        {
            if (!AllowedElements.Contains(element.Name.LocalName))
            {
                if (element == root)
                {
                    return Failure("Category.SvgRootRequired", "Categories_SvgRootRequired");
                }

                element.Remove();
                continue;
            }

            element.Name = element.Name.LocalName;
            SanitizeAttributes(element, useThemeColor);
        }

        root.SetAttributeValue("xmlns", null);
        return OperationResult<string>.Success(root.ToString(SaveOptions.DisableFormatting));
    }

    private OperationResult<string> Failure(string code, string resourceKey) =>
        OperationResult<string>.Failure(new OperationError(code, localization[resourceKey]));

    private static void SanitizeAttributes(XElement element, bool useThemeColor)
    {
        foreach (var attribute in element.Attributes().ToList())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                attribute.Remove();
                continue;
            }

            var name = attribute.Name.LocalName;
            var value = attribute.Value.Trim();
            if (attribute.Name.Namespace != XNamespace.None
                || name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                || !AllowedAttributes.Contains(name)
                || ContainsUnsafeReference(value)
                || name == "id" && !SafeTokenRegex().IsMatch(value)
                || name == "class" && !SafeClassRegex().IsMatch(value))
            {
                attribute.Remove();
                continue;
            }

            if (useThemeColor
                && name is "fill" or "stroke" or "stop-color"
                && !value.Equals("none", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                && !InternalUrlRegex().IsMatch(value))
            {
                attribute.Value = "currentColor";
            }
        }
    }

    private static bool ContainsUnsafeReference(string value)
    {
        if (value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("data:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("http:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("https:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Contains("url(", StringComparison.OrdinalIgnoreCase)
            && !InternalUrlRegex().IsMatch(value);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.:-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeTokenRegex();

    [GeneratedRegex("^[A-Za-z0-9_ .:-]{0,160}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeClassRegex();

    [GeneratedRegex("^url\\(#[A-Za-z_][A-Za-z0-9_.:-]{0,79}\\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex InternalUrlRegex();
}
