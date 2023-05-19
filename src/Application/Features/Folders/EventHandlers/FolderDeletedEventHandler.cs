// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Folders.EventHandlers;

    public class FolderDeletedEventHandler : INotificationHandler<FolderDeletedEvent>
    {
        private readonly ILogger<FolderDeletedEventHandler> _logger;

        public FolderDeletedEventHandler(
            ILogger<FolderDeletedEventHandler> logger
            )
        {
            _logger = logger;
        }
        public Task Handle(FolderDeletedEvent notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
