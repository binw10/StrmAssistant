using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 视频截图后处理：对截图生成的图片做居中裁剪（海报竖图 2:3 / 集图 16:9）。
    /// 仅处理 Emby 内部元数据目录中的服务器生成图片，不碰用户本地图片。
    /// </summary>
    public class ImagePostProcessor
    {
        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

        private readonly IFfmpegManager _ffmpegManager;
        private readonly ILogger _logger;

        public ImagePostProcessor(IFfmpegManager ffmpegManager)
        {
            _logger = Plugin.Instance.Logger;
            _ffmpegManager = ffmpegManager;
        }

        public void CropToPortrait(string imagePath)
        {
            CropImage(imagePath, 2.0 / 3.0, false);
        }

        public void CropTo16x9(string imagePath)
        {
            CropImage(imagePath, 16.0 / 9.0, true);
        }

        /// <summary>
        /// 按条目处理截图后处理：非集条目的主图按海报竖图 2:3 裁剪，集的主图/缩略图按 16:9 裁剪。
        /// 仅处理 Emby 内部元数据目录中服务器生成的图片，用户本地图片不受影响。
        /// </summary>
        public void ProcessItem(BaseItem item, bool forcePortraitPoster, bool ensureEpisode16x9)
        {
            if (item == null || (!forcePortraitPoster && !ensureEpisode16x9)) return;

            var internalMetadataPath =
                (Plugin.Instance.ApplicationPaths as IServerApplicationPaths)?.InternalMetadataPath;

            if (string.IsNullOrEmpty(internalMetadataPath)) return;

            var isEpisode = item is MediaBrowser.Controller.Entities.TV.Episode;

            foreach (var imageInfo in item.ImageInfos.ToList())
            {
                try
                {
                    var path = imageInfo.Path;

                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!path.StartsWith(internalMetadataPath, StringComparison.OrdinalIgnoreCase)) continue;

                    var newSize = (default(int?), default(int?));

                    if (imageInfo.Type == ImageType.Primary)
                    {
                        if (isEpisode)
                        {
                            if (ensureEpisode16x9) newSize = CropImage(path, 16.0 / 9.0, true);
                        }
                        else if (forcePortraitPoster)
                        {
                            newSize = CropImage(path, 2.0 / 3.0, false);
                        }
                    }
                    else if (imageInfo.Type == ImageType.Thumb && isEpisode && ensureEpisode16x9)
                    {
                        newSize = CropImage(path, 16.0 / 9.0, true);
                    }

                    if (newSize.Item1.HasValue)
                    {
                        imageInfo.Width = newSize.Item1.Value;
                        imageInfo.Height = newSize.Item2.Value;
                        item.UpdateToRepository(ItemUpdateType.ImageUpdate);
                    }
                }
                catch (Exception e)
                {
                    _logger.Warn("ImagePostProcess - ProcessItem failed for {0}: {1}", item.Name, e.Message);
                }
            }
        }

        /// <summary>
        /// 按目标宽高比居中裁剪。landscape=true 表示目标为横图（如 16/9），否则为竖图（如 2/3）。
        /// 已满足比例的图片跳过，不重复压缩。返回裁剪后的实际尺寸，未裁剪返回 null。
        /// </summary>
        private (int?, int?) CropImage(string imagePath, double aspect, bool landscape)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return (null, null);

                var (width, height) = GetImageDimensions(imagePath);
                if (width is null || height is null || width <= 0 || height <= 0) return (null, null);

                int targetW, targetH;

                if (landscape)
                {
                    targetW = Math.Min(width.Value, Convert.ToInt32(height.Value * aspect));
                    targetH = Convert.ToInt32(targetW / aspect);
                }
                else
                {
                    targetH = height.Value;
                    targetW = Math.Min(width.Value, Convert.ToInt32(height.Value * aspect));
                }

                if (Math.Abs(width.Value - targetW) < 2 && Math.Abs(height.Value - targetH) < 2)
                    return (null, null);

                var x = (width.Value - targetW) / 2;
                var y = (height.Value - targetH) / 2;

                var encoderPath = _ffmpegManager.FfmpegConfiguration?.EncoderPath;
                if (string.IsNullOrEmpty(encoderPath) || !File.Exists(encoderPath)) return (null, null);

                var tempPath = imagePath + ".crop.tmp";

                var exitCode = RunProcess(encoderPath,
                    $"-y -i \"{imagePath}\" -vf crop={targetW}:{targetH}:{x}:{y} -q:v 2 \"{tempPath}\"");

                if (exitCode == 0 && File.Exists(tempPath))
                {
                    File.Delete(imagePath);
                    File.Move(tempPath, imagePath);

                    _logger.Info("ImagePostProcess - Cropped {0} from {1}x{2} to {3}x{4}",
                        Path.GetFileName(imagePath), width, height, targetW, targetH);

                    return (targetW, targetH);
                }

                _logger.Warn("ImagePostProcess - ffmpeg crop failed ({0}) for {1}", exitCode, imagePath);
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (Exception e)
            {
                _logger.Warn("ImagePostProcess - Failed for {0}: {1}", imagePath, e.Message);
                _logger.Debug(e.StackTrace);
            }

            return (null, null);
        }

        /// <summary>
        /// 读取图片实际尺寸（JPEG SOF/PNG IHDR 头解析，服务器截图均为这两种格式），失败返回 null。
        /// </summary>
        private static (int?, int?) GetImageDimensions(string imagePath)
        {
            try
            {
                using var fs = File.OpenRead(imagePath);
                var header = new byte[65536];
                var read = fs.Read(header, 0, header.Length);
                if (read < 24) return (null, null);

                if (header[0] == 0x89 && header[1] == (byte)'P' && header[2] == (byte)'N' && header[3] == (byte)'G')
                {
                    var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    var height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    return (width, height);
                }

                if (header[0] == 0xFF && header[1] == 0xD8)
                {
                    var i = 2;

                    while (i < read - 9)
                    {
                        if (header[i] != 0xFF) { i++; continue; }

                        var marker = header[i + 1];

                        if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3)
                        {
                            var height = (header[i + 5] << 8) | header[i + 6];
                            var width = (header[i + 7] << 8) | header[i + 8];
                            return (width, height);
                        }

                        if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD9))
                        {
                            i += 2;
                            continue;
                        }

                        var length = (header[i + 2] << 8) | header[i + 3];
                        if (length < 2) return (null, null);
                        i += 2 + length;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return (null, null);
        }

        private int RunProcess(string fileName, string arguments)
        {
            return RunProcess(fileName, arguments, out _);
        }

        private int RunProcess(string fileName, string arguments, out string output)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                output = null;
                return -1;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
            {
                try { process.Kill(); } catch { /* ignored */ }
                output = null;
                return -1;
            }

            output = outputTask.GetAwaiter().GetResult();
            return process.ExitCode;
        }
    }
}
