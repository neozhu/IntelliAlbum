using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities;

public class UserIdentify : BaseAuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ExampleImage>? ExampleImages { get; set; }
    public float Threshold { get; set; } = 0.7F;
    public string? Result { get; set; }
}

public class ExampleImage
{
    public required string Name { get; set; }
    public decimal Size { get; set; }
    public required string Url { get; set; }
}

