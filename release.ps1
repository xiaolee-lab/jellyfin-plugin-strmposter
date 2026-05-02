<#
.SYNOPSIS
StrmPoster Release Script

.DESCRIPTION
Builds the project, creates a ZIP package, updates manifest.json, and optionally uploads to GitHub Release.

.PARAMETER Version
The version number for this release (e.g., "1.0.0")

.PARAMETER Token
Optional GitHub Personal Access Token for automatic upload to Release

.EXAMPLE
.\release.ps1 -Version "1.0.0"
.\release.ps1 -Version "1.0.0" -Token "ghp_your_token_here"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$Token
)

$ErrorActionPreference = "Stop"

$ProjectName = "StrmPosterForJellyfin"
$BuildPath = "bin\Release\net9.0"
$OutputPath = "publish"
$ManifestPath = "manifest.json"
$RepoOwner = "xiaolee-lab"
$RepoName = "jellyfin-plugin-strmposter"
$GitHubApiUrl = "https://api.github.com"

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Cleaning publish directory..." -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Building project..." -ForegroundColor Yellow
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Build failed!" -ForegroundColor Red
    exit 1
}

$ZipPath = "$OutputPath\$ProjectName.zip"
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Creating ZIP package..." -ForegroundColor Yellow

$TempDir = "$OutputPath\tmp"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
Copy-Item "$BuildPath\$ProjectName.dll" "$TempDir\"
Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipPath -Force
Remove-Item $TempDir -Recurse -Force

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Calculating MD5 checksum..." -ForegroundColor Yellow
$md5 = (Get-FileHash -Algorithm MD5 $ZipPath).Hash.ToLower()

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Updating manifest.json..." -ForegroundColor Yellow

$manifestContent = [System.IO.File]::ReadAllText($ManifestPath)
if ($manifestContent.Length -gt 0 -and [int]$manifestContent[0] -eq 65279) {
    $manifestContent = $manifestContent.Substring(1)
}
$manifest = $manifestContent | ConvertFrom-Json
$versionEntry = $manifest[0].versions | Where-Object { $_.version -eq "$Version.0" }

if ($versionEntry) {
    $versionEntry.checksum = $md5
    $versionEntry.timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    $updatedContent = $manifest | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($ManifestPath, $updatedContent)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] manifest.json updated successfully!" -ForegroundColor Green
}
else {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Warning: Version $Version.0 not found in manifest.json" -ForegroundColor Yellow
}

if ($Token) {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Uploading to GitHub Release..." -ForegroundColor Yellow
    
    $releaseData = @{
        tag_name = "v$Version"
        name = "StrmPoster v$Version"
        body = "Version $Version`n`n- .strm file thumbnail extraction`n- Image provider support`n- Scheduled task support"
        draft = $false
        prerelease = $false
    } | ConvertTo-Json
    
    $headers = @{
        "Authorization" = "token $Token"
        "Accept" = "application/vnd.github.v3+json"
    }
    
    try {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Creating GitHub Release..." -ForegroundColor Yellow
        $releaseResponse = Invoke-RestMethod -Uri "$GitHubApiUrl/repos/$RepoOwner/$RepoName/releases" -Method Post -Headers $headers -Body $releaseData -ErrorAction Stop
        
        $uploadUrl = $releaseResponse.upload_url -replace '\{\?name,label\}', "?name=$(Split-Path $ZipPath -Leaf)"
        
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Uploading ZIP file..." -ForegroundColor Yellow
        $zipContent = [System.IO.File]::ReadAllBytes($ZipPath)
        $headers["Content-Type"] = "application/zip"
        
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $zipContent -ErrorAction Stop
        
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Upload successful!" -ForegroundColor Green
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Release URL: $($releaseResponse.html_url)" -ForegroundColor Cyan
    }
    catch {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Upload failed: $_" -ForegroundColor Red
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Please create Release manually" -ForegroundColor Yellow
    }
}
else {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] No GitHub Token provided, skipping upload" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "         RELEASE PROCESS COMPLETED" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "ZIP File: $ZipPath" -ForegroundColor White
Write-Host "MD5 Checksum: $md5" -ForegroundColor Cyan
Write-Host ""

if (-not $Token) {
    Write-Host "Next steps:" -ForegroundColor White
    Write-Host "1. Create GitHub Release at:" -ForegroundColor White
    Write-Host "   https://github.com/$RepoOwner/$RepoName/releases/new" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "2. Fill Release info:" -ForegroundColor White
    Write-Host "   Tag: v$Version" -ForegroundColor Cyan
    Write-Host "   Title: StrmPoster v$Version" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "3. Upload ZIP file:" -ForegroundColor White
    Write-Host "   $ZipPath" -ForegroundColor Cyan
    Write-Host ""
}

Write-Host "4. Commit and push manifest.json:" -ForegroundColor White
Write-Host "   git add manifest.json" -ForegroundColor Cyan
Write-Host "   git commit -m 'Update manifest for v$Version'" -ForegroundColor Cyan
Write-Host "   git push" -ForegroundColor Cyan
Write-Host ""
Write-Host "Done! Users can now install/update via repository." -ForegroundColor Green
Write-Host ""
