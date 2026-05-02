using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    public class ExtractThumbnailTask : IScheduledTask
    {
        private readonly ThumbnailService _thumbnailService;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<ExtractThumbnailTask> _logger;

        public string Name => "提取媒体封面";

        public string Key => "ExtractThumbnail";

        public string Description => "从视频中提取关键帧作为媒体封面图";

        public string Category => "StrmPoster";

        public ExtractThumbnailTask(
            ThumbnailService thumbnailService,
            ILibraryManager libraryManager,
            ILogger<ExtractThumbnailTask> logger)
        {
            _thumbnailService = thumbnailService;
            _libraryManager = libraryManager;
            _logger = logger;
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("开始执行封面提取任务");

            try
            {
                var config = JellyfinPlugin.Instance?.Configuration;
                var forceReExtract = config?.ForceReExtractThumbnail ?? false;
                var enabledLibraryIds = config?.EnabledLibraryIds ?? Array.Empty<string>();

                if (forceReExtract)
                {
                    _logger.LogWarning("强制重提模式已启用，将处理所有媒体项目");
                }

                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
                    Recursive = true,
                    IsVirtualItem = false
                };

                var allItems = _libraryManager.GetItemList(query);
                _logger.LogInformation("媒体库中共有 {Count} 个视频项目", allItems.Count);

                List<BaseItem> filteredItems;
                if (enabledLibraryIds.Length > 0)
                {
                    filteredItems = allItems.Where(item =>
                    {
                        var libraryId = GetLibraryId(item);
                        return enabledLibraryIds.Contains(libraryId.ToString(), StringComparer.OrdinalIgnoreCase);
                    }).ToList();
                    _logger.LogInformation("已筛选 {Count} 个项目，来源于 {LibCount} 个选定的媒体库",
                        filteredItems.Count, enabledLibraryIds.Length);
                }
                else
                {
                    filteredItems = allItems.ToList();
                    _logger.LogInformation("未指定特定媒体库，将处理所有媒体库");
                }

                var itemsNeedingExtraction = filteredItems
                    .Where(item => _thumbnailService.NeedsExtraction(item, forceReExtract))
                    .ToList();

                _logger.LogInformation("发现 {Count} 个项目需要提取封面（强制模式：{Force}）",
                    itemsNeedingExtraction.Count, forceReExtract);

                await _thumbnailService.BatchExtractThumbnailsAsync(itemsNeedingExtraction, progress, cancellationToken);

                if (forceReExtract && config != null)
                {
                    config.ForceReExtractThumbnail = false;
                    JellyfinPlugin.Instance?.SaveConfiguration();
                    _logger.LogInformation("强制重提模式已自动关闭");
                }

                _logger.LogInformation("封面提取任务执行完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行封面提取任务时发生错误");
                throw;
            }
        }

        private Guid GetLibraryId(BaseItem item)
        {
            var current = item;
            while (current != null)
            {
                if (current.GetType().Name == "CollectionFolder")
                {
                    return current.Id;
                }
                current = current.GetParent();
            }
            return Guid.Empty;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.DailyTrigger,
                    TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
                }
            };
        }
    }
}
