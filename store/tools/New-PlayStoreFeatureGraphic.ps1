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
    Composites the Google Play store listing's feature graphic.

.DESCRIPTION
    Builds the 1024x500 banner shown at the top of the Play Store listing, see
    https://support.google.com/googleplay/android-developer/answer/9866151 (PNG
    or JPEG, up to 15 MB, no alpha, exactly 1024x500).

    Reuses existing assets: the listing icon and the "main actions" phone
    screenshot, whose 9:16 padding bars (see Add-ScreenshotPadding.ps1) are
    cropped back off first to recover the original 1080x2400 Pixel 7a capture.
    The only new content is the brand colour (read from the app icon's
    background SVG) and the two lines of title text, set in Roboto - Android's
    system font, and the same face the screenshot itself was rendered in. The
    two weights used are bundled under tools/fonts/roboto (Apache License 2.0,
    see the LICENSE file there).

    Layout, left to right: a rounded icon on a faint highlight card, the
    app name and a short tagline, then the screenshot in a simple dark bezel.
    All three are vertically centred in the 500 px height.

.PARAMETER OutputPath
    Where to write the PNG. Defaults to the feature graphic folder of the Play
    store assets.

.EXAMPLE
    pwsh store/tools/New-PlayStoreFeatureGraphic.ps1

    Run from the repository root to regenerate the committed feature graphic.
#>

[CmdletBinding()]
param(
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$iconPath = Join-Path $repoRoot 'store/play-store/icon/xecrets-ez-play-store-icon-512.png'
$screenshotPath = Join-Path $repoRoot 'store/play-store/screenshots/phone/01-main-actions.png'
$backgroundSvgPath = Join-Path $repoRoot 'src/Xecrets.Mobile/Resources/AppIcon/appicon.svg'
$fontDir = Join-Path $repoRoot 'store/tools/fonts/roboto'

# The Pixel 7a capture this screenshot came from, before Add-ScreenshotPadding.ps1
# padded it out to Play's 9:16 listing ratio. Recovering the original crop needs the
# real capture size.
$originalCaptureWidth = 1080
$originalCaptureHeight = 2400

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'store/play-store/feature-graphic/xecrets-ez-feature-graphic.png'
}

function ConvertFrom-HexColor {
    param([string] $Hex)

    if ($Hex -notmatch '^#([0-9A-Fa-f]{6})$') {
        throw "Only #RRGGBB colors are supported, found '$Hex'."
    }
    $value = [Convert]::ToInt32($Matches[1], 16)
    return [System.Drawing.Color]::FromArgb(
        (($value -shr 16) -band 0xFF), (($value -shr 8) -band 0xFF), ($value -band 0xFF))
}

