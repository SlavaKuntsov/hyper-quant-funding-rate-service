using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FRC_Service.Infrastructure.DataAccess.Configurations;

/// <summary>
/// Provides entity type configuration for the <see cref="Exchange"/> entity.
/// Defines the database schema, relationships, and constraints for Exchange records.
/// </summary>
public class ExchangeConfiguration: IEntityTypeConfiguration<Exchange>
{
	public void Configure(EntityTypeBuilder<Exchange> builder)
	{
		builder.HasKey(e => e.Id);
		
		builder.Property(s => s.Code)
			.IsRequired()
			.HasConversion(new EnumToStringConverter<ExchangeCodeType>());
	}
}