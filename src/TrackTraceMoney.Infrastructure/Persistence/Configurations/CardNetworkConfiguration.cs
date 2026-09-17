using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CardNetworks;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class CardNetworkConfiguration : IEntityTypeConfiguration<CardNetwork>
{
    public void Configure(EntityTypeBuilder<CardNetwork> builder)
    {
        builder.ToTable("CardNetworks");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Name).IsRequired().HasMaxLength(200);
    }
}
