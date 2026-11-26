using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Events;

public sealed record PaymentUnappliedEvent(
    PaymentTransactionId PaymentTransactionId,
    PremiId PremiId,
    string OrderId,
    decimal Amount,
    PaymentRejectionReason Reason) : IDomainEvent;
