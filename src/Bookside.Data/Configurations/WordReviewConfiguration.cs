using System.Globalization;
using Bookside.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class WordReviewConfiguration : IEntityTypeConfiguration<WordReview>
{
    public void Configure(EntityTypeBuilder<WordReview> builder)
    {
        builder.ToTable("WordReviews", t =>
        {
            t.HasCheckConstraint(
                "CK_WordReviews_Ease",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ease >= {ReviewScheduler.MinEase}"));
            t.HasCheckConstraint("CK_WordReviews_IntervalDays", "IntervalDays >= 0");
        });
        builder.HasKey(r => r.WordId);

        builder.HasOne(r => r.Word)
            .WithOne(w => w.Review)
            .HasForeignKey<WordReview>(r => r.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.DueOn);
        builder.HasIndex(r => r.FirstReviewedOn);
    }
}
