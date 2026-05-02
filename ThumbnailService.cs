using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    public class ThumbnailService
    {
        private readonly MediaEncoderAdapter _mediaEncoderAdapter;
        private readonly ILogger<ThumbnailService> _logger;

        public ThumbnailService(MediaEncoderAdapter mediaEncoderAdapter, ILogger<ThumbnailService> logger)
        {
            _mediaEncoderAdapter = mediaEncoderAdapter;
            _logger = logger;
        }

        public bool NeedsExtraction(BaseItem item, bool forceReExtract = false)
        {
            if (forceReExtract)
            {
                _logger.LogDebug("强制重提模式已启用：{ItemName}", item.Name);
                return true;
            }

            var hasPrimaryImage = !string.IsNullOrEmpty(item.PrimaryImagePath) ||
                                 item.ImageInfos.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary);
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

            return Path.Combine(directory, fileNameWithoutExt + "-poster.jpg");
        }

        public async Task<bool> ExtractAndSaveThumbnailAsync(BaseItem item, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("正在提取封面：{ItemName}", item.Name);

                var mediaInfo = await _mediaEncoderAdapter.ExtractMediaInfoAsync(item, cancellationToken);
                if (mediaInfo == null || mediaInfo.MediaStreams == null)
                {
                    _logger.LogWarning("无法获取媒体信息：{ItemName}", item.Name);
                    return false;
                }

                var duration = mediaInfo.RunTimeTicks ?? 0;
                if (duration <= 0)
                {
                    _logger.LogWarning("无法获取视频时长：{ItemName}", item.Name);
                    return false;
                }

                var config = JellyfinPlugin.Instance?.Configuration;
                var extractPercent = config?.ThumbnailExtractPositionPercent ?? 50;
                extractPercent = Math.Max(5, Math.Min(95, extractPercent));

                var positionSeconds = (double)duration / TimeSpan.TicksPerSecond * extractPercent / 100;
                
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

                var success = await ExtractThumbnailWithFFmpegAsync(mediaInfo.Path, outputPath, positionSeconds, ffmpegPath, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("封面提取成功：{ItemName} -> {Path}", item.Name, outputPath);
                }
                else
                {
                    _logger.LogWarning("封面提取失败：{ItemName}", item.Name);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提取封面时发生错误：{ItemName}", item.Name);
                return false;
            }
        }

        private async Task<bool> ExtractThumbnailWithFFmpegAsync(string inputPath, string outputPath, double positionSeconds, string ffmpegPath, CancellationToken cancellationToken)
        {
            try
            {
                // 关键帧提取 - 使用 -skip_frame nokey 更快找到设置时间后寻找第一个关键帧，速度更快
                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-skip_frame nokey -ss {positionSeconds:F2} -i \"{inputPath}\" -vframes 1 -q:v 2 -y \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken));

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    var fileInfo = new FileInfo(outputPath);
                    if (fileInfo.Length > 0)
                    {
                        return true;
                    }
                }

                _logger.LogWarning("FFmpeg 执行失败，错误码：{ExitCode}，文件：{Path}", process.ExitCode, inputPath);
                _logger.LogDebug("FFmpeg 输出：{Output}", await errorTask);

                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFmpeg 执行异常：{Path}", inputPath);
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
                    var success = await ExtractAndSaveThumbnailAsync(item, ct);
                    lock (lockObject)
                    {
                        if (success) successCount++;
                        processedCount++;
                        var progressPercent = (double)processedCount / totalCount * 100;
                        progress?.Report(progressPercent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "提取封面时发生错误：{ItemName}", item.Name);
                    lock (lockObject)
                    {
                        processedCount++;
                        var progressPercent = (double)processedCount / totalCount * 100;
                        progress?.Report(progressPercent);
                    }
                }
            });

            _logger.LogInformation("批量提取完成：成功 {Success}/{Total} 个，并发数：{Concurrency}",
                successCount, totalCount, concurrency);
            return successCount;
        }
    }
}
