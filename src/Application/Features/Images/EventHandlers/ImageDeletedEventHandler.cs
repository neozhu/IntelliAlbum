// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Images.EventHandlers;

    public class ImageDeletedEventHandler : INotificationHandler<ImageDeletedEvent>
    {
        private readonly ILogger<ImageDeletedEventHandler> _logger;

        public ImageDeletedEventHandler(
            ILogger<ImageDeletedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(ImageDeletedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event: {DomainEvent}", notification.GetType().FullName);
            return Task.CompletedTask;
        }
    }
