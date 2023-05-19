// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Folders.EventHandlers;

    public class FolderUpdatedEventHandler : INotificationHandler<FolderUpdatedEvent>
    {
        private readonly ILogger<FolderUpdatedEventHandler> _logger;

        public FolderUpdatedEventHandler(
            ILogger<FolderUpdatedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(FolderUpdatedEvent notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
