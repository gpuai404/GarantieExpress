using System.Globalization;
using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

internal static class WarrantyRowMapper
{
    private const int Number = 0;
    private const int Year = 1;
    private const int Sequence = 2;
    private const int CategoryKey = 3;
    private const int BrandModel = 4;
    private const int Serial = 5;
    private const int PurchaseDate = 6;
    private const int PriceCents = 7;
    private const int DurationMonths = 8;
    private const int IssuedAt = 9;
    private const int Status = 10;
    private const int LastTemplateKey = 11;
    private const int SnapshotId = 12;
    private const int ValidFrom = 13;
    private const int ValidUntil = 14;
    private const int ServiceType = 15;
    private const int RepairNote = 16;
    private const int Version = 17;

    internal const string Columns = """
        Number, Year, Sequence, CategoryKey, BrandModel, Serial, PurchaseDate,
        PriceCents, DurationMonths, IssuedAt, Status, LastTemplateKey, SnapshotId,
        ValidFrom, ValidUntil, ServiceType, RepairNote, Version
        """;

    internal static OperationResult<WarrantyRecord> Read(SqliteDataReader reader, int offset = 0)
    {
        try
        {
            return ReadCore(reader, offset);
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidCastException or OverflowException)
        {
            return Invalid("DecodeFailed");
        }
    }

    internal static void AddParameters(SqliteCommand command, WarrantyRecord warranty)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(warranty);

        command.Parameters.AddWithValue("$number", warranty.Number);
        command.Parameters.AddWithValue("$year", warranty.Year);
        command.Parameters.AddWithValue("$sequence", warranty.Sequence);
        command.Parameters.AddWithValue("$category", warranty.CategoryKey);
        command.Parameters.AddWithValue("$brand", warranty.BrandModel);
        command.Parameters.AddWithValue("$serial", warranty.Serial);
        command.Parameters.AddWithValue(
            "$purchase",
            warranty.PurchaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$price", warranty.PriceCents);
        command.Parameters.AddWithValue("$duration", warranty.DurationMonths);
        command.Parameters.AddWithValue("$issued", warranty.IssuedAt.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)warranty.Status);
        command.Parameters.AddWithValue("$template", warranty.LastTemplateKey);
        command.Parameters.AddWithValue("$snapshotId", warranty.SnapshotId);
        command.Parameters.AddWithValue(
            "$validFrom",
            (object?)warranty.ValidFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$validUntil",
            (object?)warranty.ValidUntil?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$serviceType",
            warranty.ServiceType.HasValue
                ? (object)(int)warranty.ServiceType.Value
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$repairNote",
            (object?)warranty.RepairNote ?? DBNull.Value);
    }

    private static OperationResult<WarrantyRecord> ReadCore(SqliteDataReader reader, int offset)
    {
        var number = WarrantyNumber.Restore(
            reader.GetString(offset + Number),
            reader.GetInt32(offset + Year),
            reader.GetInt32(offset + Sequence));
        if (number is not DomainResult<WarrantyNumber, WarrantyRecordError>.Succeeded numberSucceeded)
            return Invalid(((DomainResult<WarrantyNumber, WarrantyRecordError>.Failed)number).Error.ToString());

        var category = global::GarantieExpress.Domain.CategoryKey.Create(
            reader.GetString(offset + CategoryKey));
        if (category is not DomainResult<global::GarantieExpress.Domain.CategoryKey, CategoryKeyError>.Succeeded categorySucceeded)
            return Invalid(WarrantyRecordError.CategoryInvalid.ToString());

        var price = WarrantyPrice.Create(reader.GetInt64(offset + PriceCents));
        if (price is not DomainResult<WarrantyPrice, WarrantyIssuanceError>.Succeeded priceSucceeded)
            return Invalid(WarrantyRecordError.PriceInvalid.ToString());

        var duration = WarrantyDuration.Create(reader.GetInt32(offset + DurationMonths));
        if (duration is not DomainResult<WarrantyDuration, WarrantyIssuanceError>.Succeeded durationSucceeded)
            return Invalid(WarrantyRecordError.DurationInvalid.ToString());

        if (!TryCreatePeriod(
            ReadDate(reader, offset + ValidFrom),
            ReadDate(reader, offset + ValidUntil),
            durationSucceeded.Value,
            out var period))
        {
            return Invalid(WarrantyRecordError.CoveragePeriodInvalid.ToString());
        }

        var state = WarrantyRecordState.Create(
            (WarrantyStatus)reader.GetInt32(offset + Status),
            reader.GetInt64(offset + Version));
        if (state is not DomainResult<WarrantyRecordState, WarrantyRecordError>.Succeeded stateSucceeded)
            return Invalid(((DomainResult<WarrantyRecordState, WarrantyRecordError>.Failed)state).Error.ToString());

        var restored = WarrantyRecord.Restore(
            numberSucceeded.Value,
            categorySucceeded.Value,
            reader.GetString(offset + BrandModel),
            reader.GetString(offset + Serial),
            DateOnly.ParseExact(
                reader.GetString(offset + PurchaseDate),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            priceSucceeded.Value,
            durationSucceeded.Value,
            DateTimeOffset.Parse(
                reader.GetString(offset + IssuedAt),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            reader.GetString(offset + LastTemplateKey),
            reader.GetString(offset + SnapshotId),
            period,
            reader.IsDBNull(offset + ServiceType)
                ? null
                : (WarrantyServiceType)reader.GetInt32(offset + ServiceType),
            reader.IsDBNull(offset + RepairNote)
                ? null
                : reader.GetString(offset + RepairNote),
            stateSucceeded.Value);

        return restored.Match(
            OperationResult<WarrantyRecord>.Success,
            error => Invalid(error.ToString()));
    }

    private static OperationResult<WarrantyRecord> Invalid(string detail) =>
        OperationResult<WarrantyRecord>.Failure(
            new OperationError($"Storage.InvalidWarranty.{detail}", "A stored warranty is invalid."));

    private static DateOnly? ReadDate(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal)
        ? null
        : DateOnly.ParseExact(reader.GetString(ordinal), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryCreatePeriod(
        DateOnly? validFrom,
        DateOnly? validUntil,
        WarrantyDuration duration,
        out WarrantyPeriod? restored)
    {
        restored = null;
        if (validFrom is null && validUntil is null)
            return true;
        if (validFrom is null || validUntil is null)
            return false;

        var period = WarrantyPeriod.Create(validFrom, duration);
        if (period is not DomainResult<WarrantyPeriod, WarrantyIssuanceError>.Succeeded succeeded
            || succeeded.Value.ValidUntil != validUntil)
        {
            return false;
        }

        restored = succeeded.Value;
        return true;
    }
}
