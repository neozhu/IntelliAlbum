// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Blazor.Infrastructure.Persistence.Configurations;

#nullable disable
public class ImageConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.OwnsOne(x => x.MetaData, b => {
            b.ToJson();
            b.OwnsMany(d => d.Directories, db => db.ToJson());
            b.OwnsOne(d => d.Lens, db => db.ToJson());
            b.OwnsOne(d => d.Camera, db => db.ToJson());
            });
        builder.OwnsOne(x => x.Hash, b => b.ToJson());
        builder.OwnsMany(x => x.Classification, b => b.ToJson());
        builder.OwnsMany(x => x.ImageObjects, b => {
            b.ToJson();
            b.OwnsOne(d => d.Tag);
            
            });
        builder.OwnsMany(x => x.ImageTags, b => b.ToJson());
        builder.Property(t => t.Name).HasMaxLength(250).IsRequired();

        builder.HasOne(x => x.Folder).WithMany(x => x.Images).HasForeignKey(x => x.FolderId).IsRequired();

        builder.Ignore(e => e.DomainEvents);
    }
}


