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

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

# Import the distribution certificate into a throwaway keychain that only
# exists for the lifetime of this ephemeral runner.
$keychain = Join-Path $env:RUNNER_TEMP 'build.keychain-db'
$keychainPassword = [guid]::NewGuid().ToString()
Write-Output "::add-mask::$keychainPassword"
security create-keychain -p $keychainPassword $keychain
security set-keychain-settings -lut 21600 $keychain
security unlock-keychain -p $keychainPassword $keychain

$p12 = Join-Path $env:RUNNER_TEMP 'dist.p12'
try {
    [IO.File]::WriteAllBytes($p12, [Convert]::FromBase64String($env:APPLE_DIST_CERT_P12_BASE64))
    security import $p12 -k $keychain -P $env:APPLE_DIST_CERT_PASSWORD -T /usr/bin/codesign
} finally {
    Remove-Item -LiteralPath $p12 -ErrorAction Ignore
}
security set-key-partition-list -S 'apple-tool:,apple:' -s -k $keychainPassword $keychain | Out-Null
security list-keychains -d user -s $keychain login.keychain-db

# Install the provisioning profile where both Xcode and the .NET iOS tooling look.
# ($profile itself is a PowerShell automatic variable, hence the longer name.)
$provisioningProfile = Join-Path $env:RUNNER_TEMP 'profile.mobileprovision'
[IO.File]::WriteAllBytes($provisioningProfile, [Convert]::FromBase64String($env:APPLE_PROVISIONING_PROFILE_BASE64))
$uuid = (security cms -D -i $provisioningProfile | plutil -extract UUID raw -o - -).Trim()
foreach ($dir in @(
    (Join-Path $env:HOME 'Library/MobileDevice/Provisioning Profiles'),
    (Join-Path $env:HOME 'Library/Developer/Xcode/UserData/Provisioning Profiles'))) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Copy-Item $provisioningProfile (Join-Path $dir "$uuid.mobileprovision")
}
