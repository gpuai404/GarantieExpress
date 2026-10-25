using GarantieExpress.Domain;

namespace GarantieExpress.Application.Initialization;

public sealed record InitialApplicationData(
    StoreSettings Settings,
    IReadOnlyList<Category> Categories);
