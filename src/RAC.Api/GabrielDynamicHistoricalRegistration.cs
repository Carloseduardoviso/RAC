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
        services.AddSingleton<GabrielDynamicHistoricalSource>(sp=>new(
            sp.GetRequiredService<GabrielPublicArchiveClient>(),
            persist?sp.GetRequiredService<ResearchQuantitativeExecutionStore>():null,
            persist?sp.GetRequiredService<ResearchExecutionManifestStore>():null));
        services.AddSingleton<Func<IReadOnlyList<string>,CancellationToken,Task<string>>>(sp=>
            sp.GetRequiredService<GabrielDynamicHistoricalSource>().ReadAsync);
        return services;
    }
}
