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
    [string]$ServiceAccountEnvironmentVariable,

    [Parameter(Mandatory)]
    [string]$PackageName,

    [Parameter(Mandatory)]
    [string]$AppBundle,

    [Parameter(Mandatory)]
    [string]$ReleaseName,

    # Google Play track id: 'internal', 'alpha' (Closed testing), 'beta' (Open testing),
    # 'production', or a custom track id created in the Play Console.
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9-]*$')]
    [string]$Track
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($env:GITHUB_ACTIONS -cne 'true') {
    throw 'Google Play publishing is allowed only from GitHub Actions.'
}

function Add-GitHubMask {
    param([Parameter(Mandatory)][string]$Value)

    $escapedValue = $Value.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
    [Console]::WriteLine("::add-mask::$escapedValue")
}

function ConvertTo-Base64Url {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$serviceAccountJson = [Environment]::GetEnvironmentVariable($ServiceAccountEnvironmentVariable)
if ([string]::IsNullOrWhiteSpace($serviceAccountJson)) {
    throw "The environment variable '$ServiceAccountEnvironmentVariable' is required."
}

Add-GitHubMask $serviceAccountJson
try {
    $serviceAccount = $serviceAccountJson | ConvertFrom-Json
}
catch {
    throw "The environment variable '$ServiceAccountEnvironmentVariable' does not contain valid JSON."
}

if ($serviceAccount.type -cne 'service_account') {
    throw 'The Google Play credential must have type service_account.'
}

foreach ($propertyName in @('client_email', 'private_key_id', 'private_key')) {
    if ($serviceAccount.$propertyName -isnot [string] -or
        [string]::IsNullOrWhiteSpace($serviceAccount.$propertyName)) {
        throw "The Google Play credential is missing a valid $propertyName value."
    }
}

if ($serviceAccount.client_email -notmatch '^[^@\s]+@[^@\s]+$') {
    throw 'The Google Play credential contains an invalid client_email value.'
}

if ($serviceAccount.private_key -notmatch '(?s)^-----BEGIN PRIVATE KEY-----\r?\n.+\r?\n-----END PRIVATE KEY-----\r?\n?$') {
    throw 'The Google Play credential contains an invalid private_key value.'
}

Add-GitHubMask $serviceAccount.client_email
Add-GitHubMask $serviceAccount.private_key_id
Add-GitHubMask $serviceAccount.private_key

$issuedAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$jwtHeader = @{ alg = 'RS256'; typ = 'JWT'; kid = $serviceAccount.private_key_id } |
    ConvertTo-Json -Compress
$jwtClaims = @{
    iss = $serviceAccount.client_email
    scope = 'https://www.googleapis.com/auth/androidpublisher'
    aud = 'https://oauth2.googleapis.com/token'
    iat = $issuedAt
    exp = $issuedAt + 3600
} | ConvertTo-Json -Compress

$encodedHeader = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($jwtHeader))
$encodedClaims = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($jwtClaims))
$unsignedJwt = "$encodedHeader.$encodedClaims"

$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($serviceAccount.private_key)
    $signature = $rsa.SignData(
        [Text.Encoding]::UTF8.GetBytes($unsignedJwt),
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
}
finally {
    $rsa.Dispose()
}

$assertion = "$unsignedJwt.$(ConvertTo-Base64Url $signature)"
Add-GitHubMask $assertion
$token = Invoke-RestMethod -Method Post -Uri 'https://oauth2.googleapis.com/token' -Body @{
    grant_type = 'urn:ietf:params:oauth:grant-type:jwt-bearer'
    assertion = $assertion
}

if ($null -eq $token -or $token.access_token -isnot [string] -or
    [string]::IsNullOrWhiteSpace($token.access_token)) {
    throw 'The Google OAuth token response did not contain a valid access_token.'
}

if ($token.token_type -isnot [string] -or $token.token_type -cne 'Bearer') {
    throw 'The Google OAuth token response did not contain the expected Bearer token type.'
}

if ($null -eq $token.expires_in -or [long]$token.expires_in -le 0) {
    throw 'The Google OAuth token response did not contain a valid expires_in value.'
}

Add-GitHubMask $token.access_token
$headers = @{ Authorization = "Bearer $($token.access_token)" }
$apiRoot = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/$PackageName"

$edit = Invoke-RestMethod -Method Post -Uri "$apiRoot/edits" -Headers $headers `
    -ContentType 'application/json' -Body '{}'
$editId = $edit.id

$bundle = Invoke-RestMethod -Method Post `
    -Uri "https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications/$PackageName/edits/$editId/bundles?uploadType=media" `
    -Headers $headers -ContentType 'application/octet-stream' -InFile $AppBundle

$trackUpdate = @{
    track = $Track
    releases = @(
        @{
            name = $ReleaseName
            status = 'completed'
            versionCodes = @([string]$bundle.versionCode)
        }
    )
} | ConvertTo-Json -Depth 4 -Compress

Invoke-RestMethod -Method Put -Uri "$apiRoot/edits/$editId/tracks/$Track" -Headers $headers `
    -ContentType 'application/json' -Body $trackUpdate | Out-Null
Invoke-RestMethod -Method Post -Uri "$apiRoot/edits/$editId`:commit" -Headers $headers `
    -ContentType 'application/json' -Body '{}' | Out-Null

Write-Host "Published Android version code $($bundle.versionCode) to the '$Track' track."
