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

# Computes the app version and build number for the CI build and writes them to
# GITHUB_OUTPUT. Run from the repository root by the version job in ci.yml.
#
# Inputs (environment): BUILD_NUMBER_OFFSET, GITHUB_EVENT_NAME, GITHUB_REF,
# GITHUB_RUN_NUMBER, GITHUB_OUTPUT.

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$csproj = Get-Content -Raw src/Xecrets.Mobile/Xecrets.Mobile.csproj
if ($csproj -notmatch '<ApplicationDisplayVersion>(\d+\.\d+)\.\d+</ApplicationDisplayVersion>') {
    Write-Output '::error::Could not read ApplicationDisplayVersion from Xecrets.Mobile.csproj'
    exit 1
}
$prefix = $Matches[1]

if ($env:GITHUB_EVENT_NAME -eq 'pull_request') {
    $build = 1
    $version = "$prefix.0"
    $signed = 'false'
} else {
    # GitHub assigns a monotonically increasing run number to each new run of
    # this workflow. Add a repository-configured offset so the resulting store
    # build number starts above any version uploaded before CI was introduced.
    if ([string]::IsNullOrWhiteSpace($env:BUILD_NUMBER_OFFSET)) {
        Write-Output '::error::The BUILD_NUMBER_OFFSET repository variable does not exist or is empty; see docs/ci-setup.md'
        exit 1
    }
    $offset = [int]$env:BUILD_NUMBER_OFFSET
    $runNumber = [int]$env:GITHUB_RUN_NUMBER
    $build = $offset + $runNumber
    $version = "$prefix.$build"
    $signed = 'true'
}

if ($env:GITHUB_REF -eq 'refs/heads/main') {
    $isbeta = 'false'
    $suffix = ''
} else {
    $isbeta = 'true'
    $suffix = '-beta'
}

@(
    "build-number=$build"
    "display-version=$version"
    "is-beta=$isbeta"
    "signed=$signed"
    "suffix=$suffix"
) | Add-Content -Path $env:GITHUB_OUTPUT
Write-Output "Version $version (build $build, beta: $isbeta, signed: $signed)"
