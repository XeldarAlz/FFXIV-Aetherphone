<#
.SYNOPSIS
    Downloads Faloop's mark portrait images for every mob listed in HuntMob.json.

.DESCRIPTION
    Reads each mob's id and rank out of HuntMob.json and downloads
    https://faloop.app/static/img/mob/<rank>/<mobId>.jpg for each one, rank
    lowercased (f/b/a/s/ss). Faloop's source images are 610x472; only a
    220-pixel-wide slice starting at x=190 is kept (the leftmost 190 and the
    rightmost 200 are cropped off) before saving. Already-downloaded files are
    skipped, so the script can be re-run to pick up new marks. Pass MobId (and
    optionally ImageUrl, for a mark Faloop's own URL does not have) to fetch just
    one mark instead of the whole list.

.PARAMETER SourceFile
    Path to HuntMob.json. Defaults to the copy shipped in the Aetherphone project.

.PARAMETER OutputDirectory
    Folder to save the cropped images into. Created if it does not exist. Defaults to the
    project's bundled asset folder, so a rebuild ships whatever this downloads.

.PARAMETER DelayMilliseconds
    Pause between downloads so the script does not hammer Faloop's server.

.PARAMETER MobId
    Restrict the run to a single mob (matched against HuntMob.json's own keys, the same
    ids used for the output filenames), instead of every mark in SourceFile. Required
    alongside ImageUrl, since a link on its own does not say which mark it is for.

.PARAMETER ImageUrl
    Fetch this exact URL instead of deriving one from Faloop's own rank/mobId convention,
    for a mark Faloop does not have (or does not have at that URL). Requires MobId. Still
    cropped the same way as every other image, and still overwrites an existing file for
    that mob, ignoring the usual already-downloaded skip.
#>
[CmdletBinding()]
param(
    [string]$SourceFile = "$PSScriptRoot\..\src\Aetherphone\Hunts\HuntMob.json",
    [string]$OutputDirectory = "$PSScriptRoot\..\src\Aetherphone\Hunts\Mobs",
    [int]$DelayMilliseconds = 150,
    [string]$MobId,
    [string]$ImageUrl
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ($ImageUrl -and -not $MobId) {
    throw "ImageUrl requires MobId: which mark is this image for?"
}

if (-not (Test-Path $SourceFile)) {
    throw "HuntMob.json not found at $SourceFile"
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$SourceWidth = 610
$SourceHeight = 472
$CropLeft = 190
$CropWidth = 220

$mobEntries = (Get-Content $SourceFile -Raw | ConvertFrom-Json).PSObject.Properties
if ($MobId) {
    $mobEntries = @($mobEntries | Where-Object { $_.Name -eq $MobId })
    if ($mobEntries.Count -eq 0) {
        throw "'$MobId' was not found in $SourceFile"
    }
}

Write-Host "Found $($mobEntries.Count) marks in $SourceFile"

$downloaded = 0
$skipped = 0
$failedMobs = @()

foreach ($entry in $mobEntries) {
    $mobName = $entry.Name
    $rank = $entry.Value.rank
    if ([string]::IsNullOrWhiteSpace($rank)) {
        Write-Warning "$mobName has no rank in $SourceFile, skipping"
        $failedMobs += $mobName
        continue
    }

    $destination = Join-Path $OutputDirectory "$mobName.jpg"

    # An explicit ImageUrl means "fetch this exact image for this mark," so it always
    # overwrites, unlike the batch run's already-downloaded skip below.
    $usingCustomUrl = $ImageUrl -and $mobName -eq $MobId
    if ((Test-Path $destination) -and -not $usingCustomUrl) {
        Write-Host "Skipping $mobName (already downloaded)"
        $skipped++
        continue
    }

    $url = if ($usingCustomUrl) { $ImageUrl } else { "https://faloop.app/static/img/mob/$($rank.ToLowerInvariant())/$mobName.jpg" }
    $destinationExistedBefore = Test-Path $destination
    $tempFile = Join-Path $OutputDirectory "$mobName.download.tmp"
    try {
        Invoke-WebRequest -Uri $url -OutFile $tempFile -UserAgent "Aetherphone-HuntMobFetcher/1.0"

        $source = [System.Drawing.Image]::FromFile($tempFile)
        try {
            if ($source.Width -ne $SourceWidth -or $source.Height -ne $SourceHeight) {
                Write-Warning "$mobName is $($source.Width)x$($source.Height), expected ${SourceWidth}x${SourceHeight}; cropping the same pixel offsets anyway"
            }

            $cropLeft = [Math]::Min($CropLeft, [Math]::Max(0, $source.Width - 1))
            $cropWidth = [Math]::Min($CropWidth, $source.Width - $cropLeft)
            $srcRect = New-Object System.Drawing.Rectangle($cropLeft, 0, $cropWidth, $source.Height)
            $cropped = New-Object System.Drawing.Bitmap($cropWidth, $source.Height)
            $graphics = [System.Drawing.Graphics]::FromImage($cropped)
            try {
                $destRect = New-Object System.Drawing.Rectangle(0, 0, $cropWidth, $source.Height)
                $graphics.DrawImage($source, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            $cropped.Save($destination, [System.Drawing.Imaging.ImageFormat]::Jpeg)
            $cropped.Dispose()
        }
        finally {
            $source.Dispose()
        }

        Remove-Item $tempFile -Force
        Write-Host "Downloaded and cropped $mobName"
        $downloaded++
    }
    catch {
        Write-Warning "Failed to download $mobName from $url : $_"
        $failedMobs += $mobName
        if (Test-Path $tempFile) {
            Remove-Item $tempFile -Force
        }

        # Only cleans up a file this attempt itself could have written: a custom ImageUrl
        # retry that fails should leave a previously good image in place rather than wiping
        # it out for one bad fetch.
        if ((Test-Path $destination) -and -not $destinationExistedBefore) {
            Remove-Item $destination -Force
        }
    }

    Start-Sleep -Milliseconds $DelayMilliseconds
}

Write-Host ""
Write-Host "Done. $downloaded downloaded, $skipped already present, $($failedMobs.Count) failed, out of $($mobEntries.Count) marks."
Write-Host "Images saved to $OutputDirectory"
if ($failedMobs.Count -gt 0) {
    Write-Host "Failed marks: $($failedMobs -join ', ')"
}
