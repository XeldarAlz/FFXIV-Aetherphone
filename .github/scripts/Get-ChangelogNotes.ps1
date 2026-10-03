[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ChangelogDataPath = 'src/Aetherphone/Core/Changelog/ChangelogData.cs',

    [string]$LocalizationPath = 'src/Aetherphone/Core/Localization/L.cs',

    [int]$ChunkLimit = 4000
)

$ErrorActionPreference = 'Stop'

$changelogData = Get-Content -Raw -Path $ChangelogDataPath
$localization  = Get-Content -Raw -Path $LocalizationPath

$quoted = '"((?:[^"\\]|\\.)*)"'

function Get-ClassBody {
    param([string]$Source, [string]$ClassName)

    $match = [regex]::Match($Source, '(?m)^    internal static class ' + [regex]::Escape($ClassName) + '\s*\r?\n    \{(.*?)\r?\n    \}', 'Singleline')
    if (-not $match.Success) {
        throw "Class L.$ClassName not found in $LocalizationPath"
    }
    return $match.Groups[1].Value
}

function Get-SourceText {
    param([string]$ClassBody, [string]$MemberName)

    $match = [regex]::Match($ClassBody, 'LocString\s+' + [regex]::Escape($MemberName) + '\s*=\s*new\(\s*' + $quoted + '\s*,\s*' + $quoted + '\s*\)', 'Singleline')
    if (-not $match.Success) {
        throw "LocString $MemberName not found"
    }
    return [regex]::Unescape($match.Groups[2].Value)
}

function Get-SourceTexts {
    param([string]$ClassBody, [string]$MemberName)

    $match = [regex]::Match($ClassBody, 'LocString\[\]\s+' + [regex]::Escape($MemberName) + '\s*=\s*\{(.*?)\};', 'Singleline')
    if (-not $match.Success) {
        throw "LocString[] $MemberName not found"
    }
    $texts = [System.Collections.Generic.List[string]]::new()
    foreach ($item in [regex]::Matches($match.Groups[1].Value, 'new\(\s*' + $quoted + '\s*,\s*' + $quoted + '\s*\)', 'Singleline')) {
        $texts.Add([regex]::Unescape($item.Groups[2].Value))
    }
    return $texts
}

$entry = [regex]::Match($changelogData, 'new ChangelogEntry\(\s*"' + [regex]::Escape($Version) + '"\s*,\s*"[^"]*"\s*,\s*(?:L\.Changelog\.(\w+)\)|new ChangelogSection\[\]\s*\{(.*?)\}\))', 'Singleline')
if (-not $entry.Success) {
    return @()
}

$classBodies = @{}
function Resolve-Title {
    param([string]$ClassName, [string]$MemberName)

    if (-not $classBodies.ContainsKey($ClassName)) {
        $classBodies[$ClassName] = Get-ClassBody -Source $localization -ClassName $ClassName
    }
    return Get-SourceText -ClassBody $classBodies[$ClassName] -MemberName $MemberName
}

$changelogBody = Get-ClassBody -Source $localization -ClassName 'Changelog'
$blocks = [System.Collections.Generic.List[string]]::new()

if ($entry.Groups[1].Success) {
    $bullets = Get-SourceTexts -ClassBody $changelogBody -MemberName $entry.Groups[1].Value
    if ($bullets.Count -gt 0) {
        $blocks.Add((($bullets | ForEach-Object { '• ' + $_ }) -join "`n"))
    }
} else {
    foreach ($section in [regex]::Matches($entry.Groups[2].Value, 'new\(\s*L\.(\w+)\.(\w+)\s*,\s*L\.Changelog\.(\w+)\s*\)')) {
        $title   = Resolve-Title -ClassName $section.Groups[1].Value -MemberName $section.Groups[2].Value
        $bullets = Get-SourceTexts -ClassBody $changelogBody -MemberName $section.Groups[3].Value
        if ($bullets.Count -eq 0) {
            continue
        }
        $blocks.Add(('**' + $title + "**`n" + (($bullets | ForEach-Object { '• ' + $_ }) -join "`n")))
    }
}

$chunks  = [System.Collections.Generic.List[string]]::new()
$current = ''
foreach ($block in $blocks) {
    $candidate = if ($current.Length -eq 0) { $block } else { $current + "`n`n" + $block }
    if ($candidate.Length -le $ChunkLimit) {
        $current = $candidate
        continue
    }
    if ($current.Length -gt 0) {
        $chunks.Add($current)
        $current = ''
    }
    if ($block.Length -le $ChunkLimit) {
        $current = $block
        continue
    }
    foreach ($line in $block -split "`n") {
        $lineCandidate = if ($current.Length -eq 0) { $line } else { $current + "`n" + $line }
        if ($lineCandidate.Length -gt $ChunkLimit -and $current.Length -gt 0) {
            $chunks.Add($current)
            $current = $line
        } else {
            $current = $lineCandidate
        }
    }
}
if ($current.Length -gt 0) {
    $chunks.Add($current)
}

return $chunks.ToArray()
