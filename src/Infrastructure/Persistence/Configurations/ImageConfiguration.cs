// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Hosting;

namespace CleanArchitecture.Blazor.Infrastructure.Persistence.Configurations;

#nullable disable
public class ImageConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.OwnsOne(x => x.MetaData, b => {
            b.ToJson();
            //b.OwnsMany(d => d.EXIFData);
            b.OwnsOne(d => d.Lens);
            b.OwnsOne(d => d.Camera);
            });
        builder.OwnsOne(x => x.Hash, b => b.ToJson());
        builder.OwnsMany(x => x.Classification, b => b.ToJson());
        builder.OwnsMany(x => x.FaceDetections, b => b.ToJson());
        builder.OwnsMany(x => x.ImageObjects, b => b.ToJson());
        //builder.OwnsMany(x => x.ImageTags, b => b.ToJson());
        builder.OwnsMany(x => x.ThumbImages, b => b.ToJson());
        builder.Property(t => t.Name).HasMaxLength(250).IsRequired();

        builder.HasMany(e => e.ImageTags).WithMany(e => e.Images).UsingEntity(
            "ImageTag",
            l => l.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagsId").HasPrincipalKey(nameof(Tag.Keyword)),
            r => r.HasOne(typeof(Image)).WithMany().HasForeignKey("ImagesId").HasPrincipalKey(nameof(Image.Id)),
            j => j.HasKey("ImagesId", "TagsId")); ;
        builder.HasOne(x => x.Folder).WithMany(x => x.Images).HasForeignKey(x => x.FolderId).IsRequired();
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.DownloadImageUrl);
        builder.Ignore(e => e.RawImageUrl);
        builder.Ignore(e => e.FullPath);
        builder.Navigation(n => n.ImageTags).AutoInclude();
        builder.Navigation(n => n.MetaData).AutoInclude();
        builder.Navigation(n => n.Folder).AutoInclude();
    }
}


