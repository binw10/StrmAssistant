using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 自定义 ffmpeg/ffprobe 路径覆盖：把 Emby 全局 FfmpegConfiguration 的
    /// ProbePath / EncoderPath 底层字段替换为用户指定二进制（静态编译版、
    /// rffmpeg 包装器等），用于解决 DV P5 截图发绿、ISO/bluray 协议支持等问题。
    /// 参考自检日志范式：运行 -version 并输出关键能力行。
    /// </summary>
    public static class FfmpegPathOverride
    {
        private static IFfmpegManager _ffmpegManager;
        private static ILogger _logger;
        private static bool _initialized;

        public static void Initialize(IFfmpegManager ffmpegManager, ILogger logger)
        {
            _ffmpegManager = ffmpegManager;
            _logger = logger;
            _initialized = true;
        }

        /// <summary>
        /// 按当前配置应用路径覆盖。空值或文件不存在时跳过对应项，不回滚已生效的覆盖。
        /// </summary>
        public static void Apply(string customProbePath, string customEncoderPath)
        {
            if (!_initialized || _ffmpegManager?.FfmpegConfiguration == null) return;

            TryOverride(_ffmpegManager.FfmpegConfiguration, "ProbePath", customProbePath);
            TryOverride(_ffmpegManager.FfmpegConfiguration, "EncoderPath", customEncoderPath);
        }

        private static void TryOverride(IFfmpegConfiguration configuration, string propertyName, string customPath)
        {
            if (string.IsNullOrWhiteSpace(customPath)) return;

            var fullPath = Path.GetFullPath(customPath);
            if (!File.Exists(fullPath))
            {
                _logger.Warn("FfmpegPathOverride - {0} not found: {1}", propertyName, fullPath);
                return;
            }

            if (SetBackingField(configuration, propertyName, fullPath))
            {
                _logger.Info("FfmpegPathOverride - {0} set to {1}", propertyName, fullPath);
                SelfCheck(fullPath, propertyName);
            }
            else
            {
                _logger.Warn("FfmpegPathOverride - failed to set {0} backing field on {1}",
                    propertyName, configuration.GetType().FullName);
            }
        }

        private static bool SetBackingField(IFfmpegConfiguration configuration, string propertyName, string value)
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                // 首选自动属性后备字段：<ProbePath>k__BackingField
                var field = configuration.GetType().GetField("<" + propertyName + ">k__BackingField", flags) ??
                            configuration.GetType()
                                .GetFields(flags)
                                .FirstOrDefault(f => f.FieldType == typeof(string) &&
                                                     f.Name.IndexOf(propertyName, StringComparison.OrdinalIgnoreCase) >= 0);

                if (field == null || field.IsInitOnly)
                {
                    // readonly 字段无法直接写，走反射去除 initonly 标记的最后手段
                    if (field == null) return false;
                }

                field.SetValue(configuration, value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug("FfmpegPathOverride - SetBackingField {0} failed: {1}", propertyName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 自检：执行 -version 并把协议/能力相关输出写入日志，便于诊断 bluray/smb/chromaprint 支持。
        /// </summary>
        private static void SelfCheck(string binaryPath, string label)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);

                    var firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(l => l.StartsWith(binaryPath) || l.Contains("version"));
                    _logger.Info("FfmpegPathOverride - {0} self-check exit {1}: {2}", label, process.ExitCode,
                        firstLine?.Trim());

                    var features = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(l => l.Contains("--enable"))
                        .ToList();
                    if (features.Count > 0)
                    {
                        var keywords = new[] { "bluray", "libsmbclient", "chromaprint", "libplacebo", "vulkan" };
                        var hits = keywords.Where(k => features.Any(l => l.Contains(k))).ToList();
                        if (hits.Count > 0)
                        {
                            _logger.Info("FfmpegPathOverride - {0} supports: {1}", label, string.Join(", ", hits));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("FfmpegPathOverride - {0} self-check failed: {1}", label, ex.Message);
            }
        }
    }
}
