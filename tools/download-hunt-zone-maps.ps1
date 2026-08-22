<#
.SYNOPSIS
    Downloads Faloop's zone map images for every zone listed in HuntPOI.json.

.DESCRIPTION
    Reads the zone ids (the top-level keys) out of HuntPOI.json and downloads
    https://faloop.app/static/img/map/<zoneId>.jpg for each one. Already-downloaded
    files are skipped, so the script can be re-run to pick up new zones. Faloop serves
    these at 1300-2000px square; Dalamud has no mipmaps, so the full-size texture is
    what lands in VRAM regardless of how small the map draws. Each download is
    resampled down to -MapPixelSize square before it touches disk. Spawn point
    placement (HuntsApp.Detail.cs) reads the loaded texture's own size at draw time
    and normalizes POI coordinates against HuntPOI.json's own pixelSize/offset, so
    it is unaffected by the on-disk resolution as long as the resample keeps the
    full square extent (no cropping).

.PARAMETER SourceFile
    Path to HuntPOI.json. Defaults to the copy shipped in the Aetherphone project.

.PARAMETER OutputDirectory
    Folder to save the downloaded images into. Created if it does not exist. Defaults to the
    project's bundled asset folder, so a rebuild ships whatever this downloads.

.PARAMETER DelayMilliseconds
    Pause between downloads so the script does not hammer Faloop's server.

.PARAMETER MapPixelSize
    Square side length, in pixels, each downloaded map is resampled down to.
#>
[CmdletBinding()]
param(
    [string]$SourceFile = "$PSScriptRoot\..\src\Aetherphone\Hunts\HuntPOI.json",
    [string]$OutputDirectory = "$PSScriptRoot\..\src\Aetherphone\Hunts\Maps",
    [int]$DelayMilliseconds = 150,
    [int]$MapPixelSize = 768
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Resize-MapImage {
    param([string]$Path, [int]$Size)

    $jpegEncoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
        Where-Object { $_.MimeType -eq 'image/jpeg' }
    $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
    $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
        [System.Drawing.Imaging.Encoder]::Quality, [long]90)

    $source = [System.Drawing.Image]::FromFile($Path)
    try {
        $resized = New-Object System.Drawing.Bitmap($Size, $Size)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($resized)
            try {
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($source, 0, 0, $Size, $Size)
            }
            finally {
                $graphics.Dispose()
            }

            $tempPath = "$Path.tmp"
            $resized.Save($tempPath, $jpegEncoder, $encoderParams)
        }
        finally {
            $resized.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }

    Remove-Item $Path -Force
    Rename-Item $tempPath $Path
}

if (-not (Test-Path $SourceFile)) {
    throw "HuntPOI.json not found at $SourceFile"
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$zoneNames = (Get-Content $SourceFile -Raw | ConvertFrom-Json).PSObject.Properties.Name
Write-Host "Found $($zoneNames.Count) zones in $SourceFile"

$downloaded = 0
$skipped = 0
$failedZones = @()

foreach ($zoneName in $zoneNames) {
    $destination = Join-Path $OutputDirectory "$zoneName.jpg"
    if (Test-Path $destination) {
        Write-Host "Skipping $zoneName (already downloaded)"
        $skipped++
        continue
    }

    $url = "https://faloop.app/static/img/map/$zoneName.jpg"
    try {
        Invoke-WebRequest -Uri $url -OutFile $destination -UserAgent "Aetherphone-HuntMapFetcher/1.0"
        Resize-MapImage -Path $destination -Size $MapPixelSize
        Write-Host "Downloaded $zoneName ($($MapPixelSize)x$($MapPixelSize))"
        $downloaded++
    }
    catch {
        Write-Warning "Failed to download $zoneName from $url : $_"
        $failedZones += $zoneName
        if (Test-Path $destination) {
            Remove-Item $destination -Force
        }
    }

    Start-Sleep -Milliseconds $DelayMilliseconds
}

Write-Host ""
Write-Host "Done. $downloaded downloaded, $skipped already present, $($failedZones.Count) failed, out of $($zoneNames.Count) zones."
Write-Host "Images saved to $OutputDirectory"
if ($failedZones.Count -gt 0) {
    Write-Host "Failed zones: $($failedZones -join ', ')"
}
