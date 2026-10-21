using System;
using System.IO;
using System.Reflection;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class MediaInfoBackupPathsTests
    {
        private static string BackupPath(string folder, string root)
        {
            var type = typeof(UdfTitleResolver).Assembly.GetType("StrmAssistant.Common.MediaInfoBackupPaths", true);
            return (string)type.GetMethod("GetPath", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { folder, "movie-mediainfo.json", root });
        }

        [Fact]
        public void DifferentDrives_NeverShareCentralBackupPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "strm-backup-tests");
            Assert.NotEqual(BackupPath(@"C:\Movies\Title", root), BackupPath(@"D:\Movies\Title", root));
            Assert.Equal(BackupPath(@"c:\Movies\Title", root), BackupPath(@"C:/Movies/Title", root));
        }

        [Fact]
        public void DifferentUncServersAndShares_NeverShareCentralBackupPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "strm-backup-tests");
            var first = BackupPath(@"\\server1\media\Movies", root);
            Assert.NotEqual(first, BackupPath(@"\\server2\media\Movies", root));
            Assert.NotEqual(first, BackupPath(@"\\server1\other\Movies", root));
            Assert.Equal(first, BackupPath(@"\\SERVER1\MEDIA\Movies", root));
        }

        [Fact]
        public void AdjacentBackup_PreservesLegacyPath()
        {
            Assert.Equal(Path.Combine(@"C:\Movies", "movie-mediainfo.json"), BackupPath(@"C:\Movies", ""));
        }

        [Fact]
        public void NativeRootedPath_PreservesLegacyCentralLayout()
        {
            var root = Path.Combine(Path.GetTempPath(), "strm-backup-tests");
            // Windows central layout intentionally adds drive identity. Unix layout remains compatible.
            if (Path.DirectorySeparatorChar == '\\') return;
            Assert.Equal(Path.Combine(root, "media", "Movies", "movie-mediainfo.json"), BackupPath("/media/Movies", root));
        }
    }
}
