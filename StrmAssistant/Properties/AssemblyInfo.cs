using System.Runtime.CompilerServices;

// 单元测试项目需要访问 internal 类型（UdfTitleResolver 的 MPLS 解析、LanguageUtility 等）。
// 仅影响可见性，不改变任何运行时行为。
[assembly: InternalsVisibleTo("StrmAssistant.Tests")]
