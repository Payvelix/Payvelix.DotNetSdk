using PayVelix.Balance;
using PayVelix.Payments;

namespace PayVelix;

public interface IPayVelixClient
{
    IPayVelixBalanceClient Balance { get; }

    IPayVelixPaymentsClient Payments { get; }
}
