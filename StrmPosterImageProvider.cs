using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    public class StrmPosterImageProvider : IDynamicImageProvider, IHasOrder
    {
        private readonly ThumbnailService _thumbnailService;
        private readonly ILogger<StrmPosterImageProvider> _logger;

        public StrmPosterImageProvider(ThumbnailService thumbnailService, ILogger<StrmPosterImageProvider> logger)
        {
            _thumbnailService = thumbnailService;
            _logger = logger;
        }

        public string Name => "StrmPoster";

        public int Order => 100;

        public bool Supports(BaseItem item)
        {
            return item is MediaBrowser.Controller.Entities.Movies.Movie ||
                   item is MediaBrowser.Controller.Entities.TV.Episode ||
                   item is MediaBrowser.Controller.Entities.Video;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary
            };
        }

        public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType imageType, CancellationToken cancellationToken)
        {
            var response = new DynamicImageResponse
            {
                HasImage = false
            };

            try
            {
                if (!Supports(item) || imageType != ImageType.Primary)
                {
                    return response;
                }

                // 只处理 strm 文件
                if (!(item.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    return response;
                }

                _logger.LogDebug("尝试为 {ItemName} 获取封面", item.Name);

                var thumbnailPath = _thumbnailService.GetThumbnailPath(item);

                if (File.Exists(thumbnailPath))
                {
                    _logger.LogDebug("找到已存在的封面：{Path}", thumbnailPath);

                    response.HasImage = true;
                    response.Path = thumbnailPath;
                    response.Format = ImageFormat.Jpg;
                    return response;
                }
                else
                {
                    _logger.LogDebug("正在为 {ItemName} 提取新封面", item.Name);

                    bool success = await _thumbnailService.ExtractAndSaveThumbnailAsync(item, cancellationToken);

                    if (success && File.Exists(thumbnailPath))
                    {
                        _logger.LogInformation("成功为 {ItemName} 提取封面", item.Name);

                        response.HasImage = true;
                        response.Path = thumbnailPath;
                        response.Format = ImageFormat.Jpg;
                        return response;
                    }
                    else
                    {
                        _logger.LogWarning("无法为 {ItemName} 提取封面", item.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取封面时发生错误：{ItemName}", item.Name);
            }

            return response;
        }
    }
}
