#region Copyright and GPL License

# Xecrets Ez Mobile - Copyright © 2026 Svante Seleborg, All Rights Reserved.
#
# This code file is part of Xecrets Ez Mobile, an application that uses the Xecrets.Net library, parts of which in turn
# are derived from AxCrypt as licensed under GPL v3 or later. This code is not derived from AxCrypt. It is separately
# authored and copyrighted, and licensed only as follows unless explicitly licensed otherwise.
#
# Xecrets Ez Mobile is free software: you can redistribute it and/or modify it under the terms of the GNU General
# Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any
# later version.
#
# No additional permission is granted beyond that license. If you incorporate this code into a larger work and
# distribute that work to others, you are responsible for complying with the GNU General Public License version 3 or
# later. See https://www.gnu.org/licenses/ for more information.
#
# Xecrets Ez Mobile is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the
# implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more
# details.
#
# You should have received a copy of the GNU General Public License along with Xecrets Ez Mobile. If not, see
# <https://www.gnu.org/licenses/>.
#
# The source repository can be found at https://github.com/xecrets/xecrets-mobile please go there for more information,
# suggestions and contributions. You may also visit https://www.axantum.com for more information about the author.

#endregion Copyright and GPL License

<#
.SYNOPSIS
    Renders the Google Play store listing icon from the app icon source SVGs.

.DESCRIPTION
    Composites Resources/AppIcon/appicon.svg (the solid brand background) and
    Resources/AppIcon/appiconfg.svg (the white X mark) into a single raster image
    that satisfies the Google Play icon design specifications, see
    https://developer.android.com/distribute/google-play/resources/icon-design-specifications

        512x512 px, 32-bit PNG, sRGB, at most 1024 KB, full square with no
        pre-applied rounded corners and no drop shadow. Play applies the corner
        radius (30% of the icon size) and the shadow when it renders the listing.

    The store icon therefore has to be a separate deliverable: MAUI's MauiIcon
    item generates launcher icons only (Android mipmap densities up to 192 px plus
    the adaptive icon XML), never a 512 px square, and the Play Console takes the
    listing icon as a manual upload rather than from the app bundle.

    Only the small SVG subset the two source files actually use is understood: one
    <rect> covering the viewBox, and one <path> of straight M/L/Z segments. Anything
    else is an error rather than a silently wrong icon, so a future redesign of the
    app icon fails here instead of shipping a store icon that no longer matches.

.PARAMETER OutputPath
    Where to write the PNG. Defaults to the icon folder of the Play store assets.

.PARAMETER Size
    Edge length in pixels of the square output. Defaults to the 512 px Play requires.

.EXAMPLE
    pwsh store/tools/New-PlayStoreIcon.ps1

    Run from the repository root to regenerate the committed store icon.
#>

[CmdletBinding()]
param(
    [string] $OutputPath,
    [int] $Size = 512
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$appIconDir = Join-Path $repoRoot 'src/Xecrets.Mobile/Resources/AppIcon'
$backgroundSvg = Join-Path $appIconDir 'appicon.svg'
$foregroundSvg = Join-Path $appIconDir 'appiconfg.svg'

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'store/play-store/icon/xecrets-ez-play-store-icon-512.png'
}

# Vertical sub-samples per output pixel row. Coverage is exact horizontally, so
# this alone sets the anti-aliasing quality along the near-vertical edges of the X.
$subSamples = 16

function Get-SvgViewBox {
    param([xml] $Svg)

    $viewBox = $Svg.svg.viewBox
    if (-not $viewBox) {
        throw "The SVG has no viewBox attribute."
    }
    $parts = $viewBox -split '[\s,]+' | Where-Object { $_ -ne '' }
    if ($parts.Count -ne 4 -or [double]$parts[0] -ne 0 -or [double]$parts[1] -ne 0) {
        throw "Only a viewBox anchored at the origin is supported, found '$viewBox'."
    }
    $width = [double]$parts[2]
    $height = [double]$parts[3]
    if ($width -ne $height) {
        throw "Only a square viewBox is supported, found '$viewBox'."
    }
    return $width
}

