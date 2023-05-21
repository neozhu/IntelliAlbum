using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public interface IHashProvider
{
    string GetPerceptualHash(string path);
}
