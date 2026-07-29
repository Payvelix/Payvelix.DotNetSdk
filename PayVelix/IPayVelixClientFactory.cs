namespace PayVelix;

public interface IPayVelixClientFactory
{
    IPayVelixClient CreateClient(string apiKey);

    IPayVelixClient CreateClient(PayVelixClientConfiguration configuration);
}
