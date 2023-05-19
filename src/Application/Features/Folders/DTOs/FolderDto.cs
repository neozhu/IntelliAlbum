// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
namespace CleanArchitecture.Blazor.Application.Features.Folders.DTOs;

[Description("Folders")]
public class FolderDto:IMapFrom<Folder>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<Folder, FolderDto>().ReverseMap();
    }
    [Description("Id")]
    public int Id {get;set;} 
    [Description("Name")]
    public string Name {get;set;} = String.Empty; 
    [Description("Path")]
    public string? Path {get;set;} 
    [Description("Parent Id")]
    public int? ParentId {get;set;} 
    [Description("Folder Scan Date")]
    public DateTime? FolderScanDate {get;set;}
    [Description("Metadata")]
    public FolderMetadata? MetaData { get; set; }
    public override string ToString()
    {
        return $"{Path} [{Id}] {MetaData?.ToString()}";
    }

}

