using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    public class MediaEncoderAdapter
    {
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILogger<MediaEncoderAdapter> _logger;
        
        public MediaEncoderAdapter(IMediaEncoder mediaEncoder, ILogger<MediaEncoderAdapter> logger)
        {
            _mediaEncoder = mediaEncoder;
            _logger = logger;
        }
        
        public async Task<MediaSourceInfo> ExtractMediaInfoAsync(BaseItem item, CancellationToken cancellationToken = default)
        {
            try
            {
                var path = item.Path;
                if (string.IsNullOrEmpty(path))
                {
                    _logger.LogWarning("项目 {ItemName} 路径无效", item.Name);
                    return null;
                }
                
                string actualMediaPath = path;
                MediaProtocol protocol = MediaProtocol.File;
                
                if (path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("检测到 .strm 文件：{Path}，正在读取实际媒体地址", path);
                    
                    try
                    {
                        var strmContent = await File.ReadAllTextAsync(path, cancellationToken);
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
                            actualMediaPath = mediaUrl;
                            protocol = MediaProtocol.Http;
                            _logger.LogDebug("使用 HTTP 协议：{Url}", mediaUrl);
                        }
                        else if (File.Exists(mediaUrl))
                        {
                            actualMediaPath = mediaUrl;
                            protocol = MediaProtocol.File;
                            _logger.LogDebug("使用文件协议：{Path}", mediaUrl);
                        }
                        else
                        {
                            var strmDirectory = Path.GetDirectoryName(path);
                            var relativePath = Path.Combine(strmDirectory ?? "", mediaUrl);
                            
                            if (File.Exists(relativePath))
                            {
                                actualMediaPath = relativePath;
                                protocol = MediaProtocol.File;
                                _logger.LogDebug("使用相对路径：{Path}", relativePath);
                            }
                            else
                            {
                                _logger.LogWarning("未找到 .strm 对应的媒体文件：{StrmPath}，内容：{MediaUrl}", path, mediaUrl);
                                actualMediaPath = mediaUrl;
                                protocol = MediaProtocol.Http;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "读取 .strm 文件失败：{Path}", path);
                        return null;
                    }
                }
                
                var request = new MediaInfoRequest
                {
                    MediaSource = new MediaSourceInfo
                    {
                        Path = actualMediaPath,
                        Protocol = protocol,
                        Id = item.Id.ToString("N")
                    },
                    MediaType = DlnaProfileType.Video
                };
                
                var mediaInfo = await _mediaEncoder.GetMediaInfo(request, cancellationToken);
                
                _logger.LogDebug("已提取媒体信息：{ItemName}，包含 {StreamCount} 个流", 
                    item.Name, mediaInfo.MediaStreams?.Count ?? 0);
                
                return mediaInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提取媒体信息失败：{ItemName}", item.Name);
                return null;
            }
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
