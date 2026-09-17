using TrackTraceMoney.Domain.Institutions;

namespace TrackTraceMoney.Domain.Tests.Institutions;

public sealed class FinancialInstitutionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new FinancialInstitution(name));
    }

    [Fact]
    public void Constructor_WithOversizedName_Throws()
    {
        var name = new string('A', 201);

        Assert.Throws<ArgumentException>(() => new FinancialInstitution(name));
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var institution = new FinancialInstitution("  Banco Agrícola  ");

        Assert.Equal("Banco Agrícola", institution.Name);
    }

    [Fact]
    public void Rename_WithEmptyName_Throws()
    {
        var institution = new FinancialInstitution("Banco Agrícola");

        Assert.Throws<ArgumentException>(() => institution.Rename(" "));
    }

    [Fact]
    public void Rename_WithOversizedName_Throws()
    {
        var institution = new FinancialInstitution("Banco Agrícola");

        Assert.Throws<ArgumentException>(() => institution.Rename(new string('B', 201)));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var institution = new FinancialInstitution("Banco Agrícola");

        institution.Rename("Banco Industrial");

        Assert.Equal("Banco Industrial", institution.Name);
    }
}
