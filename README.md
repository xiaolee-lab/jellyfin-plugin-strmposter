# StrmPoster for Jellyfin

[![GitHub release (latest by SemVer)](https://img.shields.io/github/v/release/xiaolee-lab/jellyfin-plugin-strmposter?sort=semver)](https://github.com/xiaolee-lab/jellyfin-plugin-strmposter/releases)
[![GitHub stars](https://img.shields.io/github/stars/xiaolee-lab/jellyfin-plugin-strmposter)](https://github.com/xiaolee-lab/jellyfin-plugin-strmposter/stargazers)
[![License](https://img.shields.io/github/license/xiaolee-lab/jellyfin-plugin-strmposter)](LICENSE)

为 .strm 流媒体文件自动提取视频关键帧作为封面图的 Jellyfin 插件

## ✨ 功能特性

- 🖼️ **图片获取器** - 在媒体库设置中选择 StrmPoster 作为图片获取器，刷新元数据时自动提取封面
- ⏰ **计划任务** - 支持定时或手动批量提取封面
- ⚙️ **配置页面** - 可选择媒体库、设置并发数、配置提取位置
- 🎯 **strm 文件支持** - 专门为 .strm 文件设计，自动解析真实媒体路径
- 🚀 **关键帧提取** - 使用 FFmpeg 快速提取关键帧，速度更快
- 📊 **百分比定位** - 使用百分比定位提取位置，不受视频时长影响

## 📦 安装方式

### 方式一：通过存储库安装（推荐）

1. 打开 Jellyfin 管理界面
2. 进入 **插件 → 存储库**
3. 点击 **+** 添加存储库
4. 填写：
   - 存储库名称：`StrmPoster`
   - 存储库 URL：`https://raw.githubusercontent.com/xiaolee-lab/jellyfin-plugin-strmposter/main/manifest.json`
5. 保存后进入 **目录** 标签
6. 找到 `StrmPoster` 并点击安装
7. 重启 Jellyfin

### 方式二：手动安装

1. 从 [Releases](https://github.com/xiaolee-lab/jellyfin-plugin-strmposter/releases) 下载最新版本
2. 解压并将 `StrmPosterForJellyfin.dll` 复制到 Jellyfin 插件目录
   - Windows: `%AppData%\Jellyfin\Server\plugins\`
   - Linux: `/var/lib/jellyfin/plugins/`
   - macOS: `~/.local/share/jellyfin/plugins/`
3. 重启 Jellyfin 服务

## 📖 使用说明

### 图片获取器

1. 进入 **媒体库 → 管理媒体库**
2. 选择一个电影库或电视剧库
3. 在 **图片获取器** 中勾选 `StrmPoster`
4. 保存设置
5. 刷新媒体库元数据即可自动提取封面

### 计划任务

1. 进入 **控制面板 → 计划任务**
2. 找到 **提取媒体封面** 任务
3. 点击 **▶** 手动执行，或等待定时执行（默认每天凌晨3点）

### 插件配置

1. 进入 **插件 → StrmPoster**
2. 配置：
   - **启用封面提取** - 是否启用自动提取功能
   - **强制重新提取所有封面** - 强制重新提取所有封面（执行后自动关闭）
   - **选择媒体库** - 选择要处理的媒体库（不选择则处理所有媒体库）
   - **并发数** - 同时处理的视频数量（1-10，默认2）
   - **提取位置** - 从视频的哪个百分比位置提取封面（5-95%，默认50%）

## 🔧 开发与构建

### 环境要求

- .NET 9.0 SDK
- Jellyfin 10.8.0+

### 本地构建

```bash
# 克隆仓库
git clone https://github.com/xiaolee-lab/jellyfin-plugin-strmposter.git
cd jellyfin-plugin-strmposter

# 构建
dotnet build -c Release

# 输出位置
# bin/Release/net9.0/StrmPosterForJellyfin.dll
```

### 发布新版本

使用一键发布脚本：

```powershell
# 在项目根目录运行
.\release.ps1 -Version "1.0.0"
```

脚本会自动：
1. 构建项目
2. 打包 ZIP 文件
3. 计算 MD5 校验值
4. 更新 manifest.json

然后：
1. 在 GitHub 创建 Release
2. 上传生成的 ZIP 文件
3. 提交并推送更新后的 manifest.json

## 📁 项目结构

```
StrmPosterForJellyfin/
├── JellyfinPlugin.cs              # 插件入口
├── PluginConfiguration.cs         # 配置类
├── ThumbnailService.cs            # 封面提取服务
├── MediaEncoderAdapter.cs         # 媒体编码器适配
├── ExtractThumbnailTask.cs        # 计划任务
├── StrmPosterImageProvider.cs     # 图片获取器
├── Configuration/
│   └── configPage.html            # 配置页面
├── thumb.jpg                      # 插件 Logo
├── manifest.json                  # 插件清单
├── release.ps1                    # 一键发布脚本
└── README.md                      # 说明文档
```

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

[MIT License](LICENSE)

## 🙏 致谢

- [Jellyfin](https://jellyfin.org/) - 开源媒体系统
- [FFmpeg](https://ffmpeg.org/) - 多媒体处理框架
