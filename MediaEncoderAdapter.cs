using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    /// <summary>
    /// 媒体探测结果。
    /// </summary>
    public class MediaProbeResult
    {
        /// <summary>
        /// 实际可读取的媒体路径（本地文件路径或流媒体 URL）。
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 视频时长，探测失败时为 null。
        /// </summary>
        public TimeSpan? Duration { get; set; }
    }

    public class MediaEncoderAdapter
    {
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILogger<MediaEncoderAdapter> _logger;

        public MediaEncoderAdapter(IMediaEncoder mediaEncoder, ILogger<MediaEncoderAdapter> logger)
        {
            _mediaEncoder = mediaEncoder;
            _logger = logger;
        }

        public async Task<MediaProbeResult> ProbeMediaAsync(BaseItem item, CancellationToken cancellationToken = default)
        {
            try
            {
                var actualMediaPath = await ResolveMediaPathAsync(item, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(actualMediaPath))
                {
                    return null;
                }

                // 优先使用媒体库已解析的时长，避免每次提取都启动 FFprobe
                var duration = GetDurationFromItem(item) ??
                               await ProbeDurationWithFFProbeAsync(actualMediaPath, cancellationToken).ConfigureAwait(false);

                if (duration == null)
                {
                    _logger.LogWarning("无法获取视频时长：{ItemName}", item.Name);
                    return null;
                }

                _logger.LogDebug("已探测媒体信息：{ItemName}，时长：{Duration}", item.Name, duration);

                return new MediaProbeResult
                {
                    Path = actualMediaPath,
                    Duration = duration
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "探测媒体信息失败：{ItemName}", item.Name);
                return null;
            }
        }

        private async Task<string> ResolveMediaPathAsync(BaseItem item, CancellationToken cancellationToken)
        {
            var path = item.Path;
            if (string.IsNullOrEmpty(path))
            {
                _logger.LogWarning("项目 {ItemName} 路径无效", item.Name);
                return null;
            }

            if (!path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            _logger.LogDebug("检测到 .strm 文件：{Path}，正在读取实际媒体地址", path);

            try
            {
                var strmContent = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var mediaUrl = strmContent.Trim();

                if (string.IsNullOrWhiteSpace(mediaUrl))
                {
                    _logger.LogWarning(".strm 文件内容为空：{Path}", path);
                    return null;
                }

                if (mediaUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    mediaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    mediaUrl.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
                    mediaUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("使用流媒体地址：{Url}", ToLogPath(mediaUrl));
                    return mediaUrl;
                }

                if (File.Exists(mediaUrl))
                {
                    _logger.LogDebug("使用文件路径：{Path}", mediaUrl);
                    return mediaUrl;
                }

                var strmDirectory = Path.GetDirectoryName(path);
                var relativePath = Path.Combine(strmDirectory ?? string.Empty, mediaUrl);

                if (File.Exists(relativePath))
                {
                    _logger.LogDebug("使用相对路径：{Path}", relativePath);
                    return relativePath;
                }

                _logger.LogWarning(
                    "未找到 .strm 对应的媒体文件：{StrmPath}，内容：{MediaUrl}",
                    path,
                    ToLogPath(mediaUrl));
                return mediaUrl;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取 .strm 文件失败：{Path}", path);
                return null;
            }
        }

        private TimeSpan? GetDurationFromItem(BaseItem item)
        {
            var runTimeTicks = item.RunTimeTicks;
            if (runTimeTicks.HasValue && runTimeTicks.Value > 0)
            {
                return TimeSpan.FromTicks(runTimeTicks.Value);
            }

            return null;
        }

        private async Task<TimeSpan?> ProbeDurationWithFFProbeAsync(string mediaPath, CancellationToken cancellationToken)
        {
            var ffprobePath = GetFFProbePath();
            if (string.IsNullOrEmpty(ffprobePath))
            {
                _logger.LogWarning("未找到 FFprobe 路径");
                return null;
            }

            try
            {
                var arguments = new[]
                {
                    "-v", "quiet",
                    "-print_format", "json",
                    "-show_format",
                    "-show_streams",
                    "-i", mediaPath
                };

                var result = await ProcessRunner.RunAsync(ffprobePath, arguments, ProbeTimeout, cancellationToken)
                    .ConfigureAwait(false);

                if (result.TimedOut)
                {
                    _logger.LogWarning("FFprobe 探测超时：{Path}", ToLogPath(mediaPath));
                    return null;
                }

                if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    _logger.LogWarning(
                        "FFprobe 执行失败，错误码：{ExitCode}，文件：{Path}",
                        result.ExitCode,
                        ToLogPath(mediaPath));
                    _logger.LogDebug("FFprobe 输出：{Output}", result.StandardError);
                    return null;
                }

                return ParseDurationFromFFProbeOutput(result.StandardOutput);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFprobe 执行异常：{Path}", ToLogPath(mediaPath));
                return null;
            }
        }

        private TimeSpan? ParseDurationFromFFProbeOutput(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("format", out var format))
                {
                    var formatDuration = ParseSeconds(format, "duration");
                    if (formatDuration.HasValue)
                    {
                        return formatDuration;
                    }

                    if (format.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var tag in tags.EnumerateObject())
                        {
                            if (tag.Name.Equals("DURATION", StringComparison.OrdinalIgnoreCase) &&
                                tag.Value.ValueKind == JsonValueKind.String &&
                                TimeSpan.TryParse(tag.Value.GetString(), CultureInfo.InvariantCulture, out var tagDuration) &&
                                tagDuration > TimeSpan.Zero)
                            {
                                return tagDuration;
                            }
                        }
                    }
                }

                if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
                {
                    TimeSpan? longest = null;
                    foreach (var stream in streams.EnumerateArray())
                    {
                        var streamDuration = ParseSeconds(stream, "duration");
                        if (streamDuration.HasValue && (longest == null || streamDuration.Value > longest.Value))
                        {
                            longest = streamDuration;
                        }
                    }

                    return longest;
                }

                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析 FFprobe 输出失败");
                return null;
            }
        }

        private static TimeSpan? ParseSeconds(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            if (double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
                seconds > 0 &&
                seconds < TimeSpan.MaxValue.TotalSeconds &&
                !double.IsInfinity(seconds) &&
                !double.IsNaN(seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }

        /// <summary>
        /// 日志脱敏：流媒体 URL 只保留协议与主机，避免网关地址中的凭据泄露。
        /// </summary>
        private static string ToLogPath(string path)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                return $"{uri.Scheme}://{uri.Host}";
            }

            return path;
        }

        public string GetFFmpegPath()
        {
            return _mediaEncoder.EncoderPath;
        }

        public string GetFFProbePath()
        {
            return _mediaEncoder.ProbePath;
        }
    }
}