function Get-RoundedRectPath {
    param([float] $X, [float] $Y, [float] $Width, [float] $Height, [float] $Radius)

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $Radius * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

[xml] $backgroundSvg = Get-Content -Raw -LiteralPath $backgroundSvgPath
$brandColor = ConvertFrom-HexColor -Hex $backgroundSvg.svg.rect.fill

$fontCollection = [System.Drawing.Text.PrivateFontCollection]::new()
$fontCollection.AddFontFile((Join-Path $fontDir 'Roboto-Bold.ttf'))
$fontCollection.AddFontFile((Join-Path $fontDir 'Roboto-Regular.ttf'))
$roboto = $fontCollection.Families | Where-Object { $_.Name -eq 'Roboto' } | Select-Object -First 1
if (-not $roboto -or
    -not $roboto.IsStyleAvailable([System.Drawing.FontStyle]::Bold) -or
    -not $roboto.IsStyleAvailable([System.Drawing.FontStyle]::Regular)) {
    throw "Could not load the Roboto Bold/Regular styles from '$fontDir'."
}

$canvasWidth = 1024
$canvasHeight = 500

$canvas = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $bgBrush = [System.Drawing.SolidBrush]::new($brandColor)
    $g.FillRectangle($bgBrush, 0, 0, $canvasWidth, $canvasHeight)
    $bgBrush.Dispose()

    # --- Icon chip, left-aligned -------------------------------------------------
    $iconSize = 240
    $iconX = 64
    $iconY = [int](($canvasHeight - $iconSize) / 2)

    $cardPad = 22
    $cardPath = Get-RoundedRectPath -X ($iconX - $cardPad) -Y ($iconY - $cardPad) `
        -Width ($iconSize + 2 * $cardPad) -Height ($iconSize + 2 * $cardPad) -Radius 32
    $cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(28, 255, 255, 255))
    $g.FillPath($cardBrush, $cardPath)
    $cardBrush.Dispose()
    $cardPath.Dispose()

    $iconImage = [System.Drawing.Image]::FromFile($iconPath)
    $iconClip = Get-RoundedRectPath -X $iconX -Y $iconY -Width $iconSize -Height $iconSize -Radius ($iconSize * 0.18)
    $g.SetClip($iconClip)
    $g.DrawImage($iconImage, $iconX, $iconY, $iconSize, $iconSize)
    $g.ResetClip()
    $iconClip.Dispose()
    $iconImage.Dispose()

    # The committed screenshot was padded to Play's 9:16 listing ratio (see
    # Add-ScreenshotPadding.ps1); recover the original capture by cropping that
    # padding back off before using it here. Both dimensions come from the known
    # capture size above.
    $paddedShot = [System.Drawing.Image]::FromFile($screenshotPath)
    if ($paddedShot.Height -ne $originalCaptureHeight) {
        throw "Expected the screenshot's height to still be $originalCaptureHeight px " +
            "(only its width should change when it's padded), found $($paddedShot.Height)."
    }
    $captureWidth = $originalCaptureWidth
    $captureHeight = $originalCaptureHeight
    $cropX = [int](($paddedShot.Width - $captureWidth) / 2)
    $sourceRect = [System.Drawing.Rectangle]::new($cropX, 0, $captureWidth, $captureHeight)

    $mockHeight = 420
    $mockWidth = [int]($mockHeight * $captureWidth / $captureHeight)
    $bezel = 10
    $mockX = $canvasWidth - $mockWidth - $bezel - 72
    $mockY = [int](($canvasHeight - $mockHeight) / 2)

    $bezelPath = Get-RoundedRectPath -X ($mockX - $bezel) -Y ($mockY - $bezel) `
        -Width ($mockWidth + 2 * $bezel) -Height ($mockHeight + 2 * $bezel) -Radius 34
    $bezelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 18, 20, 28))
    $g.FillPath($bezelBrush, $bezelPath)
    $bezelBrush.Dispose()
    $bezelPath.Dispose()

    $shotClip = Get-RoundedRectPath -X $mockX -Y $mockY -Width $mockWidth -Height $mockHeight -Radius 24
    $g.SetClip($shotClip)
    $g.DrawImage($paddedShot, [System.Drawing.Rectangle]::new($mockX, $mockY, $mockWidth, $mockHeight), $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.ResetClip()
    $shotClip.Dispose()
    $paddedShot.Dispose()

    # --- Title and tagline, between the icon and the phone ------------------------
    $textX = $iconX + $iconSize + $cardPad + 44
    $textLimit = $mockX - $bezel - 32

    $titleFont = [System.Drawing.Font]::new($roboto, 44, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $taglineFont = [System.Drawing.Font]::new($roboto, 21, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $taglineBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 228, 245))

    $title = 'Xecrets Ez'
    $tagline = 'Simple, on-device encryption'

    $titleSize = $g.MeasureString($title, $titleFont)
    $taglineSize = $g.MeasureString($tagline, $taglineFont)
    $maxTextWidth = [Math]::Max($titleSize.Width, $taglineSize.Width)
    if ($textX + $maxTextWidth -gt $textLimit) {
        throw "Title/tagline text ($([Math]::Ceiling($maxTextWidth))px) overruns the space before the phone mockup " +
            "(available $([Math]::Ceiling($textLimit - $textX))px). Shorten the copy or shrink the fonts."
    }

    $blockHeight = $titleSize.Height + 14 + $taglineSize.Height
    $textY = [int](($canvasHeight - $blockHeight) / 2)

    $g.DrawString($title, $titleFont, $whiteBrush, $textX, $textY)
    $g.DrawString($tagline, $taglineFont, $taglineBrush, $textX, $textY + $titleSize.Height + 14)

    $titleFont.Dispose()
    $taglineFont.Dispose()
    $whiteBrush.Dispose()
    $taglineBrush.Dispose()
} finally {
    $g.Dispose()
}

$directory = Split-Path -Parent $OutputPath
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$canvas.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()
$fontCollection.Dispose()

$written = Get-Item -LiteralPath $OutputPath
Write-Host "Wrote $($written.FullName) ($([Math]::Round($written.Length / 1KB, 1)) KB)."
