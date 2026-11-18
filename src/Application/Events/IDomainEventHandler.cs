using System.Threading;
using System.Threading.Tasks;

namespace Application.Events;

public interface IDomainEventHandler<in TEvent>
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
