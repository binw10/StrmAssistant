using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    /// LanguageUtility 纯函数测试：语言判定 / 繁简转换 / 搜索变体归一化。
    /// 这些是中文搜索与元数据中文化的判定基础，边界多、改动频繁。
    /// </summary>
    public class LanguageUtilityTests
    {
        [Theory]
        [InlineData("杜比视界测试", true)]
        [InlineData("美国狙击手", true)]
        [InlineData("American Sniper", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("進撃の巨人", false)]   // 含假名 → 不是中文
        [InlineData("杜比·视界", true)]     // 中点不算假名
        public void IsChinese_ClassifiesByScript(string input, bool expected)
        {
            Assert.Equal(expected, LanguageUtility.IsChinese(input));
        }

        [Theory]
        [InlineData("測試", "测试")]
        [InlineData("雙層杜比視界", "双层杜比视界")]
        [InlineData("简体不变", "简体不变")]
        public void ConvertTraditionalToSimplified_Converts(string input, string expected)
        {
            Assert.Equal(expected, LanguageUtility.ConvertTraditionalToSimplified(input));
        }

        [Theory]
        [InlineData("ＡＢＣ", "ABC")]          // 全角 ASCII → 半角
        [InlineData("杜比·视界", "杜比视界")]   // 中点丢弃
        [InlineData("FEL—2", "FEL-2")]         // 破折号 → 连字符
        [InlineData("測試", "测试")]           // 繁体 → 简体
        [InlineData("  杜比  ", "杜比")]       // 首尾空白裁剪
        public void NormalizeChineseVariants_FoldsVariants(string input, string expected)
        {
            Assert.Equal(expected, LanguageUtility.NormalizeChineseVariants(input));
        }

        [Theory]
        [InlineData("美国狙击手", "zh")]
        [InlineData("American Sniper", "en")]
        [InlineData("進撃の巨人", "ja")]
        public void GetLanguageByTitle_MapsToIso(string input, string expected)
        {
            Assert.Equal(expected, LanguageUtility.GetLanguageByTitle(input));
        }

        [Fact]
        public void ConvertToPinyinInitials_ProducesInitials()
        {
            // TinyPinyin 的大小写约定不作为契约，只校验首字母序列
            Assert.Equal("dbsj", LanguageUtility.ConvertToPinyinInitials("杜比视界").ToLowerInvariant());
        }

        [Theory]
        [InlineData("Season 1", true)]
        [InlineData("第 1 季", true)]
        [InlineData("シーズン1", true)]
        [InlineData("第一季", false)]
        public void IsDefaultSeasonName_DetectsPlaceholderNames(string input, bool expected)
        {
            Assert.Equal(expected, LanguageUtility.IsDefaultSeasonName(input));
        }
    }
}
