using Bookside.Core.Challenge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class ReadingGoalConfiguration : IEntityTypeConfiguration<ReadingGoal>
{
    public void Configure(EntityTypeBuilder<ReadingGoal> builder)
    {
        builder.ToTable("ReadingGoals", t =>
        {
            t.HasCheckConstraint(
                "CK_ReadingGoals_Year",
                $"Year BETWEEN {ReadingGoal.MinYear} AND {ReadingGoal.MaxYear}");
            t.HasCheckConstraint(
                "CK_ReadingGoals_TargetBooks",
                $"TargetBooks BETWEEN {ReadingGoal.MinTarget} AND {ReadingGoal.MaxTarget}");
        });
        builder.ConfigureEntity();

        builder.HasIndex(g => g.Year).IsUnique();
    }
}
