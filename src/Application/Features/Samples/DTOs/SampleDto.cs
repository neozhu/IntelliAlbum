// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
namespace CleanArchitecture.Blazor.Application.Features.Samples.DTOs;

[Description("Samples")]
public class SampleDto : IMapFrom<Sample>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<Sample, SampleDto>().ReverseMap();
    }
    [Description("Id")]
    public int Id { get; set; }
    [Description("Name")]
    public string Name { get; set; } = String.Empty;
    [Description("Description")]
    public string? Description { get; set; }
    [Description("Sample Images")]
    public List<SampleImage>? SampleImages { get; set; }
    [Description("Threshold")]
    public float Threshold { get; set; }
    [Description("Result")]
    public string? Result { get; set; }

}

