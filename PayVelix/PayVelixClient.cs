using PayVelix.Balance;
using PayVelix.Payments;

namespace PayVelix;

internal sealed class PayVelixClient : IPayVelixClient
{
    public PayVelixClient(
        IPayVelixBalanceClient balance,
        IPayVelixPaymentsClient payments)
    {
        Balance = balance;
        Payments = payments;
    }

    public IPayVelixBalanceClient Balance { get; }

    public IPayVelixPaymentsClient Payments { get; }
}
