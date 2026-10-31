using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class FieldSpecPayload
{
    public WarrantyFieldKind Kind { get; set; }
    public string? Label { get; set; }
    public bool Required { get; set; } = true;
    public string? LabelResourceKey { get; set; }
}

internal static class FieldSpecPayloadMapper
{
    public static DomainResult<IReadOnlyList<FieldSpec>, FieldSpecError> ToDomain(
        IReadOnlyList<FieldSpecPayload>? payloads)
    {
        if (payloads is null)
            return Failure(FieldSpecError.LabelInvalid);

        var fields = new List<FieldSpec>(payloads.Count);
        foreach (var payload in payloads)
        {
            if (payload is null) return Failure(FieldSpecError.LabelInvalid);

            var field = FieldSpec.Create(
                payload.Kind,
                payload.Label,
                payload.Required,
                payload.LabelResourceKey);
            if (field is DomainResult<FieldSpec, FieldSpecError>.Failed failed)
                return Failure(failed.Error);

            fields.Add(((DomainResult<FieldSpec, FieldSpecError>.Succeeded)field).Value);
        }

        return DomainResult<IReadOnlyList<FieldSpec>, FieldSpecError>.Success(fields);
    }

    public static FieldSpecPayload FromDomain(FieldSpec field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return new FieldSpecPayload
        {
            Kind = field.Kind,
            Label = field.Label,
            Required = field.Required,
            LabelResourceKey = field.LabelResourceKey
        };
    }

    private static DomainResult<IReadOnlyList<FieldSpec>, FieldSpecError> Failure(
        FieldSpecError error) =>
        DomainResult<IReadOnlyList<FieldSpec>, FieldSpecError>.Failure(error);
}
