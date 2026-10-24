using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class ReadingSessionConfiguration : IEntityTypeConfiguration<ReadingSession>
{
    public void Configure(EntityTypeBuilder<ReadingSession> builder)
    {
        builder.ToTable("ReadingSessions", t =>
        {
            t.HasCheckConstraint(
                "CK_ReadingSessions_Rating",
                $"Rating IS NULL OR Rating BETWEEN {ReadingSession.MinRating} AND {ReadingSession.MaxRating}");
            t.HasCheckConstraint(
                "CK_ReadingSessions_Dates",
                "StartedOn IS NULL OR FinishedOn IS NULL OR FinishedOn >= StartedOn");
        });
        builder.ConfigureEntity();

        builder.Property(s => s.Notes).HasMaxLength(10_000);

        builder.HasIndex(s => new { s.BookId, s.StartedOn });
        builder.HasIndex(s => s.FinishedOn);
    }
}
