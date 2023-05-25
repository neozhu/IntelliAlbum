// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Samples.EventHandlers;

public class SampleCreatedEventHandler : INotificationHandler<SampleCreatedEvent>
{
        private readonly ILogger<SampleCreatedEventHandler> _logger;

        public SampleCreatedEventHandler(
            ILogger<SampleCreatedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(SampleCreatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event: {DomainEvent}", notification.GetType().FullName);
            return Task.CompletedTask;
        }
}
