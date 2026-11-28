namespace Chakra.Application.Features.Payments.Dtos;

public record RefundPaymentRequestDto(string Reference, string? Note);
