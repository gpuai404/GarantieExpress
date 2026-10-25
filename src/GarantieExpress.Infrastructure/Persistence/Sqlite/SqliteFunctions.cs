using System.Globalization;
using Microsoft.Data.Sqlite;
using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite;

internal static class SqliteFunctions
{
    internal static void Register(SqliteConnection connection)
    {
        connection.CreateFunction(
            "garantie_express_contains",
            static (string? value, string? searchTerm, string? cultureName) =>
                Contains(value, searchTerm, cultureName));

        connection.CreateFunction(
            "garantie_express_product_key",
            static (string? value) => ProductKey(value),
            isDeterministic: true);

    }

    private static int Contains(string? value, string? searchTerm, string? cultureName)
    {
        if (value is null || searchTerm is null || cultureName is null) return 0;

        var compareInfo = CultureInfo.GetCultureInfo(cultureName).CompareInfo;
        return compareInfo.IndexOf(value, searchTerm, CompareOptions.IgnoreCase) >= 0 ? 1 : 0;
    }

    private static string ProductKey(string? value) => Product.NormalizeKey(value);
}
