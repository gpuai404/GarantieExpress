namespace GarantieExpress.Domain;

public sealed record CategoryCatalogState
{
    private CategoryCatalogState(
        bool isArchived,
        bool showInBar,
        int sortOrder,
        long version)
    {
        IsArchived = isArchived;
        ShowInBar = showInBar;
        SortOrder = sortOrder;
        Version = version;
    }

    public bool IsArchived { get; }
    public bool ShowInBar { get; }
    public int SortOrder { get; }
    public long Version { get; }

    public static DomainResult<CategoryCatalogState, CategoryError> Create(
        bool isArchived,
        bool showInBar,
        int sortOrder,
        long version = 0)
    {
        if (sortOrder < 0) return Failure(CategoryError.SortOrderInvalid);
        if (version < 0) return Failure(CategoryError.VersionInvalid);
        if (isArchived && showInBar)
            return Failure(CategoryError.ArchivedCategoryCannotBeShown);

        return DomainResult<CategoryCatalogState, CategoryError>.Success(
            new CategoryCatalogState(isArchived, showInBar, sortOrder, version));
    }

    public CategoryCatalogState Archive() =>
        new(isArchived: true, showInBar: false, sortOrder: SortOrder, version: Version);

    public CategoryCatalogState Unarchive() =>
        new(isArchived: false, showInBar: false, sortOrder: SortOrder, version: Version);

    public DomainResult<CategoryCatalogState, CategoryError> WithVersion(long version) =>
        Create(IsArchived, ShowInBar, SortOrder, version);

    private static DomainResult<CategoryCatalogState, CategoryError> Failure(
        CategoryError error) =>
        DomainResult<CategoryCatalogState, CategoryError>.Failure(error);
}
