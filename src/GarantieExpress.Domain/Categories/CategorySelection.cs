namespace GarantieExpress.Domain;

public sealed class CategorySelection
{
    public const int MaximumBarItems = 5;

    public CategorySelection(IReadOnlyList<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        Items = Array.AsReadOnly(categories.ToArray());
    }

    public IReadOnlyList<Category> Items { get; }

    public Category? FindActive(string? key) => Items.FirstOrDefault(category =>
        !category.IsArchived
        && string.Equals(category.Key, key, StringComparison.Ordinal));

    public Category SelectOrFirst(string? key, string fallbackKey = "smartphone") =>
        FindActive(key)
        ?? FindActive(fallbackKey)
        ?? Items.FirstOrDefault(category => !category.IsArchived)
        ?? throw new InvalidOperationException("The category catalog has no active categories.");

    public IReadOnlyList<Category> BarItems() => Items
        .Where(category => category.ShowInBar && !category.IsArchived)
        .OrderBy(category => category.SortOrder)
        .ThenBy(category => category.Key, StringComparer.Ordinal)
        .Take(MaximumBarItems)
        .ToList();

    public bool CanShowInBar(string? excludedKey = null) => Items.Count(category =>
        category.ShowInBar
        && !category.IsArchived
        && !string.Equals(category.Key, excludedKey, StringComparison.Ordinal)) < MaximumBarItems;
}
