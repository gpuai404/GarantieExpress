using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Payloads;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

internal static class WarrantySnapshotReader
{
    internal static OperationResult<WarrantySnapshot> Read(
        string payload,
        string expectedId)
    {
        var restored = WarrantySnapshotPayloadMapper.ToDomain(
            SqliteStoredData.Deserialize<WarrantySnapshotPayload>(payload),
            expectedId);
        return restored.Match(
            snapshot => string.Equals(
                WarrantySnapshotHasher.Compute(snapshot),
                expectedId,
                StringComparison.Ordinal)
                ? OperationResult<WarrantySnapshot>.Success(snapshot)
                : Invalid(),
            error => Invalid(error.ToString()));
    }

    private static OperationResult<WarrantySnapshot> Invalid(string? detail = null) =>
        OperationResult<WarrantySnapshot>.Failure(new OperationError(
            detail is null ? "Storage.InvalidSnapshot" : $"Storage.InvalidSnapshot.{detail}",
            "A stored warranty snapshot is invalid."));
}
