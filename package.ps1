# StrmPoster 插件打包脚本
# 使用方法: .\package.ps1

$ErrorActionPreference = "Stop"

# 项目设置
$ProjectName = "StrmPosterForJellyfin"
$Version = "1.0.0"
$BuildPath = "bin\Release\net9.0"
$OutputPath = "publish"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  StrmPoster 插件打包脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 清理旧的发布目录
if (Test-Path $OutputPath) {
    Write-Host "清理旧的发布目录..." -ForegroundColor Yellow
    Remove-Item $OutputPath -Recurse -Force
}

# 创建发布目录
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# 构建项目
Write-Host "构建项目..." -ForegroundColor Yellow
dotnet build -c Release

# 复制文件
Write-Host "复制文件..." -ForegroundColor Yellow
Copy-Item "$BuildPath\$ProjectName.dll" "$OutputPath\"
Copy-Item "$BuildPath\$ProjectName.pdb" "$OutputPath\" -ErrorAction SilentlyContinue

# 创建 ZIP 包
$ZipPath = "$OutputPath\$ProjectName.zip"
Write-Host "创建 ZIP 包..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputPath\*" -DestinationPath $ZipPath -Force

# 计算 MD5
Write-Host "计算 MD5 校验值..." -ForegroundColor Yellow
$md5 = (Get-FileHash -Algorithm MD5 $ZipPath).Hash.ToLower()

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  打包完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "插件位置: $ZipPath" -ForegroundColor White
Write-Host ""
Write-Host "MD5 校验值: $md5" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步操作:" -ForegroundColor Yellow
Write-Host "1. 在 GitHub 创建 Release v$Version" -ForegroundColor White
Write-Host "2. 上传 $ProjectName.zip 到 Release" -ForegroundColor White
Write-Host "3. 更新 manifest.json 中的 checksum 为: $md5" -ForegroundColor White
Write-Host "4. 更新 manifest.json 中的 sourceUrl" -ForegroundColor White
Write-Host ""
