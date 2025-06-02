using FRC_Service.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FRC_Service.Infrastructure.DataAccess.Configurations;

/// <summary>
/// Provides entity type configuration for the <see cref="FundingRateOnline"/> entity.
/// Defines the database schema, relationships, and constraints for FundingRates records.
/// </summary>
public class FundingRateOnlineConfiguration : IEntityTypeConfiguration<FundingRateOnline>
{
	public void Configure(EntityTypeBuilder<FundingRateOnline> builder)
	{
		builder.HasKey(e => e.Id);

		builder.Property(e => e.Symbol)
			.IsRequired();

		builder.Property(e => e.Name)
			.IsRequired();

		builder.Property(e => e.ExchangeId)
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
		
		builder.HasIndex(e => new { e.Symbol, e.ExchangeId })
			.IsUnique()
			.HasDatabaseName("ix_fr_online_symbol_exchange_unique");

		builder.HasIndex(e => new { e.Name, e.ExchangeId })
			.IsUnique()
			.HasDatabaseName("ix_fr_online_name_exchange_unique");

		builder.HasIndex(e => e.Symbol)
			.HasDatabaseName("ix_fr_online_symbol");

		builder.HasIndex(e => e.TsRate)
			.HasDatabaseName("ix_fr_online_tsrate")
			.IsDescending(true);
	}
}