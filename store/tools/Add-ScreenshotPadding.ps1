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
    Pads phone screenshots to the Google Play listing's 9:16 portrait ratio.

.DESCRIPTION
    Some phones (this app was captured on a Pixel 7a, 1080x2400, i.e. 9:20) have
    a taller screen than Play wants for store listing screenshots. Play's own
    upload guidance asks for exactly 16:9 landscape or 9:16 portrait, see
    https://support.google.com/googleplay/android-developer/answer/9866151#screenshots

    Rather than crop content off the top or bottom each image is padded to width
    = height * 9 / 16 with solid-colour bars added evenly left and right. The
    fill colour is sampled from the source image's own top-left pixel (the app's
    status bar / background colour). No source pixels are discarded.

.PARAMETER InputPath
    A single screenshot file, or a directory of them, to pad.

.PARAMETER OutputDirectory
    Where padded copies are written. Defaults to the repo's phone screenshots
    folder.

.EXAMPLE
    pwsh store/tools/Add-ScreenshotPadding.ps1 -InputPath C:\temp\raw-screenshots

    Pads every image in C:\temp\raw-screenshots and writes the results into
    store/play-store/screenshots/phone, keeping each source file's name.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InputPath,

    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot 'store/play-store/screenshots/phone'
}
if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

function Add-Padding {
    param([string] $SourcePath, [string] $DestinationPath)

    $source = [System.Drawing.Bitmap]::new($SourcePath)
    try {
        $width = $source.Width
        $height = $source.Height
        $targetWidth = [Math]::Ceiling($height * 9.0 / 16.0)

        if ($targetWidth -le $width) {
            Write-Host "$(Split-Path -Leaf $SourcePath): already at or narrower than 9:16, copied unchanged."
            Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
            return
        }

        $fill = $source.GetPixel(0, 0)
        # Format24bppRgb explicitly: the parameterless Bitmap constructor defaults to
        # 32bppArgb, which would silently add an alpha channel Play's "no alpha"
        # screenshot rule disallows, even though every pixel ends up fully opaque.
        $padded = [System.Drawing.Bitmap]::new(
            [int]$targetWidth, $height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $graphics = [System.Drawing.Graphics]::FromImage($padded)
        try {
            $brush = [System.Drawing.SolidBrush]::new($fill)
            $graphics.FillRectangle($brush, 0, 0, $targetWidth, $height)
            $brush.Dispose()
            $xOffset = [int](($targetWidth - $width) / 2)
            $graphics.DrawImage($source, $xOffset, 0, $width, $height)
        } finally {
            $graphics.Dispose()
        }

        $padded.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $padded.Dispose()
        Write-Host "$(Split-Path -Leaf $SourcePath): $($width)x$($height) -> $([int]$targetWidth)x$($height)."
    } finally {
        $source.Dispose()
    }
}

$files = if (Test-Path -LiteralPath $InputPath -PathType Container) {
    Get-ChildItem -LiteralPath $InputPath -Filter '*.png' -File
} else {
    Get-Item -LiteralPath $InputPath
}

foreach ($file in $files) {
    $destination = Join-Path $OutputDirectory $file.Name
    Add-Padding -SourcePath $file.FullName -DestinationPath $destination
}
