using FRC_Service.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FRC_Service.Infrastructure.DataAccess.Configurations;

/// <summary>
/// Provides entity type configuration for the <see cref="FundingRateHistory"/> entity.
/// Defines the database schema, relationships, and constraints for FundingRates records.
/// </summary>
public class FundingRateHistoryConfiguration : IEntityTypeConfiguration<FundingRateHistory>
{
	public void Configure(EntityTypeBuilder<FundingRateHistory> builder)
	{
		builder.HasKey(e => e.Id);

		builder.Property(e => e.ExchangeId)
			.IsRequired();

		builder.Property(e => e.Symbol)
			.IsRequired();

		builder.Property(e => e.Name)
			.IsRequired();

		builder.Property(e => e.IntervalHours)
			.IsRequired();

		builder.Property(e => e.Rate)
			.IsRequired();

		builder.Property(e => e.OpenInterest)
			.IsRequired();

		builder.Property(e => e.TsRate)
			.IsRequired();

		builder.Property(e => e.FetchedAt)
			.IsRequired();

		builder.HasOne(e => e.Exchange)
			.WithMany()
			.HasForeignKey(e => e.ExchangeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(e => new { e.Symbol, e.ExchangeId, e.TsRate })
			.HasDatabaseName("ix_fr_history")
			.IsDescending(false, false, true);
	}
}