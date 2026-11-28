using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;

namespace Chakra.Application.Features.Payments.Queries.GetUnappliedPayments;

public class GetUnappliedPaymentsQuery : PaginatedRequest, IAdminRequest<Result<PaginatedResult<UnappliedPaymentDto>>>;
