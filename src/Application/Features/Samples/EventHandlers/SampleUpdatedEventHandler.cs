// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Samples.EventHandlers;

    public class SampleUpdatedEventHandler : INotificationHandler<SampleUpdatedEvent>
    {
        private readonly ILogger<SampleUpdatedEventHandler> _logger;

        public SampleUpdatedEventHandler(
            ILogger<SampleUpdatedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(SampleUpdatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event: {DomainEvent}", notification.GetType().FullName);
            return Task.CompletedTask;
        }
    }
