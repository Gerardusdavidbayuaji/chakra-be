using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Events;

public sealed record InstallmentPaidEvent(
    InstallmentId InstallmentId,
    PremiId PremiId,
    UserId UserId,
    int InstallmentNumber,
    decimal Amount,
    bool PremiCompleted,
    string? MidtransOrderId) : IDomainEvent;
