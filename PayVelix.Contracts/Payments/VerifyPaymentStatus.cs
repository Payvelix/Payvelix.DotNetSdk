namespace PayVelix.Contracts.Payments;

public enum VerifyPaymentStatus
{
    Pending,
    Paid,
    Mismatch,
    Expired,
    Cancelled
}
