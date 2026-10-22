using System.Collections.Generic;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.Mod;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    /// ISO 音轨/字幕字段补写：把 MPLS STN_table 里的语言、声道数、采样率补到 ffprobe 探测结果上。
    ///
    /// 核心约束是「宁可标不出，也不能标错」——ffprobe 输出里没有 PID，
    /// 只能按顺序对齐，所以数量不等时必须整体放弃。这里把这些边界钉住。
    /// </summary>
    public class IsoStreamLanguageTests
    {
        private static UdfTitleResolver.MplsStreamInfo Stn(string language,
            UdfTitleResolver.BlurayStreamKind kind, int channels = 0, int sampleRate = 0)
        {
            return new UdfTitleResolver.MplsStreamInfo
            {
                Language = language,
                Kind = kind,
                Channels = channels,
                SampleRate = sampleRate
            };
        }

        private static MediaStream Probed(string language, MediaStreamType type = MediaStreamType.Audio,
            int channels = 0, int sampleRate = 0)
        {
            return new MediaStream
            {
                Type = type,
                Language = language,
                Channels = channels,
                SampleRate = sampleRate
            };
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

            var filled = IsoExtraction.FillMissingStreamDetails(probed, playlist, true);

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

            var filled = IsoExtraction.FillMissingStreamDetails(probed, playlist, true);

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
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("jpn", UdfTitleResolver.BlurayStreamKind.Audio)
            };

            Assert.Equal(1, IsoExtraction.FillMissingStreamDetails(probed, playlist, true));
            Assert.Equal("jpn", probed[0].Language);
        }

        [Fact]
        public void SkipsEntirely_WhenCountsDiffer()
        {
            var probed = new List<MediaStream> { Probed(null), Probed(null) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("eng", UdfTitleResolver.BlurayStreamKind.Audio)
            };

            var filled = IsoExtraction.FillMissingStreamDetails(probed, playlist, true);

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

            Assert.Equal(0, IsoExtraction.FillMissingStreamDetails(probed, playlist, true));
            Assert.Null(probed[0].Language);
            Assert.Null(probed[1].Language);
        }

        [Fact]
        public void HandlesSubtitleStreams()
        {
            var probed = new List<MediaStream> { Probed(null, MediaStreamType.Subtitle) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("chi", UdfTitleResolver.BlurayStreamKind.Subtitle)
            };

            Assert.Equal(1, IsoExtraction.FillMissingStreamDetails(probed, playlist, false));
            Assert.Equal("chi", probed[0].Language);
        }

        [Fact]
        public void ReturnsZero_WhenProbedListEmpty()
        {
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("eng", UdfTitleResolver.BlurayStreamKind.Audio)
            };

            Assert.Equal(0, IsoExtraction.FillMissingStreamDetails(new List<MediaStream>(), playlist, true));
        }

        [Fact]
        public void FillsChannelsAndSampleRate_WhenMissing()
        {
            var probed = new List<MediaStream> { Probed(null) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("eng", UdfTitleResolver.BlurayStreamKind.Audio, channels: 6, sampleRate: 48000)
            };

            var filled = IsoExtraction.FillMissingStreamDetails(probed, playlist, true);

            Assert.Equal(3, filled);
            Assert.Equal("eng", probed[0].Language);
            Assert.Equal(6, probed[0].Channels);
            Assert.Equal(48000, probed[0].SampleRate);
        }

        [Fact]
        public void DoesNotOverwriteExistingChannelsAndSampleRate()
        {
            var probed = new List<MediaStream> { Probed("eng", channels: 2, sampleRate: 44100) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("jpn", UdfTitleResolver.BlurayStreamKind.Audio, channels: 6, sampleRate: 48000)
            };

            Assert.Equal(0, IsoExtraction.FillMissingStreamDetails(probed, playlist, true));
            Assert.Equal(2, probed[0].Channels);
            Assert.Equal(44100, probed[0].SampleRate);
        }

        [Fact]
        public void SubtitlePath_DoesNotTouchAudioDetails()
        {
            var probed = new List<MediaStream> { Probed(null, MediaStreamType.Subtitle, channels: 0) };
            var playlist = new List<UdfTitleResolver.MplsStreamInfo>
            {
                Stn("chi", UdfTitleResolver.BlurayStreamKind.Subtitle, channels: 6, sampleRate: 48000)
            };

            var filled = IsoExtraction.FillMissingStreamDetails(probed, playlist, fillAudioDetails: false);

            Assert.Equal(1, filled);
            Assert.Equal("chi", probed[0].Language);
            Assert.Equal(0, probed[0].Channels);
            Assert.Equal(0, probed[0].SampleRate);
        }
    }
}
