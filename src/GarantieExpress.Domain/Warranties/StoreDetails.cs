namespace GarantieExpress.Domain;

public sealed record StoreDetails(
    string Name,
    string Slogan,
    IReadOnlyList<string> Services,
    string Address,
    string Phone,
    string Vat);
