// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Blazor.Infrastructure.Persistence.Configurations;

#nullable disable
public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasMany(x => x.Children).WithOne(x => x.Parent).HasForeignKey(x => x.ParentId).IsRequired(false);
        //builder.HasOne(x=>x.Parent).WithMany(x=>x.Children).HasForeignKey(x=>x.ParentId).IsRequired(false);
        builder.Property(t => t.Name).HasMaxLength(250).IsRequired();
        builder.OwnsOne(x => x.MetaData, b => b.ToJson());
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.HasSubFolders);
        builder.Ignore(e => e.Subfolders);
        builder.Ignore(e => e.ParentFolders);
    }
}


