﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Common.Utils;
public static class PathUtils
{
    /// <summary>
    ///     Little wrapper for managing relative paths without trailing slashes.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="root"></param>
    /// <returns></returns>
    public static string MakePathRelativeTo(this string path, string root)
    {
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;

        var result = Path.GetRelativePath(root, path);
        return result;
    }
}
