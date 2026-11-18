using System.Threading;
using System.Threading.Tasks;

namespace Application.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default);
}
