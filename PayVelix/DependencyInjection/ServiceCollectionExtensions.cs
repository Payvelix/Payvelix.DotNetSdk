using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayVelix.Internal;
using PayVelix.Options;

namespace PayVelix.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPayVelix(
        this IServiceCollection services,
        Action<PayVelixOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddHttpClient(PayVelixHttp.HttpClientName, ConfigureHttpClient);

        services.AddSingleton<IPayVelixClientFactory, PayVelixClientFactory>();
        services.AddScoped(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<PayVelixOptions>>()
                .Value;
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(PayVelixHttp.HttpClientName);
            var apiKey = PayVelixConfigurationValidator.ValidateApiKey(options.ApiKey);

            return PayVelixClientFactory.CreateClient(httpClient, apiKey);
        });
        services.AddScoped(serviceProvider => serviceProvider.GetRequiredService<IPayVelixClient>().Balance);
        services.AddScoped(serviceProvider => serviceProvider.GetRequiredService<IPayVelixClient>().Payments);

        return services;
    }

    private static void ConfigureHttpClient(
        IServiceProvider serviceProvider,
        HttpClient httpClient)
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<PayVelixOptions>>()
            .Value;

        httpClient.BaseAddress = PayVelixConfigurationValidator.ValidateBaseUrl(options.BaseUrl);
        httpClient.Timeout = PayVelixConfigurationValidator.ValidateTimeout(options.Timeout);
        PayVelixHttp.ConfigureSharedHeaders(httpClient);
    }
}
