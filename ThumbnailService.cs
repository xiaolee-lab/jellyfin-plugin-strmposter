using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    public class ThumbnailService
    {
        private static readonly TimeSpan FFmpegTimeout = TimeSpan.FromSeconds(60);

        private readonly MediaEncoderAdapter _mediaEncoderAdapter;
        private readonly ILogger<ThumbnailService> _logger;

        public ThumbnailService(MediaEncoderAdapter mediaEncoderAdapter, ILogger<ThumbnailService> logger)
        {
            _mediaEncoderAdapter = mediaEncoderAdapter;
            _logger = logger;
        }

        public bool NeedsExtraction(BaseItem item, bool forceReExtract = false)
        {
            if (!IsStrmItem(item))
            {
                return false;
            }

            if (forceReExtract)
            {
                _logger.LogDebug("强制重提模式已启用：{ItemName}", item.Name);
                return true;
            }

            var hasPrimaryImage = !string.IsNullOrEmpty(item.PrimaryImagePath) ||
                                 item.ImageInfos.Any(i => i.Type == ImageType.Primary);
            if (hasPrimaryImage)
            {
                return false;
            }

            var thumbnailPath = GetThumbnailPath(item);
            if (File.Exists(thumbnailPath))
            {
                _logger.LogDebug("封面文件已存在：{ItemName} - {Path}", item.Name, thumbnailPath);
                return false;
            }

            return true;
        }

        public string GetThumbnailPath(BaseItem item)
        {
            var mediaPath = item.Path;
            if (string.IsNullOrEmpty(mediaPath))
            {
                return string.Empty;
            }

            var directory = Path.GetDirectoryName(mediaPath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(mediaPath);

            return Path.Combine(directory ?? string.Empty, fileNameWithoutExt + "-poster.jpg");
        }

        public async Task<bool> ExtractAndSaveThumbnailAsync(BaseItem item, CancellationToken cancellationToken = default)
        {
            if (!IsStrmItem(item))
            {
                return false;
            }

            var config = JellyfinPlugin.Instance?.Configuration;
            if (config != null && !config.EnableThumbnailExtraction)
            {
                _logger.LogDebug("封面提取功能已关闭，跳过：{ItemName}", item.Name);
                return false;
            }

            try
            {
                _logger.LogInformation("正在提取封面：{ItemName}", item.Name);

                var mediaInfo = await _mediaEncoderAdapter.ProbeMediaAsync(item, cancellationToken).ConfigureAwait(false);
                if (mediaInfo?.Duration == null || mediaInfo.Duration.Value.TotalSeconds <= 0)
                {
                    _logger.LogWarning("无法获取媒体信息：{ItemName}", item.Name);
                    return false;
                }

                var extractPercent = config?.ThumbnailExtractPositionPercent ?? 50;
                extractPercent = Math.Max(5, Math.Min(95, extractPercent));

                var positionSeconds = mediaInfo.Duration.Value.TotalSeconds * extractPercent / 100;

                var ffmpegPath = _mediaEncoderAdapter.GetFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    _logger.LogWarning("未找到 FFmpeg 路径");
                    return false;
                }

                var outputPath = GetThumbnailPath(item);
                if (string.IsNullOrEmpty(outputPath))
                {
                    _logger.LogWarning("无法生成封面保存路径：{ItemName}", item.Name);
                    return false;
                }

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 先写入临时文件再原子替换，避免并发读取到不完整的封面
                var tempPath = GetTempPath(outputPath);

                try
                {
                    var success = await ExtractThumbnailWithFFmpegAsync(
                        mediaInfo.Path,
                        tempPath,
                        positionSeconds,
                        ffmpegPath,
                        cancellationToken).ConfigureAwait(false);

                    if (success)
                    {
                        File.Move(tempPath, outputPath, true);
                        _logger.LogInformation("封面提取成功：{ItemName} -> {Path}", item.Name, outputPath);
                        return true;
                    }

                    _logger.LogWarning("封面提取失败：{ItemName}", item.Name);
                    return false;
                }
                finally
                {
                    TryDelete(tempPath);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提取封面时发生错误：{ItemName}", item.Name);
                return false;
            }
        }

        private async Task<bool> ExtractThumbnailWithFFmpegAsync(
            string inputPath,
            string outputPath,
            double positionSeconds,
            string ffmpegPath,
            CancellationToken cancellationToken)
        {
            try
            {
                // 关键帧提取：跳过非关键帧，快速定位到目标时间点后的第一帧
                var arguments = new[]
                {
                    "-nostdin",
                    "-hide_banner",
                    "-loglevel", "error",
                    "-skip_frame", "nokey",
                    "-ss", positionSeconds.ToString("F2", CultureInfo.InvariantCulture),
                    "-i", inputPath,
                    "-frames:v", "1",
                    "-q:v", "2",
                    "-y",
                    outputPath
                };

                var result = await ProcessRunner.RunAsync(ffmpegPath, arguments, FFmpegTimeout, cancellationToken)
                    .ConfigureAwait(false);

                if (result.TimedOut)
                {
                    _logger.LogWarning("FFmpeg 执行超时");
                    return false;
                }

                if (result.ExitCode != 0)
                {
                    _logger.LogWarning("FFmpeg 执行失败，错误码：{ExitCode}", result.ExitCode);
                    _logger.LogDebug("FFmpeg 输出：{Output}", result.StandardError);
                    return false;
                }

                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFmpeg 执行异常");
                return false;
            }
        }

        public async Task<int> BatchExtractThumbnailsAsync(
            IEnumerable<BaseItem> items,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            var itemList = items.ToList();
            var totalCount = itemList.Count;

            if (totalCount == 0)
            {
                _logger.LogInformation("没有需要处理的项目");
                return 0;
            }

            var config = JellyfinPlugin.Instance?.Configuration;
            var concurrency = config?.ThumbnailConcurrency ?? 2;
            concurrency = Math.Max(1, Math.Min(10, concurrency));

            _logger.LogInformation(
                "开始批量提取封面，共 {Count} 个项目，并发数：{Concurrency}",
                totalCount, concurrency);

            var successCount = 0;
            var processedCount = 0;
            var lockObject = new object();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(itemList, parallelOptions, async (item, ct) =>
            {
                try
                {
                    var success = await ExtractAndSaveThumbnailAsync(item, ct).ConfigureAwait(false);
                    lock (lockObject)
                    {
                        if (success)
                        {
                            successCount++;
                        }

                        processedCount++;
                        progress?.Report((double)processedCount / totalCount * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "提取封面时发生错误：{ItemName}", item.Name);
                    lock (lockObject)
                    {
                        processedCount++;
                        progress?.Report((double)processedCount / totalCount * 100);
                    }
                }
            }).ConfigureAwait(false);

            _logger.LogInformation(
                "批量提取完成：成功 {Success}/{Total} 个，并发数：{Concurrency}",
                successCount, totalCount, concurrency);
            return successCount;
        }

        private static bool IsStrmItem(BaseItem item)
        {
            return item.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static string GetTempPath(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            var tempName = Path.GetFileNameWithoutExtension(outputPath) + ".tmp" + Path.GetExtension(outputPath);
            return string.IsNullOrEmpty(directory) ? tempName : Path.Combine(directory, tempName);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 清理临时文件失败不影响主流程
            }
        }
    }
}
