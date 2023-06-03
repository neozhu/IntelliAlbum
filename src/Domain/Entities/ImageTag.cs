using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities
{
    public class Tag
    {
        public string Keyword { get; set; } = null!;

        public override string ToString()
        {
            return $"{Keyword}";
        }
    }
}
