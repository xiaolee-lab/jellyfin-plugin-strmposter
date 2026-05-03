using System;
using System.Collections.Generic;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StrmPosterForJellyfin
{
    public class JellyfinPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger<JellyfinPlugin> _logger;

        public override string Name => "StrmPoster";

        public override Guid Id => Guid.Parse("b7e23c8a-9f5d-4c8b-91a2-3d6e7f8c1b4a");

        public override string Description => "媒体封面提取工具 - 为 .strm 流媒体文件自动提取视频关键帧作为封面图";

        public static JellyfinPlugin Instance { get; private set; }

        public JellyfinPlugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILoggerFactory loggerFactory)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _logger = loggerFactory.CreateLogger<JellyfinPlugin>();

            _logger.LogInformation("StrmPoster 插件已加载");
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "StrmPoster",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }
    }

    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<MediaEncoderAdapter>();
            serviceCollection.AddSingleton<ThumbnailService>();
            serviceCollection.AddSingleton<IDynamicImageProvider, StrmPosterImageProvider>();
        }
    }
}
