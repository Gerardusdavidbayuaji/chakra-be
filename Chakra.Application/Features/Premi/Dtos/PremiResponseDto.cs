using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Premi.Dtos;

public class PremiResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal InstallmentAmount { get; set; }
    public int Tenor { get; set; }
    public int DueDay { get; set; }
    public int GracePeriodDays { get; set; }
    public DateOnly StartDate { get; set; }
    public PremiStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int InstallmentsPaid { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateOnly? NextDueDate { get; set; }
}
