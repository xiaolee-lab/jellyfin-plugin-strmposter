# StrmPoster Release Script
# Usage: .\release.ps1 -Version "1.0.0"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$ProjectName = "StrmPosterForJellyfin"
$BuildPath = "bin\Release\net9.0"
$OutputPath = "publish"
$ManifestPath = "manifest.json"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  StrmPoster Release Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host ""

# Clean
if (Test-Path $OutputPath) {
    Write-Host "[1/5] Cleaning..." -ForegroundColor Yellow
    Remove-Item $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Build
Write-Host "[2/5] Building..." -ForegroundColor Yellow
dotnet build -c Release

# Copy files
Write-Host "[3/5] Copying files..." -ForegroundColor Yellow
Copy-Item "$BuildPath\$ProjectName.dll" "$OutputPath\"
Copy-Item "$BuildPath\$ProjectName.pdb" "$OutputPath\" -ErrorAction SilentlyContinue

# Create ZIP
$ZipPath = "$OutputPath\$ProjectName.zip"
Write-Host "[4/5] Creating ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputPath\*" -DestinationPath $ZipPath -Force

# Calculate MD5
Write-Host "[5/5] Calculating MD5..." -ForegroundColor Yellow
$md5 = (Get-FileHash -Algorithm MD5 $ZipPath).Hash.ToLower()

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "ZIP: $ZipPath" -ForegroundColor White
Write-Host "MD5: $md5" -ForegroundColor Cyan
Write-Host ""

# Update manifest.json
Write-Host "Updating manifest.json..." -ForegroundColor Yellow

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$versionEntry = $manifest[0].versions | Where-Object { $_.version -eq "$Version.0" }

if ($versionEntry) {
    $versionEntry.checksum = $md5
    $versionEntry.timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    $manifest | ConvertTo-Json -Depth 10 | Set-Content $ManifestPath -Encoding UTF8
    Write-Host "manifest.json updated!" -ForegroundColor Green
} else {
    Write-Host "Warning: Version $Version.0 not found in manifest.json" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Create GitHub Release:" -ForegroundColor White
Write-Host "   https://github.com/xiaolee-lab/jellyfin-plugin-strmposter/releases/new" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Fill Release info:" -ForegroundColor White
Write-Host "   Tag: v$Version" -ForegroundColor Cyan
Write-Host "   Title: StrmPoster v$Version" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. Upload ZIP file:" -ForegroundColor White
Write-Host "   $ZipPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "4. Commit and push:" -ForegroundColor White
Write-Host "   git add manifest.json" -ForegroundColor Cyan
Write-Host "   git commit -m 'Update manifest for v$Version'" -ForegroundColor Cyan
Write-Host "   git push" -ForegroundColor Cyan
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host ""
