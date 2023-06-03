// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Images.EventHandlers;

    public class ImageUpdatedEventHandler : INotificationHandler<ImageUpdatedEvent>
    {
        private readonly ILogger<ImageUpdatedEventHandler> _logger;

        public ImageUpdatedEventHandler(
            ILogger<ImageUpdatedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(ImageUpdatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event: {DomainEvent}", notification.GetType().FullName);
            return Task.CompletedTask;
        }
    }
