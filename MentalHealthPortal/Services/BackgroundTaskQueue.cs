// Filepath: c:/Users/hamad/Documents/GitHub/Mental-Health-Portal/MentalHealthPortal/Services/BackgroundTaskQueue.cs
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MentalHealthPortal.Services
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<IServiceProvider, CancellationToken, ValueTask>> _queue;

        // Constructor with a default capacity for the queue
        public BackgroundTaskQueue() : this(100) // Default capacity of 100, can be configured
        {
        }

        // Constructor allowing capacity to be set
        public BackgroundTaskQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }

            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait // Wait for space if the queue is full
            };
            _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, ValueTask>>(options);
        }

        public void QueueBackgroundWorkItemAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            // TryWrite is non-blocking. Given FullMode = Wait, it should generally succeed 
            // unless the channel is completed. For robustness, error handling or logging 
            // could be added if TryWrite returns false.
            if (!_queue.Writer.TryWrite(workItem))
            {
                // This might happen if the channel is completed.
                // Consider logging this or throwing a more specific exception if needed.
                Console.WriteLine("Warning: Failed to queue background work item. The queue might be full or completed.");
                // Depending on requirements, you might throw new InvalidOperationException("Failed to queue background work item.");
            }
        }

        public async ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
        {
            // ReadAsync waits until an item is available or the channel is completed.
            // It will throw OperationCanceledException if cancellationToken is signaled.
            var workItem = await _queue.Reader.ReadAsync(cancellationToken);
            return workItem;
        }
    }
}