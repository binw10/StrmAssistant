using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace StrmAssistant.Options.Store
{
    /// <summary>
    /// 「网络与直链」是新增的第 6 个 Tab，三块配置原先分散在两个旧文件里：
    /// <list type="bullet">
    /// <item><c>StrmAssistant.json</c> 的 <c>NetworkOptions</c> 与 <c>ModOptions.DirectLinkOptions</c></item>
    /// <item><c>StrmAssistant_MetadataEnhanceOptions.json</c> 的 <c>AltMovieDb</c></item>
    /// </list>
    /// 本类在首次加载时把这三块搬进新文件，避免用户重填代理地址与 TMDB Key。
    /// 任一源文件缺失时对应分组退回默认值，迁移失败不影响插件启动。
    /// </summary>
    internal static class NetworkProxyOptionsMigration
    {
        private const string FlagFileName = "StrmAssistant_NetworkProxyOptions_migrated_v1.flag";
        private const string MainOptionsFileName = "StrmAssistant.json";
        private const string MetadataOptionsFileName = "StrmAssistant_MetadataEnhanceOptions.json";

        public static void MigrateIfNeeded(string optionsFilePath, IJsonSerializer jsonSerializer, ILogger logger)
        {
            try
            {
                var directory = Path.GetDirectoryName(optionsFilePath);

                if (string.IsNullOrEmpty(directory)) return;

                var flagPath = Path.Combine(directory, FlagFileName);

                if (File.Exists(flagPath)) return;

                // 新文件已存在（用户已在新页面配置过）时不再覆盖，只落 flag
                if (File.Exists(optionsFilePath))
                {
                    File.WriteAllText(flagPath, DateTime.UtcNow.ToString("O"));
                    return;
                }

                NetworkOptions network = null;
                DirectLinkOptions directLink = null;
                AltMovieDbOptions tmdb = null;

                var mainPath = Path.Combine(directory, MainOptionsFileName);

                if (File.Exists(mainPath))
                {
                    var legacy = jsonSerializer.DeserializeFromString<LegacyMainOptions>(File.ReadAllText(mainPath));
                    network = legacy?.NetworkOptions;
                    directLink = legacy?.ModOptions?.DirectLinkOptions;
                }

                var metaPath = Path.Combine(directory, MetadataOptionsFileName);

                if (File.Exists(metaPath))
                {
                    var legacy = jsonSerializer.DeserializeFromString<LegacyMetadataOptions>(File.ReadAllText(metaPath));
                    tmdb = legacy?.AltMovieDb;
                }

                var migrated = new NetworkProxyOptions();

                if (network != null) migrated.Network = network;
                if (directLink != null) migrated.DirectLink = directLink;
                if (tmdb != null) migrated.TmdbReplacement = tmdb;

                byte[] bytes;

                using (var stream = new MemoryStream())
                {
                    jsonSerializer.SerializeToStream(migrated, stream, new JsonSerializerOptions { Indent = true });
                    bytes = stream.ToArray();
                }

                File.WriteAllBytes(optionsFilePath, bytes);
                File.WriteAllText(flagPath, DateTime.UtcNow.ToString("O"));

                logger.Info(
                    "NetworkProxyOptions: migrated network={0}, directLink={1}, tmdb={2} from legacy configs",
                    network != null, directLink != null, tmdb != null);
            }
            catch (Exception e)
            {
                logger.Warn("NetworkProxyOptions migration failed: " + e.Message);
            }
        }

        /// <summary>只取主配置里的网络与直链节点，其余字段反序列化时忽略。</summary>
        private class LegacyMainOptions
        {
            public NetworkOptions NetworkOptions { get; set; }
            public LegacyModOptions ModOptions { get; set; }
        }

        private class LegacyModOptions
        {
            public DirectLinkOptions DirectLinkOptions { get; set; }
        }

        /// <summary>只取元数据增强配置里的 TMDB 替换节点。</summary>
        private class LegacyMetadataOptions
        {
            public AltMovieDbOptions AltMovieDb { get; set; }
        }
    }
}
