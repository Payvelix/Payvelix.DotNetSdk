using PayVelix.Contracts.Balance;

namespace PayVelix.Balance;

public interface IPayVelixBalanceClient
{
    Task<BalanceResponse> GetAsync(
        string? id = null,
        CancellationToken cancellationToken = default);
}
