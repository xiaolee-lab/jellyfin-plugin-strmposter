# StrmPoster for Jellyfin

为 .strm 流媒体文件自动提取视频关键帧作为封面图的 Jellyfin 插件

## 功能特性

- 🖼️ **图片获取器** - 在媒体库设置中选择 StrmPoster 作为图片获取器，刷新元数据时自动提取封面
- ⏰ **计划任务** - 支持定时或手动批量提取封面
- ⚙️ **配置页面** - 可选择媒体库、设置并发数、配置提取位置
- 🎯 **strm 文件支持** - 专门为 .strm 文件设计

## 安装方式

### 方式一：手动安装

1. 下载 `StrmPosterForJellyfin.dll`
2. 将文件复制到 Jellyfin 插件目录
3. 重启 Jellyfin 服务

### 方式二：通过存储库安装

1. 打开 Jellyfin 管理界面
2. 进入 **插件 → 存储库**
3. 点击 **+** 添加存储库
4. 填写：
   - 存储库名称：`StrmPoster`
   - 存储库 URL：`https://raw.githubusercontent.com/你的用户名/你的仓库名/main/manifest.json`
5. 保存后进入 **目录** 标签
6. 找到 `StrmPoster` 并点击安装
7. 重启 Jellyfin

## 使用说明

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
   - 是否启用封面提取
   - 是否强制重新提取
   - 选择要处理的媒体库
   - 并发数（1-10）
   - 提取位置（视频百分比，5-95%）

## 发布新版本

### 准备工作

1. 将代码推送到 GitHub 仓库
2. 将 thumb.jpg（logo）放到仓库根目录
3. 更新 manifest.json 中的：
   - `imageUrl` - 指向你的 GitHub 仓库中的 thumb.jpg
   - `sourceUrl` - 指向你的 GitHub Release 下载地址

### 创建 Release

1. 在 GitHub 上创建新 Release
2. 标签名：`v1.0.0`
3. 标题：`StrmPoster v1.0.0`
4. 描述：更新日志

### 打包插件

在构建好 DLL 后，将以下文件打包成 ZIP：
- StrmPosterForJellyfin.dll
- StrmPosterForJellyfin.pdb（可选）

### 计算 MD5 校验值

```powershell
Get-FileHash -Algorithm MD5 StrmPosterForJellyfin.zip
```

### 更新 manifest.json

在 manifest.json 中填入 MD5 校验值，然后推送到 GitHub

### 完成！

现在用户可以通过存储库自动安装和更新你的插件了！

## 文件说明

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
└── README.md                      # 说明文档
```

## 许可证

MIT License
