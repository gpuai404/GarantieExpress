using GarantieExpress.Domain;

namespace GarantieExpress.Application.Categories;

public static class BuiltInCategories
{
    public static IReadOnlyList<Category> All { get; } = CreateAll();

    private static IReadOnlyList<Category> CreateAll()
    {
        DomainResult<Category, CategoryError>[] definitions =
        [
            Create(
                "laptop", "Laptop", "Category_Laptop", 10, true, [12, 24],
                WarrantyFieldKind.SerialNumber, "Serial number", "Field_SerialNumber",
                """<svg viewBox="0 0 180 150"><rect x="35" y="20" width="110" height="82" rx="7"/><path d="M18 118h144l-13 14H31z"/><path class="screen" d="M49 37h82v49H49z"/></svg>""",
                """<svg viewBox="0 0 180 150"><rect x="39" y="25" width="102" height="74" rx="6"/><path d="M22 116h136l-12 12H34z"/></svg>""",
                """<svg viewBox="0 0 180 150"><path d="M42 31h96v66H42zM24 118h132"/></svg>"""),
            Create(
                "smartphone", "Smartphone", "Category_Smartphone", 20, true, [24],
                WarrantyFieldKind.Imei, "Serial number / IMEI", "Field_Imei",
                """<svg viewBox="0 0 180 150"><rect x="55" y="12" width="70" height="126" rx="14"/><path class="screen" d="M69 31h42v82H69z"/><path d="M82 22h16M84 126h12"/></svg>""",
                """<svg viewBox="0 0 180 150"><rect x="58" y="14" width="64" height="122" rx="12"/><path d="M70 31h40M83 124h14"/></svg>""",
                """<svg viewBox="0 0 180 150"><rect x="61" y="17" width="58" height="116" rx="10"/><path d="M83 122h14"/></svg>"""),
            Create(
                "tablet", "Tablet", "Category_Tablet", 30, true, [24],
                WarrantyFieldKind.SerialNumber, "Serial number", "Field_SerialNumber",
                """<svg viewBox="0 0 180 150"><rect x="32" y="16" width="116" height="118" rx="10"/><path class="screen" d="M45 30h90v86H45z"/><circle cx="90" cy="125" r="3"/><circle cx="90" cy="23" r="1.5"/></svg>""",
                """<svg viewBox="0 0 180 150"><rect x="35" y="18" width="110" height="114" rx="9"/><path d="M48 31h84M86 122h8"/></svg>""",
                """<svg viewBox="0 0 180 150"><rect x="39" y="21" width="102" height="108" rx="8"/></svg>"""),
            Create(
                "printer", "Printer", "Category_Printer", 40, true, [12, 24],
                WarrantyFieldKind.SerialNumber, "Serial number", "Field_SerialNumber",
                """<svg viewBox="0 0 180 150"><path d="M50 18h80v38H50z"/><rect x="28" y="50" width="124" height="68" rx="9"/><path class="screen" d="M48 88h84v44H48z"/><path d="M119 68h14M60 103h60"/><circle cx="42" cy="68" r="3"/></svg>""",
                """<svg viewBox="0 0 180 150"><path d="M52 22h76v31H52z"/><rect x="31" y="51" width="118" height="64" rx="8"/><path d="M49 90h82v38H49z"/></svg>""",
                """<svg viewBox="0 0 180 150"><path d="M53 32h74v21M40 55h100v56H40zM55 91h70v30"/></svg>"""),
            Create(
                "accessory", "Accessory", "Category_Accessory", 50, true, [12],
                WarrantyFieldKind.SerialNumber, "Serial number", "Field_SerialNumber",
                """<svg viewBox="0 0 180 150"><path d="M58 41a32 32 0 0 1 64 0v42a17 17 0 0 1-17 17H75a17 17 0 0 1-17-17z"/><circle class="screen" cx="90" cy="68" r="17"/><path d="M90 100v30M69 130h42"/></svg>""",
                """<svg viewBox="0 0 180 150"><path d="M61 43a29 29 0 0 1 58 0v39a15 15 0 0 1-15 15H76a15 15 0 0 1-15-15zM90 97v30"/></svg>""",
                """<svg viewBox="0 0 180 150"><circle cx="90" cy="66" r="29"/><path d="M90 95v32M72 127h36"/></svg>"""),
            Create(
                "smartwatch", "Smartwatch", "Category_Smartwatch", 60, false, [24],
                WarrantyFieldKind.SerialNumber, "Serial number", "Field_SerialNumber",
                """<svg viewBox="0 0 180 150"><path d="M72 10h36l7 28H65zM65 112h50l-7 28H72z"/><rect x="51" y="34" width="78" height="82" rx="20"/><rect class="screen" x="65" y="48" width="50" height="54" rx="12"/><path d="M129 62h7v24h-7"/></svg>""",
                """<svg viewBox="0 0 180 150"><path d="M74 12h32l6 25H68zM68 113h44l-6 25H74z"/><rect x="54" y="35" width="72" height="80" rx="18"/></svg>""",
                """<svg viewBox="0 0 180 150"><path d="M76 14h28l6 25M70 111l6 25h28l6-25"/><rect x="57" y="36" width="66" height="77" rx="17"/></svg>""")
        ];

        return Array.AsReadOnly(definitions
            .Select(result => result.Match(
                category => category,
                error => throw new InvalidOperationException(
                    $"Built-in category definition is invalid: {error}.")))
            .ToArray());
    }

    private static DomainResult<Category, CategoryError> Create(
        string key,
        string name,
        string nameResourceKey,
        int sortOrder,
        bool showInBar,
        int[] durations,
        WarrantyFieldKind fieldKind,
        string fieldLabel,
        string fieldResourceKey,
        string detailedSvg,
        string simpleSvg,
        string minimalSvg) => FieldSpec.Create(
            fieldKind,
            fieldLabel,
            required: true,
            labelResourceKey: fieldResourceKey).Match(
            field => Category.CreateBuiltIn(
                key,
                name,
                nameResourceKey,
                sortOrder,
                showInBar,
                durations,
                fieldKind == WarrantyFieldKind.Imei
                    ? CategoryIdentifierMode.SerialAndImei
                    : CategoryIdentifierMode.SerialNumber,
                new Dictionary<IllustrationVariant, string>
                {
                    [IllustrationVariant.Detailed] = detailedSvg,
                    [IllustrationVariant.Simple] = simpleSvg,
                    [IllustrationVariant.Minimal] = minimalSvg
                },
                [field]),
            _ => DomainResult<Category, CategoryError>.Failure(CategoryError.FieldInvalid));
}
