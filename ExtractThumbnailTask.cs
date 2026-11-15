using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
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
            var config = JellyfinPlugin.Instance?.Configuration;
            if (config != null && !config.EnableThumbnailExtraction)
            {
                _logger.LogInformation("封面提取功能已在插件设置中关闭，跳过任务");
                return;
            }

            _logger.LogInformation("开始执行封面提取任务");

            try
            {
                var forceReExtract = config?.ForceReExtractThumbnail ?? false;
                var enabledLibraryIds = config?.EnabledLibraryIds ?? Array.Empty<string>();

                if (forceReExtract)
                {
                    _logger.LogWarning("强制重提模式已启用，将处理所有 .strm 项目");
                }

                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Video },
                    Recursive = true,
                    IsVirtualItem = false
                };

                if (enabledLibraryIds.Length > 0)
                {
                    var libraryIds = enabledLibraryIds
                        .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToArray();

                    if (libraryIds.Length == 0)
                    {
                        _logger.LogWarning("插件配置中的媒体库 ID 均无效，请重新选择媒体库");
                        return;
                    }

                    // 在数据库查询层过滤，避免逐项回溯父级
                    query.AncestorIds = libraryIds;
                    _logger.LogInformation("将处理 {LibCount} 个选定媒体库", libraryIds.Length);
                }
                else
                {
                    _logger.LogInformation("未指定特定媒体库，将处理所有媒体库");
                }

                var allItems = _libraryManager.GetItemList(query);

                var strmItems = allItems
                    .Where(item => item.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                _logger.LogInformation("媒体库中共有 {Count} 个 .strm 项目", strmItems.Count);

                var itemsNeedingExtraction = strmItems
                    .Where(item => _thumbnailService.NeedsExtraction(item, forceReExtract))
                    .ToList();

                _logger.LogInformation(
                    "发现 {Count} 个项目需要提取封面（强制模式：{Force}）",
                    itemsNeedingExtraction.Count, forceReExtract);

                await _thumbnailService
                    .BatchExtractThumbnailsAsync(itemsNeedingExtraction, progress, cancellationToken)
                    .ConfigureAwait(false);

                if (forceReExtract && config != null)
                {
                    config.ForceReExtractThumbnail = false;
                    JellyfinPlugin.Instance?.SaveConfiguration();
                    _logger.LogInformation("强制重提模式已自动关闭");
                }

                _logger.LogInformation("封面提取任务执行完成");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行封面提取任务时发生错误");
                throw;
            }
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
