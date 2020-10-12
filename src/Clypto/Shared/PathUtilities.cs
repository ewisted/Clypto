using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Shared
{
    public static class PathUtilities
    {
        public static string GetFullPath(string fileName)
        {
            var currentPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (File.Exists(currentPath))
                return currentPath;

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }
    }
}
