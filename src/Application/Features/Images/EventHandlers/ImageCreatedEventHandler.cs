﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Images.EventHandlers;

public class ImageCreatedEventHandler : INotificationHandler<ImageCreatedEvent>
{
        private readonly ILogger<ImageCreatedEventHandler> _logger;

        public ImageCreatedEventHandler(
            ILogger<ImageCreatedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(ImageCreatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event: {DomainEvent}", notification.GetType().FullName);
            return Task.CompletedTask;
        }
}
