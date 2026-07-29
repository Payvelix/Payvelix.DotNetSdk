namespace PayVelix.Contracts.Payments;

public enum PaymentStatus
{
    Unknown = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Expired = 4,
    Cancelled = 5,
    Mismatch = 6
}
