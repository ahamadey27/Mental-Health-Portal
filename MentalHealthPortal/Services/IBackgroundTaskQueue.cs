// Filepath: c:/Users/hamad/Documents/GitHub/Mental-Health-Portal/MentalHealthPortal/Services/IBackgroundTaskQueue.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MentalHealthPortal.Services
{
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItemAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem);

        ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
    }
}