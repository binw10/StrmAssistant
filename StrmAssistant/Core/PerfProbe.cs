using System;
using System.Diagnostics;

namespace StrmAssistant.Core
{
    /// <summary>
    /// 轻量耗时探针。
    /// <para>
    /// 用途：定位**同步阻塞点**（<c>.Result</c> / 挂载 / 网络调用）的真实耗时。
    /// 设计取舍：
    /// </para>
    /// <list type="bullet">
    /// <item>指标**常驻**记录到 <see cref="PerformanceMonitor"/>（开销仅一个 Stopwatch，可忽略），
    /// 由 <see cref="HealthCheck"/> / <see cref="OptimizationAdvisor"/> 的「慢操作」统计自动消费。</item>
    /// <item>**逐次日志**仅在超过阈值且 <c>DebugMode</c> 开启时输出，避免生产环境刷屏。</item>
    /// </list>
    /// </summary>
    internal static class PerfProbe
    {
        /// <summary>
        /// 开始测量。用 <c>using</c> 包住被测代码块即可。
        /// </summary>
        /// <param name="name">指标名，建议 <c>类名.方法名</c>。</param>
        /// <param name="slowThresholdMs">超过该毫秒数时（且 DebugMode 开启）打一行日志；0 表示不打日志。</param>
        public static IDisposable Measure(string name, long slowThresholdMs = 0) =>
            new Scope(name, slowThresholdMs);

        private sealed class Scope : IDisposable
        {
            private readonly string _name;
            private readonly long _slowThresholdMs;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private bool _disposed;

            public Scope(string name, long slowThresholdMs)
            {
                _name = name;
                _slowThresholdMs = slowThresholdMs;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _stopwatch.Stop();
                var elapsedMs = _stopwatch.ElapsedMilliseconds;

                try
                {
                    // 常驻指标（供 HealthCheck / OptimizationAdvisor 的慢操作统计）
                    PerformanceMonitor.Instance?.RecordOperation(_name, elapsedMs);

                    // 逐次日志：仅 DebugMode + 超阈值
                    if (_slowThresholdMs > 0 && elapsedMs >= _slowThresholdMs &&
                        Plugin.Instance?.DebugMode == true)
                    {
                        Plugin.Instance.Logger.Debug($"[Perf] {_name} took {elapsedMs} ms");
                    }
                }
                catch
                {
                    // 探针本身绝不影响业务
                }
            }
        }
    }
}
