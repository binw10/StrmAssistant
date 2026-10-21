using System;
using System.IO;
using System.Linq;

namespace StrmAssistant.Common
{
    internal static class MediaInfoBackupPaths
    {
        internal static bool HasWindowsRoot(string folder)
        {
            var normalized = folder?.Replace('\\', '/');
            return normalized != null &&
                ((normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '/') ||
                 normalized.StartsWith("//", StringComparison.Ordinal));
        }

        internal static string GetPath(string folder, string fileName, string jsonRoot)
        {
            if (string.IsNullOrEmpty(jsonRoot)) return Path.Combine(folder, fileName);
            var normalized = folder.Replace('\\', '/');
            if (normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '/')
            {
                var drive = "windows-drive-" + char.ToUpperInvariant(normalized[0]);
                return Path.Combine(jsonRoot, ".strmassistant-v2", drive,
                    normalized.Substring(3).Replace('/', Path.DirectorySeparatorChar), fileName);
            }
            if (normalized.StartsWith("//", StringComparison.Ordinal))
            {
                var parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) throw new ArgumentException("UNC path requires a server and share", nameof(folder));
                var tail = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(2));
                return Path.Combine(jsonRoot, ".strmassistant-v2", "windows-unc", parts[0].ToLowerInvariant(),
                    parts[1].ToLowerInvariant(), tail, fileName);
            }
            var relative = Path.IsPathRooted(folder)
                ? Path.GetRelativePath(Path.GetPathRoot(folder), folder)
                : folder;
            return Path.Combine(jsonRoot, relative, fileName);
        }
    }
}
