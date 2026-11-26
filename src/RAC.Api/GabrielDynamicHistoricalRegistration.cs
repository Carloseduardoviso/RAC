using RAC.Infrastructure;
namespace RAC.Api;

public static class GabrielDynamicHistoricalRegistration
{
    public static IServiceCollection AddGabrielDynamicHistoricalSource(this IServiceCollection services,IConfiguration configuration)
    {
        if(!configuration.GetValue<bool>("Specialists:Gabriel:DynamicHistoricalEnabled",false)) return services;
        services.AddHttpClient<GabrielPublicArchiveClient>()
            .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false});
        var persist=configuration.GetValue<bool>("Specialists:Gabriel:QuantitativeMemoryEnabled",false);
        if(persist) { services.AddSingleton<ResearchQuantitativeExecutionStore>(); services.AddSingleton<ResearchExecutionManifestStore>(); }
        var freeze=configuration.GetValue<bool>("Specialists:Gabriel:IndependentFreezeEnabled",false);
        if(freeze && !persist) throw new InvalidOperationException("Independent freeze requires quantitative memory.");
        if(freeze)
            services.AddSingleton<IndependentResearchFreezeStore>(sp=>new(sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<RacDbContext>>(),TimeProvider.System));
        services.AddSingleton<GabrielDynamicHistoricalSource>(sp=>new(
            sp.GetRequiredService<GabrielPublicArchiveClient>(),
            persist?sp.GetRequiredService<ResearchQuantitativeExecutionStore>():null,
            persist?sp.GetRequiredService<ResearchExecutionManifestStore>():null, freeze?sp.GetRequiredService<IndependentResearchFreezeStore>():null));
        services.AddSingleton<Func<IReadOnlyList<string>,CancellationToken,Task<string>>>(sp=>
            sp.GetRequiredService<GabrielDynamicHistoricalSource>().ReadAsync);
        return services;
    }
}
