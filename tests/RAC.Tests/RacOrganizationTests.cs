using RAC.Application;
using Xunit;

public class RacOrganizationTests
{
    [Fact]
    public void InitialOrganizationHasUniqueAgentsAndValidReportingLines()
    {
        Assert.Equal(3, RacOrganization.Leaders.Count);
        Assert.Equal(6, RacOrganization.Specialists.Count);
        Assert.Equal(12, RacOrganization.InitialTradingAgents.Count);
        Assert.Equal(21, RacOrganization.All.Count);
        Assert.Equal(21, RacOrganization.All.Select(x => x.Id).Distinct().Count());
        Assert.Single(RacOrganization.All.Where(x => x.ReportsTo is null));
        foreach (var agent in RacOrganization.All.Where(x => x.ReportsTo is not null))
            Assert.Contains(RacOrganization.Leaders, x => x.Id == agent.ReportsTo);
    }
    [Fact]
    public void SpecialistRolesAreDistinctAndTradingAgentsArePaperOnly()
    {
        Assert.Equal(6, RacOrganization.Specialists.Select(x => x.Role).Distinct().Count());
        Assert.All(RacOrganization.InitialTradingAgents, x => Assert.Equal("PAPER_TRADING", x.Role));
    }
}
