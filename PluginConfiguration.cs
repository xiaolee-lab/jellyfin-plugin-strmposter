using System;
using MediaBrowser.Model.Plugins;

namespace StrmPosterForJellyfin
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableThumbnailExtraction { get; set; } = true;

        public int ThumbnailConcurrency { get; set; } = 2;

        public int ThumbnailExtractPositionPercent { get; set; } = 50;

        public bool ForceReExtractThumbnail { get; set; } = false;

        public string[] EnabledLibraryIds { get; set; } = Array.Empty<string>();
    }
}
