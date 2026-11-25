using RAC.Infrastructure;
namespace RAC.Api;

public static class GabrielDynamicHistoricalRegistration
{
    public static IServiceCollection AddGabrielDynamicHistoricalSource(this IServiceCollection services,IConfiguration configuration)
    {
        if(!configuration.GetValue<bool>("Specialists:Gabriel:DynamicHistoricalEnabled",false)) return services;
        services.AddHttpClient<GabrielPublicArchiveClient>()
            .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false});
        services.AddSingleton<GabrielDynamicHistoricalSource>();
        services.AddSingleton<Func<IReadOnlyList<string>,CancellationToken,Task<string>>>(sp=>
            sp.GetRequiredService<GabrielDynamicHistoricalSource>().ReadAsync);
        return services;
    }
}
