﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Samples.EventHandlers;

    public class SampleDeletedEventHandler : INotificationHandler<SampleDeletedEvent>
    {
        private readonly ILogger<SampleDeletedEventHandler> _logger;

        public SampleDeletedEventHandler(
            ILogger<SampleDeletedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(SampleDeletedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event: {DomainEvent}", notification.GetType().FullName);
            return Task.CompletedTask;
        }
    }
