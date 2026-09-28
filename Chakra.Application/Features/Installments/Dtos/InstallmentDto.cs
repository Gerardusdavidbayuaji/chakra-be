using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Installments.Dtos;

public class InstallmentResponseDto
{
    public Guid Id { get; set; }
    public Guid PremiId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public InstallmentStatus Status { get; set; }
    public int ReminderCount { get; set; }
    public string? MidtransOrderId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}