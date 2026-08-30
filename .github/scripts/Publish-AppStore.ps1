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

param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Z][A-Z0-9_]*$')]
    [string]$ApiKeyEnvironmentVariable,

    # App Store Connect API Key ID, e.g. '2X9R4HXF34'.
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Z0-9]{6,12}$')]
    [string]$KeyId,

    # App Store Connect API Issuer ID, a UUID shared by all keys on the team.
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')]
    [string]$IssuerId,

    [Parameter(Mandatory)]
    [string]$IpaPath,

    # 'validate' runs altool's --validate-app check (auth, bundle structure) without
    # creating a TestFlight build or consuming a build number. 'upload' (the default)
    # performs the real submission.
    [ValidateSet('validate', 'upload')]
    [string]$Action = 'upload'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($env:GITHUB_ACTIONS -cne 'true') {
    throw 'App Store publishing is allowed only from GitHub Actions.'
}

function Add-GitHubMask {
    param([Parameter(Mandatory)][string]$Value)

    $escapedValue = $Value.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
    [Console]::WriteLine("::add-mask::$escapedValue")
}

if (-not (Test-Path -LiteralPath $IpaPath -PathType Leaf)) {
    throw "The IPA file '$IpaPath' does not exist."
}

$apiKeyBase64 = [Environment]::GetEnvironmentVariable($ApiKeyEnvironmentVariable)
if ([string]::IsNullOrWhiteSpace($apiKeyBase64)) {
    throw "The environment variable '$ApiKeyEnvironmentVariable' is required."
}
Add-GitHubMask $apiKeyBase64

$privateKeyBytes = [Convert]::FromBase64String($apiKeyBase64)
$privateKeyText = [Text.Encoding]::UTF8.GetString($privateKeyBytes)
Add-GitHubMask $privateKeyText

if ($privateKeyText -notmatch '(?s)^-----BEGIN PRIVATE KEY-----\r?\n.+\r?\n-----END PRIVATE KEY-----\r?\n?$') {
    throw "The environment variable '$ApiKeyEnvironmentVariable' does not contain a valid PEM private key."
}

# altool looks up App Store Connect API keys by this fixed directory and filename
# convention: ~/.appstoreconnect/private_keys/AuthKey_<KeyID>.p8
$keysDirectory = Join-Path $env:HOME '.appstoreconnect/private_keys'
New-Item -ItemType Directory -Force -Path $keysDirectory | Out-Null
$keyPath = Join-Path $keysDirectory "AuthKey_$KeyId.p8"

try {
    [IO.File]::WriteAllBytes($keyPath, $privateKeyBytes)

    $altoolArgs = @(
        '--upload-app'
        '--type', 'ios'
        '-f', $IpaPath
        '--apiKey', $KeyId
        '--apiIssuer', $IssuerId
    )
    if ($Action -eq 'validate') {
        $altoolArgs[0] = '--validate-app'
    }

    xcrun altool @altoolArgs
}
finally {
    Remove-Item -LiteralPath $keyPath -ErrorAction Ignore
}

if ($Action -eq 'validate') {
    Write-Host "Validated '$IpaPath' for App Store Connect submission."
}
else {
    Write-Host "Uploaded '$IpaPath' to App Store Connect (TestFlight)."
}
