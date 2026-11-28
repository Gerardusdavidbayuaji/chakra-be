using Chakra.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chakra.Infrastructure.Common;

public sealed class UserIdConverter : ValueConverter<UserId, Guid>
{
    public UserIdConverter() : base(id => id.Value, value => new UserId(value))
    {
    }
}

public sealed class PremiIdConverter : ValueConverter<PremiId, Guid>
{
    public PremiIdConverter() : base(id => id.Value, value => new PremiId(value))
    {
    }
}

public sealed class InstallmentIdConverter : ValueConverter<InstallmentId, Guid>
{
    public InstallmentIdConverter() : base(id => id.Value, value => new InstallmentId(value))
    {
    }
}

public sealed class OutboxMessageIdConverter : ValueConverter<OutboxMessageId, Guid>
{
    public OutboxMessageIdConverter() : base(id => id.Value, value => new OutboxMessageId(value))
    {
    }
}

public sealed class PaymentTransactionIdConverter : ValueConverter<PaymentTransactionId, Guid>
{
    public PaymentTransactionIdConverter() : base(id => id.Value, value => new PaymentTransactionId(value))
    {
    }
}

public sealed class JobRunIdConverter : ValueConverter<JobRunId, Guid>
{
    public JobRunIdConverter() : base(id => id.Value, value => new JobRunId(value))
    {
    }
}
