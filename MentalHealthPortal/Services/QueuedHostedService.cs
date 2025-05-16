using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; // Required for IServiceProvider and CreateScope
using Microsoft.Extensions.Hosting; // Required for BackgroundService
using Microsoft.Extensions.Logging; // Required for ILogger

namespace MentalHealthPortal.Services
{
    // This class is a background service that processes tasks from the IBackgroundTaskQueue.
    // It inherits from BackgroundService, which is the recommended base class for long-running IHostedService tasks.

    public class QueuedHostedService : BackgroundService
    {
        // Logger for logging information and errors.
        private readonly ILogger<QueuedHostedService> _logger;
        // Service provider to create scopes for processing tasks.
        // Each task will be processed in its own dependency injection scope to ensure services like DbContext are correctly managed.

        private readonly IServiceProvider _serviceProvider;
        //The queue from which task will be dequed 
        public IBackgroundTaskQueue TaskQueue { get; }

        // Constructor: Initializes the service with its dependencies.
        // These dependencies (logger, taskQueue, serviceProvider) are injected by the DI container.
        public QueuedHostedService(
            ILogger<QueuedHostedService> logger,
            IBackgroundTaskQueue taskQueue,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            TaskQueue = taskQueue;
            _serviceProvider = serviceProvider;
        }

        // This method is called when the IHostedService starts.
        // It contains the main logic for the background service.
        // stoppingToken: A CancellationToken that is triggered when the application is shutting down.

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is starting.");

            // Loop indefinitely until the application is requested to stop.
            while (!stoppingToken.IsCancellationRequested)
            {
                // Dequeue a work item. This will wait if the queue is empty.
                // The workItem is a Func<IServiceProvider, CancellationToken, ValueTask>
                var workItem = await TaskQueue.DequeueAsync(stoppingToken);

                try
                {
                    // Create a new DI scope for executing the work item.
                    // This is important because services like ApplicationDbContext are typically scoped,
                    // meaning they should have a lifetime tied to a specific operation (like processing one queue item).
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // Execute the work item, passing in the scoped service provider and the stopping token.
                        // The work item itself will resolve its necessary services (like TextExtractionService and ApplicationDbContext)
                        // from this 'scope.ServiceProvider'.
                        await workItem(scope.ServiceProvider, stoppingToken);
                    }
                    _logger.LogInformation("Successfully processed a background work item.");
                }
                catch (OperationCanceledException)
                {
                    // Prevent throwing if stoppingToken was signaled
                    _logger.LogWarning("Background work item processing was canceled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing background work item.");
                }
            }

            _logger.LogInformation("Queued Hosted Service is stopping.");
        }
    }

}