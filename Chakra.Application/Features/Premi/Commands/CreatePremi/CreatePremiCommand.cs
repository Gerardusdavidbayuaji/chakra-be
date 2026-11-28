using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Commands.CreatePremi;

public class CreatePremiCommand : IAdminRequest<Result<PremiResponseDto>>
{
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public int Tenor { get; set; }
    public int DueDay { get; set; }
    public int GracePeriodDays { get; set; }
    public DateOnly StartDate { get; set; }
}
