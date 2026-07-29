using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayVelix.Balance;
using PayVelix.Options;
using PayVelix.Payments;

namespace PayVelix.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPayVelix(
        this IServiceCollection services,
        Action<PayVelixOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddHttpClient<IPayVelixBalanceClient, PayVelixBalanceClient>(ConfigureHttpClient);
        services.AddHttpClient<IPayVelixPaymentsClient, PayVelixPaymentsClient>(ConfigureHttpClient);

        services.AddScoped<IPayVelixClient, PayVelixClient>();

        return services;
    }

    private static void ConfigureHttpClient(
        IServiceProvider serviceProvider,
        HttpClient httpClient)
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<PayVelixOptions>>()
            .Value;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("PayVelix ApiKey is required.");
        }

        httpClient.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
        httpClient.Timeout = options.Timeout;
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static string EnsureTrailingSlash(string url)
    {
        return url.EndsWith('/') ? url : url + "/";
    }
}
