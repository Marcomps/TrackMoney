using TrackTraceMoney.Domain.CardNetworks;

namespace TrackTraceMoney.Domain.Tests.CardNetworks;

public sealed class CardNetworkTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new CardNetwork(name));
    }

    [Fact]
    public void Constructor_WithOversizedName_Throws()
    {
        var name = new string('A', 201);

        Assert.Throws<ArgumentException>(() => new CardNetwork(name));
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var network = new CardNetwork("  Visa  ");

        Assert.Equal("Visa", network.Name);
    }

    [Fact]
    public void Rename_WithEmptyName_Throws()
    {
        var network = new CardNetwork("Visa");

        Assert.Throws<ArgumentException>(() => network.Rename(" "));
    }

    [Fact]
    public void Rename_WithOversizedName_Throws()
    {
        var network = new CardNetwork("Visa");

        Assert.Throws<ArgumentException>(() => network.Rename(new string('B', 201)));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var network = new CardNetwork("Visa");

        network.Rename("Mastercard");

        Assert.Equal("Mastercard", network.Name);
    }
}
