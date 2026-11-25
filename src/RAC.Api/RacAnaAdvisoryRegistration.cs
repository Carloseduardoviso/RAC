using RAC.Infrastructure;
using RAC.Application;

namespace RAC.Api;

public static class RacAnaAdvisoryRegistration
{
    public static IServiceCollection AddRacAnaAdvisory(this IServiceCollection services,IConfiguration configuration)
    {
        if(!configuration.GetValue<bool>("Specialists:AnaAdvisory:Enabled",false)) return services;
        services.AddSingleton<RacAgentMessageStore>();
        services.AddSingleton<IRacNewsCalendar>(sp=>sp.GetRequiredService<RebecaNewsCalendar>());
        services.AddSingleton<RebecaMarketEvidenceReader>();
        services.AddSingleton<RebecaMarketAdvisory>();
        services.AddSingleton<RebecaMarketAdvisoryPublisher>();
        services.AddSingleton<RacAnaRecentReview>();
        services.AddSingleton<RacAnaAdvisoryStore>();
        if(configuration.GetValue<bool>("Specialists:AnaAdvisory:ReviewRequestsEnabled",false))
            services.AddSingleton<RacAnaReviewRequestStore>();
        // Separate opt-in; never enables trading or request generation.
        if(configuration.GetValue<bool>("Specialists:AnaAdvisory:CarlosBatchEnabled",false))
        {
            services.AddSingleton<CarlosRafaelEvidenceReader>();
            services.AddSingleton<CarlosSamuelEvidenceReader>();
            services.AddSingleton<CarlosMiguelEvidenceReader>();
            services.AddSingleton<CarlosFinancialEvidenceService>();
            services.AddSingleton<CarlosFinancialAdvisoryPublisher>();
            services.AddSingleton<CarlosReviewRequestPublisher>();
            services.AddSingleton<CarlosReviewRequestBatch>();
        }
        services.AddSingleton<RacAnaAdvisoryCycle>();
        services.AddHostedService<RacAnaAdvisoryWorker>(sp=>new RacAnaAdvisoryWorker(
            sp.GetRequiredService<RacAnaAdvisoryCycle>().RunAsync,
            sp.GetRequiredService<ILogger<RacAnaAdvisoryWorker>>()));
        return services;
    }
}
