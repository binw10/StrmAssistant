using System.Collections.Generic;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.Mod;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    /// ISO 音轨/字幕语言补写：把 MPLS STN_table 里的语言补到 ffprobe 探测结果上。
    ///
    /// 核心约束是「宁可标不出，也不能标错」——ffprobe 输出里没有 PID，
    /// 只能按顺序对齐，所以数量不等时必须整体放弃。这里把这些边界钉住。
    /// </summary>
    public class IsoStreamLanguageTests
    {
        private static UdfTitleResolver.MplsStreamInfo Stn(string language, UdfTitleResolver.BlurayStreamKind kind)
        {
            return new UdfTitleResolver.MplsStreamInfo { Language = language, Kind = kind };
        }

        private static MediaStream Probed(string language, MediaStreamType type = MediaStreamType.Audio)
        {
            return new MediaStream { Type = type, Language = language };
        }

        [Fact]
        public void FillsMissingLanguages_WhenCountsMatch()
        {
            var probed = new List<MediaStream> { Probed(null), Probed(null) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("eng", UdfTitleResolver.BlurayStreamKind.Audio),
                Stn("jpn", UdfTitleResolver.BlurayStreamKind.Audio)
            };

            var filled = IsoExtraction.FillMissingLanguages(probed, playlist);

            Assert.Equal(2, filled);
            Assert.Equal("eng", probed[0].Language);
            Assert.Equal("jpn", probed[1].Language);
        }

        [Fact]
        public void DoesNotOverwriteExistingLanguage()
        {
            var probed = new List<MediaStream> { Probed("eng"), Probed(null) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("jpn", UdfTitleResolver.BlurayStreamKind.Audio),
                Stn("fra", UdfTitleResolver.BlurayStreamKind.Audio)
            };

            var filled = IsoExtraction.FillMissingLanguages(probed, playlist);

            Assert.Equal(1, filled);
            Assert.Equal("eng", probed[0].Language);
            Assert.Equal("fra", probed[1].Language);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("und")]
        [InlineData("UND")]
        public void TreatsBlankAndUndAsUnknown(string existing)
        {
            var probed = new List<MediaStream> { Probed(existing) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo> { Stn("jpn", UdfTitleResolver.BlurayStreamKind.Audio) };

            Assert.Equal(1, IsoExtraction.FillMissingLanguages(probed, playlist));
            Assert.Equal("jpn", probed[0].Language);
        }

        [Fact]
        public void SkipsEntirely_WhenCountsDiffer()
        {
            var probed = new List<MediaStream> { Probed(null), Probed(null) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo> { Stn("eng", UdfTitleResolver.BlurayStreamKind.Audio) };

            var filled = IsoExtraction.FillMissingLanguages(probed, playlist);

            Assert.Equal(0, filled);
            Assert.Null(probed[0].Language);
            Assert.Null(probed[1].Language);
        }

        [Fact]
        public void SkipsStream_WhenPlaylistLanguageUnknown()
        {
            var probed = new List<MediaStream> { Probed(null), Probed(null) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("und", UdfTitleResolver.BlurayStreamKind.Audio),
                Stn(null, UdfTitleResolver.BlurayStreamKind.Audio)
            };

            Assert.Equal(0, IsoExtraction.FillMissingLanguages(probed, playlist));
            Assert.Null(probed[0].Language);
            Assert.Null(probed[1].Language);
        }

        [Fact]
        public void HandlesSubtitleStreams()
        {
            var probed = new List<MediaStream> { Probed(null, MediaStreamType.Subtitle) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo> { Stn("chi", UdfTitleResolver.BlurayStreamKind.Subtitle) };

            Assert.Equal(1, IsoExtraction.FillMissingLanguages(probed, playlist));
            Assert.Equal("chi", probed[0].Language);
        }

        [Fact]
        public void ReturnsZero_WhenProbedListEmpty()
        {
            var playlist = new List<UdfTitleResolver.MplsStreamInfo> { Stn("eng", UdfTitleResolver.BlurayStreamKind.Audio) };

            Assert.Equal(0, IsoExtraction.FillMissingLanguages(new List<MediaStream>(), playlist));
        }
    }
}
