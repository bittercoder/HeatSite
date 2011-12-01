using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HeatSite
{
    public static class FileEnumerationExtensions
    {
        static readonly string[] _blackList = new[]
            {
                "obj\\",
                ".svn",
                "_svn",
                "resharper",
                "Cassini.dll"
            };

        static readonly string[] _extensionBlackList = new[]
            {
                ".cs",
                ".svn",
                ".suo",
                ".csproj",
                ".resharper",
                ".user"
            };

        public static IEnumerable<string> ExcludeBlackListFiles(this IEnumerable<string> inputs)
        {
            return
                from name in inputs
                where !_blackList.Any((string item) => name.IndexOf(item, StringComparison.InvariantCultureIgnoreCase) >= 0)
                where !_extensionBlackList.Any((string item) => name.EndsWith(item, StringComparison.InvariantCultureIgnoreCase))
                where !name.Contains("bin/") && !name.EndsWith(".xml")
                select name;
        }

        public static IEnumerable<string> AllFiles(this string path)
        {
            return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        }

        public static IEnumerable<string> AllDirectories(this IEnumerable<string> files)
        {
            return (
                       from file in files
                       select file.DirectoryName()).SelectMany((string directory) => directory.ExpandDirectory()).Distinct<string>();
        }

        public static IEnumerable<string> ExpandDirectory(this string path)
        {
            string parentDirectory = path.ParentDirectory();
            IEnumerable<string> result;
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                result = new[]
                    {
                        path
                    }.Concat(parentDirectory.ExpandDirectory());
            }
            else
            {
                result = new[]
                    {
                        path
                    };
            }
            return result;
        }

        public static IEnumerable<string> ToRelative(this IEnumerable<string> files, string path)
        {
            string arg_54_0 = path;
            char c = Path.DirectorySeparatorChar;
            bool arg_79_0;
            if (!arg_54_0.EndsWith(c.ToString()))
            {
                string arg_6E_0 = path;
                c = Path.AltDirectorySeparatorChar;
                arg_79_0 = !arg_6E_0.EndsWith(c.ToString());
            }
            else
            {
                arg_79_0 = false;
            }
            if (!arg_79_0)
            {
                path = path.Substring(0, path.Length - 1);
            }
            foreach (string current in files)
            {
                if (current.StartsWith(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (current.Length > path.Length + 1)
                    {
                        yield return current.Substring(path.Length + 1);
                    }
                    else
                    {
                        yield return current.Substring(path.Length);
                    }
                }
                else
                {
                    yield return current;
                }
            }
            yield break;
        }

        public static string DirectoryName(this string path)
        {
            return Path.GetDirectoryName(path);
        }

        public static string ToWixId(this string path)
        {
            var builder = new StringBuilder(path.Length);
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (char.IsLetter(c) || char.IsDigit(c) || c == '_')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append(".");
                }
            }
            return builder.ToString();
        }

        public static string ParentDirectory(this string path)
        {
            int lastIndex = path.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastIndex < 0)
            {
                lastIndex = path.LastIndexOf(Path.AltDirectorySeparatorChar);
            }
            string result;
            if (lastIndex > 0)
            {
                result = path.Substring(0, lastIndex);
            }
            else
            {
                result = null;
            }
            return result;
        }

        public static string NameOnly(this string path)
        {
            int lastIndex = path.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastIndex < 0)
            {
                lastIndex = path.LastIndexOf(Path.AltDirectorySeparatorChar);
            }
            string result;
            if (lastIndex >= 0)
            {
                result = path.Substring(lastIndex + 1);
            }
            else
            {
                result = path;
            }
            return result;
        }
    }
}