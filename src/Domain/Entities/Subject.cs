using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities;

public class Subject : BaseAuditableEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? BeginDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? Photographer { get; set; }

    public virtual List<Image> Images { get; set; } = new();
}

