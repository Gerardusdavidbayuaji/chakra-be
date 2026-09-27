namespace Chakra.Application.Features.Premi.Dtos;

public class UpdatePremiRequestDto
{
    public Guid Id { get; set; }
    public decimal TotalAmount { get; set; }
    public int Tenor { get; set; }
    public int DueDay { get; set; }
    public int GracePeriodDays { get; set; }
    public DateOnly StartDate { get; set; }
}