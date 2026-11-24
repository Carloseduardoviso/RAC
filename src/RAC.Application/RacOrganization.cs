namespace RAC.Application;

public static class RacOrganization
{
    public sealed record Agent(string Id, string Name, string Role, string? ReportsTo);
    public static IReadOnlyList<Agent> Leaders { get; } = Array.AsReadOnly(new Agent[]
    {
        new("ana", "Ana", "COORDINATION", null),
        new("rebeca", "Rebeca", "RESEARCH", "ana"),
        new("carlos", "Carlos", "RISK", "ana")
    });
    public static IReadOnlyList<Agent> Specialists { get; } = Array.AsReadOnly(new Agent[]
    {
        new("daniel", "Daniel", "MARKET_DISCOVERY", "rebeca"),
        new("gabriel", "Gabriel", "QUANTITATIVE_RESEARCH", "rebeca"),
        new("rafael", "Rafael", "CAPITAL_ANALYSIS", "carlos"),
        new("samuel", "Samuel", "LIQUIDITY_COST_ANALYSIS", "carlos"),
        new("miguel", "Miguel", "AUDIT", "carlos"),
        new("lucas", "Lucas", "EXCHANGE_HEALTH", "ana")
    });
    public static IReadOnlyList<Agent> InitialTradingAgents { get; } =
        Array.AsReadOnly(new[] { "BTC", "ETH", "SOL", "BNB", "XRP", "ADA", "DOGE", "AVAX", "LINK", "DOT", "TRX", "LTC" }
            .Select(symbol => new Agent("market-" + symbol.ToLowerInvariant(), symbol, "PAPER_TRADING", "ana")).ToArray());
    public static IReadOnlyList<Agent> All { get; } =
        Array.AsReadOnly(Leaders.Concat(Specialists).Concat(InitialTradingAgents).ToArray());
}
