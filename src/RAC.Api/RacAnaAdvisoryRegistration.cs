using RAC.Infrastructure;

namespace RAC.Api;

public static class RacAnaAdvisoryRegistration
{
    public static IServiceCollection AddRacAnaAdvisory(this IServiceCollection services,IConfiguration configuration)
    {
        if(!configuration.GetValue<bool>("Specialists:AnaAdvisory:Enabled",false)) return services;
        services.AddSingleton<RacAgentMessageStore>();
        services.AddSingleton<RebecaMarketEvidenceReader>();
        services.AddSingleton<RebecaMarketAdvisory>();
        services.AddSingleton<RebecaMarketAdvisoryPublisher>();        services.AddSingleton<RacAnaRecentReview>();
        services.AddSingleton<RacAnaAdvisoryStore>();
        services.AddSingleton<RacAnaAdvisoryCycle>();
        services.AddHostedService<RacAnaAdvisoryWorker>(sp=>new RacAnaAdvisoryWorker(
            sp.GetRequiredService<RacAnaAdvisoryCycle>().RunAsync,
            sp.GetRequiredService<ILogger<RacAnaAdvisoryWorker>>()));
        return services;
    }
}
