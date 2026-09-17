using TrackTraceMoney.Domain.CardNetworks;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in the card networks list (README §14 network/brand source data, CreditCard-only).</summary>
public sealed record CardNetworkListItem(Guid Id, string Name)
{
    public static CardNetworkListItem FromDomain(CardNetwork network) =>
        new(network.Id, network.Name);
}
