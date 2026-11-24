namespace RAC.Infrastructure;
public static class ResearchTopicRotation
{
    private static readonly string[] Topics=[
        "cryptocurrency quantitative trading risk",
        "financial machine learning out of sample validation",
        "cryptocurrency technical indicators empirical performance",
        "portfolio risk management drawdown",
        "financial news sentiment cryptocurrency",
        "cryptocurrency market microstructure spot trading",
        "algorithmic trading transaction costs slippage"];
    public static string ForDate(DateTimeOffset now)
    {
        var day=now.UtcDateTime.Date.Ticks/TimeSpan.TicksPerDay;
        return Topics[(int)(day%Topics.Length)];
    }
    public static Uri EndpointFor(DateTimeOffset now)=>EndpointForTopic(ForDate(now));
    public static Uri EndpointForTopic(string topic)=>new(
        "https://api.crossref.org/works?query="+Uri.EscapeDataString(Topics.Contains(topic,StringComparer.Ordinal)?topic:throw new ArgumentException("Unknown research topic"))+
        "&rows=20&select=URL,link,DOI,title,author,published");
}
