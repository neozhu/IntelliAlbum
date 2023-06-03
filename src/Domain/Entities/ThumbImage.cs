using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities;

    public class ThumbImage
    {
        public required string Name { get; set; }
        public ObjectTypes Types { get; set; } = ObjectTypes.Object;
        public string? ThumbSize { get; set; }
        public required string Url { get; set; }

    public override string ToString()
    {
        return $"{Name}"; 
    }
}

