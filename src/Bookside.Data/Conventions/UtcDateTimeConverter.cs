using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bookside.Data.Conventions;

/// <summary>Writes UTC (converting local times) and marks values read back as <see cref="DateTimeKind.Utc"/>.</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
