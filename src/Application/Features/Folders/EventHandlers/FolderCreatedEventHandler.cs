// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Folders.EventHandlers;

public class FolderCreatedEventHandler : INotificationHandler<FolderCreatedEvent>
{
        private readonly ILogger<FolderCreatedEventHandler> _logger;

        public FolderCreatedEventHandler(
            ILogger<FolderCreatedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(FolderCreatedEvent notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
}
