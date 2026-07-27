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

# Resolves the sibling repositories for the branch being built and writes their
# immutable tip commit SHAs to GITHUB_OUTPUT. Run from the repository root by the
# version job in ci.yml. The resolution rules live in SiblingResolution.ps1; the
# resolved commits are checked out by every platform and recorded in build-state.
#
# Inputs (environment): GITHUB_EVENT_NAME, GITHUB_HEAD_REF, GITHUB_REF_NAME,
# XECRETS_NET_REF, XECRETS_TEXTS_REF, XECRETS_LOCALIZATION_REF, GITHUB_OUTPUT.

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

. (Join-Path $PSScriptRoot 'SiblingResolution.ps1')

$branch = if ($env:GITHUB_EVENT_NAME -eq 'pull_request') { $env:GITHUB_HEAD_REF } else { $env:GITHUB_REF_NAME }
$siblings = Get-Siblings $branch

$net = $siblings['xecrets-net']
$texts = $siblings['xecrets-texts']
$localization = $siblings['xecrets-localization']

@(
    "net-sha=$($net.Sha)"
    "texts-sha=$($texts.Sha)"
    "localization-sha=$($localization.Sha)"
) | Add-Content -Path $env:GITHUB_OUTPUT
Write-Output "Building '$branch': xecrets-net@$($net.Ref) ($($net.Sha)), xecrets-texts@$($texts.Ref) ($($texts.Sha)), xecrets-localization@$($localization.Ref) ($($localization.Sha))"
