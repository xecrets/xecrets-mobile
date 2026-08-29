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

# Nightly drift check: for each of 'main' and 'develop', compares the current tip
# commits of xecrets-mobile and the resolved siblings against the commits recorded
# by the branch's last successful build (the build-state artifact), and dispatches
# a rebuild when they differ. Run from the repository root by the nightly job in
# ci.yml. The sibling resolution rules live in SiblingResolution.ps1.
#
# Inputs (environment): GH_TOKEN (for gh), GITHUB_REPOSITORY, RUNNER_TEMP,
# XECRETS_NET_REF, XECRETS_COMMON_REF, XECRETS_LOCALIZATION_REF.

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

. (Join-Path $PSScriptRoot 'SiblingResolution.ps1')

# Queried via the authenticated API rather than git ls-remote, so a missing branch is
# reported as such instead of failing the run.
function Get-MobileSha {
    param([string]$Branch)

    try {
        return (gh api "repos/$env:GITHUB_REPOSITORY/branches/$Branch" --jq .commit.sha)
    } catch {
        return '' # branch does not exist
    }
}

# The tips the branch would be built from right now, in the exact format the
# build-state job in ci.yml records.
function Get-CurrentState {
    param([string]$Branch)

    $mobile = Get-MobileSha $Branch
    if (-not $mobile) { return $null }
    $lines = @("xecrets-mobile=$mobile")
    foreach ($entry in (Get-Siblings $Branch).GetEnumerator()) {
        $lines += "$($entry.Key)=$($entry.Value.Sha)"
    }
    $lines -join "`n"
}

# The state recorded by the branch's most recent successful build, and which run
# recorded it (for diagnostics). Schedule runs are this gate itself and record
# nothing, so they are skipped -- including ones that found nothing to rebuild,
# which still complete with an overall conclusion of 'success'. Returns $null
# state when there is no previous build or its artifact has expired; both count
# as changed, which at worst causes one redundant, self-healing build.
function Get-LastBuiltState {
    param([string]$Branch)

    $runs = gh run list -R $env:GITHUB_REPOSITORY --workflow ci.yml --branch $Branch --status success --limit 20 --json databaseId,event,headSha,url | ConvertFrom-Json
    $run = $runs | Where-Object { $_.event -ne 'schedule' } | Select-Object -First 1
    if (-not $run) { return [pscustomobject]@{ State = $null; Run = $null } }
    $dir = Join-Path $env:RUNNER_TEMP "state-$($Branch -replace '[^A-Za-z0-9]', '-')"
    try {
        gh run download $run.databaseId -R $env:GITHUB_REPOSITORY --name build-state --dir $dir
    } catch {
        return [pscustomobject]@{ State = $null; Run = $run }
    }
    $state = (Get-Content (Join-Path $dir 'build-state.txt')) -join "`n"
    [pscustomobject]@{ State = $state; Run = $run }
}

foreach ($branch in @('main', 'develop')) {
    $current = Get-CurrentState $branch
    if ($null -eq $current) {
        Write-Output "Branch '$branch' does not exist; skipping."
        continue
    }
    $last = Get-LastBuiltState $branch

    # Always logged, so a later "why didn't this rebuild" question can be answered
    # from this run's log alone instead of re-deriving state by hand. A run that
    # already built the current combination -- e.g. a manual re-run of a
    # previously failed job, or a manual dispatch -- is a legitimate reason for
    # 'no changes', not necessarily a bug in this comparison.
    Write-Output "--- $branch ---"
    Write-Output "Current state:`n$current"
    if ($last.Run) {
        Write-Output "Last successful build: $($last.Run.url) (commit $($last.Run.headSha), event $($last.Run.event))"
        Write-Output "Its recorded state:`n$($last.State)"
    } else {
        Write-Output 'No prior successful non-schedule build found (or its artifact expired).'
    }

    if ($current -eq $last.State) {
        Write-Output "No changes for '$branch' since its last successful build; not rebuilding."
    } else {
        Write-Output "Sources for '$branch' changed since its last successful build; dispatching a rebuild."
        gh workflow run ci.yml -R $env:GITHUB_REPOSITORY --ref $branch
    }
}
