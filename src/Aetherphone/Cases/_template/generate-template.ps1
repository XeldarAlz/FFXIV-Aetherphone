[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

$CanvasWidth = 1500.0
$CanvasHeight = 2755.0
$Exponent = 4.2
$Segments = 64

$RailFraction = 0.0195
$BodySpan = 1.0 - 2.0 * $RailFraction
$Margin = 250.0
$BodyWidth = $CanvasWidth - 2.0 * $Margin
$BodyRight = $CanvasWidth - $Margin
$BodyBottom = $CanvasHeight - $Margin
$Metal = 0.0365 / $BodySpan * $BodyWidth
$Glass = 0.0155 / $BodySpan * $BodyWidth
$BodyRadius = 0.1548 / $BodySpan * $BodyWidth
$Bleed = 10.0

function Squircle-Path {
    param([double]$x0, [double]$y0, [double]$x1, [double]$y1, [double]$box)

    $power = 2.0 / $Exponent
    $points = New-Object System.Collections.Generic.List[string]
    $corners = @(
        @{ cx = $x0 + $box; cy = $y0 + $box; sx = -1; sy = -1; rev = $false },
        @{ cx = $x1 - $box; cy = $y0 + $box; sx = 1; sy = -1; rev = $true },
        @{ cx = $x1 - $box; cy = $y1 - $box; sx = 1; sy = 1; rev = $false },
        @{ cx = $x0 + $box; cy = $y1 - $box; sx = -1; sy = 1; rev = $true }
    )

    foreach ($corner in $corners) {
        $range = 0..$Segments
        if ($corner.rev) { $range = $Segments..0 }
        foreach ($index in $range) {
            $angle = [Math]::PI * 0.5 * $index / $Segments
            $px = [Math]::Pow([Math]::Max([Math]::Cos($angle), 0.0), $power)
            $py = [Math]::Pow([Math]::Max([Math]::Sin($angle), 0.0), $power)
            $x = $corner.cx + $corner.sx * $px * $box
            $y = $corner.cy + $corner.sy * $py * $box
            $points.Add(("{0:F2},{1:F2}" -f $x, $y))
        }
    }

    return "M " + ($points -join " L ") + " Z"
}

$bodyPath = Squircle-Path $Margin $Margin $BodyRight $BodyBottom $BodyRadius
$glassPath = Squircle-Path ($Margin + $Metal) ($Margin + $Metal) ($BodyRight - $Metal) ($BodyBottom - $Metal) `
    ($BodyRadius - $Metal)
$cutoutPath = Squircle-Path ($Margin + $Metal + $Bleed) ($Margin + $Metal + $Bleed) ($BodyRight - $Metal - $Bleed) `
    ($BodyBottom - $Metal - $Bleed) ($BodyRadius - $Metal - $Bleed)
$screenPath = Squircle-Path ($Margin + $Metal + $Glass) ($Margin + $Metal + $Glass) ($BodyRight - $Metal - $Glass) `
    ($BodyBottom - $Metal - $Glass) ($BodyRadius - $Metal - $Glass)

function Key-Rect {
    param([double]$startFraction, [double]$lengthFraction, [string]$edge, [double]$proudShare, [string]$label)

    $top = $Margin + $startFraction * ($BodyBottom - $Margin)
    $height = $lengthFraction * ($BodyBottom - $Margin)
    $rail = $RailFraction / $BodySpan * $BodyWidth
    $proud = $rail * $proudShare
    $bury = $BodyWidth / $BodySpan / 360.0
    $width = $proud + $bury
    $x = if ($edge -eq "left") { $Margin - $proud } else { $BodyRight - $bury }
    return "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' class='button'/><text x='{4:F1}' y='{5:F1}' class='tag'>{6}</text>" -f `
        $x, $top, $width, $height, ($(if ($edge -eq "left") { $x - 110 } else { $x + $width + 12 })), ($top + $height * 0.5), $label
}

$action = Key-Rect 0.157 0.046 "left" 0.45 "action"
$lockPosition = Key-Rect 0.241 0.090 "left" 0.45 "lock"
$side = Key-Rect 0.243 0.113 "right" 0.45 "side"
$camera = Key-Rect 0.590 0.050 "right" 0.30 "camera"

$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="$CanvasWidth" height="$CanvasHeight" viewBox="0 0 $CanvasWidth $CanvasHeight">
  <style>
    .guide { fill: none; stroke-width: 3; }
    .body { stroke: #22c55e; }
    .glass { stroke: #38bdf8; }
    .cutout { stroke: #f43f5e; stroke-dasharray: 18 12; }
    .screen { stroke: #a855f7; }
    .button { fill: #f59e0b; fill-opacity: 0.35; }
    .overflow { fill: none; stroke: #f59e0b; stroke-width: 2; stroke-dasharray: 10 10; stroke-opacity: .5; }
    .tag { fill: #94a3b8; font: 26px sans-serif; }
    .note { fill: #64748b; font: 30px sans-serif; }
  </style>
  <rect width="100%" height="100%" fill="#0b1020"/>
  <rect x="$Margin" y="$Margin" width="$BodyWidth" height="$($BodyBottom - $Margin)" class="overflow"/>
  <path class="guide body" d="$bodyPath"/>
  <path class="guide glass" d="$glassPath"/>
  <path class="guide cutout" d="$cutoutPath"/>
  <path class="guide screen" d="$screenPath"/>
  $action
  $lockPosition
  $side
  $camera
  <text x="40" y="70" class="note">PAINT BETWEEN GREEN AND RED, AND ANYWHERE OUTSIDE GREEN</text>
  <text x="40" y="118" class="note">green silhouette / blue glass edge / red alpha cutout / purple screen</text>
  <text x="40" y="166" class="note">the $Margin px margin outside green is free: charms, ears, straps, figures</text>
</svg>
"@

Set-Content -Path (Join-Path $PSScriptRoot "ArtCaseTemplate.svg") -Value $svg -Encoding UTF8
"metal        {0:F2}" -f $Metal
"glass        {0:F2}" -f $Glass
"body box     {0:F2}" -f $BodyRadius
"glass box    {0:F2}" -f ($BodyRadius - $Metal)
"cutout box   {0:F2}" -f ($BodyRadius - $Metal - $Bleed)
"screen box   {0:F2}" -f ($BodyRadius - $Metal - $Glass)
