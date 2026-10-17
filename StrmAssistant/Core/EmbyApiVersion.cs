using System;

namespace StrmAssistant.Core
{
    /// <summary>
    /// Emby API版本枚举，用于标识 4.9.1+ 的API行为。
    ///
    /// 数值编码规则（保证与 <see cref="Version"/> 同序）：
    ///     major * 1_000_000 + minor * 100_000 + build * 1_000 + revision
    /// 例：4.9.1.90 = 4_900_000 + 90 = 4_901_090；4.10.0.0 = 5_000_000。
    /// 这样 4.10.x 恒大于所有 4.9.x，避免“4.10 被当成比 4.9 旧”的排序反转。
    /// </summary>
    public enum EmbyApiVersion
    {
        /// <summary>
        /// 未知版本
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 4.8.0.x 兼容版本（保留给旧版适配代码）
        /// </summary>
        V4_8_0 = 4800000,

        /// <summary>
        /// 4.8.3.x 兼容版本（保留给旧版适配代码）
        /// </summary>
        V4_8_3 = 4830000,

        /// <summary>
        /// 4.9.0.x 兼容版本（保留给旧版适配代码）
        /// </summary>
        V4_9_0 = 4900000,

        /// <summary>
        /// 4.9.1.0-79 中期版本
        /// </summary>
        V4_9_1 = 4901000,

        /// <summary>
        /// 4.9.1.80+ 稳定版本
        /// </summary>
        V4_9_1_80 = 4901080,

        /// <summary>
        /// 4.9.1.90+ 稳定版本
        /// </summary>
        V4_9_1_90 = 4901090,

        /// <summary>
        /// 4.9.2.x 版本（晚于 4.9.1.90）
        /// </summary>
        V4_9_2 = 4902000,

        /// <summary>
        /// 4.10.x 版本（当前运行实例所属分档；数值上晚于全部 4.9.x）
        /// </summary>
        V4_10 = 5000000
    }
}
