// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Domain.Events;

    public class FolderDeletedEvent : DomainEvent
    {
        public FolderDeletedEvent(Folder item)
        {
            Item = item;
        }

        public Folder Item { get; }
    }