function ConvertFrom-HexColor {
    param([string] $Hex)

    if ($Hex -notmatch '^#([0-9A-Fa-f]{6})$') {
        throw "Only #RRGGBB colors are supported, found '$Hex'."
    }
    $value = [Convert]::ToInt32($Matches[1], 16)
    # The inner parentheses matter: comma binds tighter than -band in PowerShell.
    return @((($value -shr 16) -band 0xFF), (($value -shr 8) -band 0xFF), ($value -band 0xFF))
}

function Get-BackgroundColor {
    param([string] $Path)

    [xml] $svg = Get-Content -Raw -LiteralPath $Path
    $extent = Get-SvgViewBox -Svg $svg

    $rects = @($svg.svg.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
    if ($rects.Count -ne 1 -or $rects[0].LocalName -ne 'rect') {
        throw "The background SVG must contain exactly one <rect>, found $($rects.Count) element(s)."
    }
    $rect = $rects[0]
    if ([double]$rect.x -ne 0 -or [double]$rect.y -ne 0 -or
        [double]$rect.width -ne $extent -or [double]$rect.height -ne $extent) {
        throw "The background <rect> must cover the whole viewBox."
    }
    return ConvertFrom-HexColor -Hex $rect.fill
}

function Get-ForegroundPolygons {
    param([string] $Path, [double] $Extent)

    [xml] $svg = Get-Content -Raw -LiteralPath $Path
    if ((Get-SvgViewBox -Svg $svg) -ne $Extent) {
        throw "The foreground and background SVGs must share the same viewBox."
    }

    $paths = @($svg.svg.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
    if ($paths.Count -ne 1 -or $paths[0].LocalName -ne 'path') {
        throw "The foreground SVG must contain exactly one <path>, found $($paths.Count) element(s)."
    }

    $color = ConvertFrom-HexColor -Hex $paths[0].fill
    $tokens = $paths[0].d -split '[\s,]+' | Where-Object { $_ -ne '' }

    $polygons = [System.Collections.Generic.List[object]]::new()
    $current = $null
    $index = 0
    while ($index -lt $tokens.Count) {
        $command = $tokens[$index]
        switch -CaseSensitive ($command) {
            'M' {
                if ($null -ne $current) {
                    throw "An open subpath must be closed with Z before a new M."
                }
                $current = [System.Collections.Generic.List[double[]]]::new()
                $current.Add(@([double]$tokens[$index + 1], [double]$tokens[$index + 2]))
                $index += 3
            }
            'L' {
                if ($null -eq $current) {
                    throw "An L command appeared before any M command."
                }
                $current.Add(@([double]$tokens[$index + 1], [double]$tokens[$index + 2]))
                $index += 3
            }
            'Z' {
                if ($null -eq $current -or $current.Count -lt 3) {
                    throw "A Z command closed a subpath with fewer than three points."
                }
                $polygons.Add($current)
                $current = $null
                $index += 1
            }
            default {
                throw "Unsupported path command '$command'; only M, L and Z are understood."
            }
        }
    }
    if ($null -ne $current) {
        throw "The path ended with an unclosed subpath."
    }
    if ($polygons.Count -eq 0) {
        throw "The path contains no closed subpaths."
    }

    return [pscustomobject]@{ Color = $color; Polygons = $polygons }
}

function Get-Edges {
    param($Polygons)

    # Flat parallel arrays rather than objects: the inner rasterizer loop runs once
    # per edge per sub-scanline, which is where all the time goes.
    $x0 = [System.Collections.Generic.List[double]]::new()
    $y0 = [System.Collections.Generic.List[double]]::new()
    $x1 = [System.Collections.Generic.List[double]]::new()
    $y1 = [System.Collections.Generic.List[double]]::new()

    foreach ($polygon in $Polygons) {
        for ($i = 0; $i -lt $polygon.Count; $i++) {
            $a = $polygon[$i]
            $b = $polygon[($i + 1) % $polygon.Count]
            if ($a[1] -eq $b[1]) {
                continue    # Horizontal edges never contribute a crossing.
            }
            $x0.Add($a[0]); $y0.Add($a[1]); $x1.Add($b[0]); $y1.Add($b[1])
        }
    }

    return [pscustomobject]@{
        X0 = $x0.ToArray(); Y0 = $y0.ToArray(); X1 = $x1.ToArray(); Y1 = $y1.ToArray()
        Count = $x0.Count
    }
}

function Get-CoverageRow {
    <#
        Anti-aliased scanline coverage for one output pixel row.

        Horizontal coverage is computed analytically from the span end points, and
        the interior full-coverage pixels are accumulated into a difference array
        that is prefix-summed once per row, so a span costs the same regardless of
        how many pixels it covers. Vertically the row is sampled $subSamples times.

        The even-odd rule pairs consecutive crossings, which is exact here because
        both subpaths of the X are simple and disjoint.
    #>
    param($Edges, [int] $Row, [int] $Size, [double] $Scale, [int] $SubSamples)

    $partial = [double[]]::new($Size)
    $delta = [double[]]::new($Size + 1)
    $weight = 1.0 / $SubSamples
    $crossings = [double[]]::new($Edges.Count)

    for ($sample = 0; $sample -lt $SubSamples; $sample++) {
        # Sub-scanline centre in device space, converted to source space.
        $y = ($Row + ($sample + 0.5) * $weight) / $Scale

        $found = 0
        for ($e = 0; $e -lt $Edges.Count; $e++) {
            $ey0 = $Edges.Y0[$e]
            $ey1 = $Edges.Y1[$e]
            # Half-open in y so a shared vertex counts exactly once.
            if (($ey0 -le $y -and $y -lt $ey1) -or ($ey1 -le $y -and $y -lt $ey0)) {
                $crossings[$found++] = $Edges.X0[$e] +
                    ($y - $ey0) * ($Edges.X1[$e] - $Edges.X0[$e]) / ($ey1 - $ey0)
            }
        }
        if ($found -eq 0) {
            continue
        }
        [Array]::Sort($crossings, 0, $found)

        for ($pair = 0; $pair + 1 -lt $found; $pair += 2) {
            $xa = $crossings[$pair] * $Scale
            $xb = $crossings[$pair + 1] * $Scale
            if ($xa -lt 0) { $xa = 0 }
            if ($xb -gt $Size) { $xb = $Size }
            if ($xb -le $xa) { continue }

            $i0 = [int][Math]::Floor($xa)
            $i1 = [int][Math]::Floor($xb)
            if ($i1 -ge $Size) { $i1 = $Size - 1 }

            if ($i0 -eq $i1) {
                $partial[$i0] += ($xb - $xa) * $weight
            } else {
                $partial[$i0] += ($i0 + 1 - $xa) * $weight
                $partial[$i1] += ($xb - $i1) * $weight
                $delta[$i0 + 1] += $weight
                $delta[$i1] -= $weight
            }
        }
    }

    $running = 0.0
    for ($x = 0; $x -lt $Size; $x++) {
        $running += $delta[$x]
        $coverage = $partial[$x] + $running
        if ($coverage -lt 0) { $coverage = 0 }
        if ($coverage -gt 1) { $coverage = 1 }
        $partial[$x] = $coverage
    }
    return $partial
}

function Get-Crc32Table {
    $table = [uint32[]]::new(256)
    for ($n = 0; $n -lt 256; $n++) {
        $c = [uint32]$n
        for ($k = 0; $k -lt 8; $k++) {
            if ($c -band 1) {
                $c = [uint32](0xEDB88320u -bxor ($c -shr 1))
            } else {
                $c = [uint32]($c -shr 1)
            }
        }
        $table[$n] = $c
    }
    return $table
}

$script:Crc32Table = Get-Crc32Table

function Get-Crc32 {
    param([byte[]] $Bytes)

    $c = 0xFFFFFFFFu
    foreach ($b in $Bytes) {
        $c = [uint32]($script:Crc32Table[($c -bxor $b) -band 0xFF] -bxor ($c -shr 8))
    }
    return [uint32]($c -bxor 0xFFFFFFFFu)
}

function ConvertTo-BigEndian {
    param([uint32] $Value)

    return [byte[]] @((($Value -shr 24) -band 0xFF), (($Value -shr 16) -band 0xFF),
        (($Value -shr 8) -band 0xFF), ($Value -band 0xFF))
}

function Add-PngChunk {
    param([System.IO.Stream] $Stream, [string] $Type, [byte[]] $Data)

    $typeBytes = [System.Text.Encoding]::ASCII.GetBytes($Type)
    $Stream.Write((ConvertTo-BigEndian ([uint32]$Data.Length)), 0, 4)
    $Stream.Write($typeBytes, 0, 4)
    if ($Data.Length -gt 0) {
        $Stream.Write($Data, 0, $Data.Length)
    }
    $crc = Get-Crc32 -Bytes ($typeBytes + $Data)
    $Stream.Write((ConvertTo-BigEndian $crc), 0, 4)
}

function Write-Png {
    <#
        Writes 8-bit RGBA (PNG colour type 6, the "32-bit PNG" Play asks for) with
        an sRGB chunk and the matching gAMA for decoders that ignore sRGB. Every
        scanline uses filter type 0; the artwork is flat colour, so the deflate
        stream is tiny either way and staying unfiltered keeps this readable.
    #>
    param([string] $Path, [int] $Size, [byte[]] $Pixels)

    $raw = [byte[]]::new(($Size * 4 + 1) * $Size)
    $source = 0
    $target = 0
    for ($y = 0; $y -lt $Size; $y++) {
        $raw[$target++] = 0
        [Array]::Copy($Pixels, $source, $raw, $target, $Size * 4)
        $source += $Size * 4
        $target += $Size * 4
    }

    $compressed = [System.IO.MemoryStream]::new()
    $deflate = [System.IO.Compression.ZLibStream]::new(
        $compressed, [System.IO.Compression.CompressionLevel]::SmallestSize, $true)
    $deflate.Write($raw, 0, $raw.Length)
    $deflate.Dispose()

    $ihdr = (ConvertTo-BigEndian ([uint32]$Size)) + (ConvertTo-BigEndian ([uint32]$Size)) +
        [byte[]] @(8, 6, 0, 0, 0)

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $file = [System.IO.File]::Create($Path)
    try {
        $signature = [byte[]] @(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
        $file.Write($signature, 0, $signature.Length)
        Add-PngChunk -Stream $file -Type 'IHDR' -Data $ihdr
        Add-PngChunk -Stream $file -Type 'sRGB' -Data ([byte[]] @(0))
        Add-PngChunk -Stream $file -Type 'gAMA' -Data (ConvertTo-BigEndian ([uint32]45455))
        Add-PngChunk -Stream $file -Type 'IDAT' -Data $compressed.ToArray()
        Add-PngChunk -Stream $file -Type 'IEND' -Data ([byte[]] @())
    } finally {
        $file.Dispose()
        $compressed.Dispose()
    }
}

$background = Get-BackgroundColor -Path $backgroundSvg
[xml] $backgroundXml = Get-Content -Raw -LiteralPath $backgroundSvg
$extent = Get-SvgViewBox -Svg $backgroundXml
$foreground = Get-ForegroundPolygons -Path $foregroundSvg -Extent $extent
$edges = Get-Edges -Polygons $foreground.Polygons
$scale = $Size / $extent

Write-Host "Rendering $Size x $Size from $extent x $extent source, $($edges.Count) edges."

$pixels = [byte[]]::new($Size * $Size * 4)
$offset = 0
for ($y = 0; $y -lt $Size; $y++) {
    $coverage = Get-CoverageRow -Edges $edges -Row $y -Size $Size -Scale $scale -SubSamples $subSamples
    for ($x = 0; $x -lt $Size; $x++) {
        $a = $coverage[$x]
        for ($channel = 0; $channel -lt 3; $channel++) {
            $value = $background[$channel] + ($foreground.Color[$channel] - $background[$channel]) * $a
            $pixels[$offset++] = [byte][Math]::Round($value)
        }
        $pixels[$offset++] = 255    # Fully opaque: Play shows its own background through alpha.
    }
}

Write-Png -Path $OutputPath -Size $Size -Pixels $pixels

$written = Get-Item -LiteralPath $OutputPath
Write-Host "Wrote $($written.FullName) ($([Math]::Round($written.Length / 1KB, 1)) KB)."
