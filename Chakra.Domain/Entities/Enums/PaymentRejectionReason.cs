namespace Chakra.Domain.Entities.Enums;

public enum PaymentRejectionReason
{
    PremiNotActive,
    InstallmentNotFound,
    InstallmentAlreadyPaid,
    PreviousInstallmentUnpaid,
    AmountMismatch
}
