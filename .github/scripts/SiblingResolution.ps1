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

# Shared sibling-repository resolution for the CI workflow; dot-source this file.
#
# The siblings follow the xecrets-mobile branch being built: the same branch name if
# the sibling has it, otherwise 'develop'. A non-empty XECRETS_*_REF environment
# variable (set in the env block of ci.yml) overrides the resolution for that sibling
# and may be a branch, tag or full commit SHA.
#
# Used by Resolve-SiblingRefs.ps1 (the version job) and Invoke-NightlyGate.ps1 (the
# nightly drift check), so both always resolve identically.

function Get-RemoteSha {
    param([string]$Repo, [string[]]$Refs)

    $line = git ls-remote "https://github.com/$Repo.git" @Refs | Select-Object -First 1
    if ($line) { return ($line -split "`t")[0] }
    return ''
}

function Resolve-Sibling {
    param([string]$Repo, [string]$Override, [string]$Branch)

    if ($Override -match '^[0-9a-fA-F]{40}$') {
        return [pscustomobject]@{ Ref = $Override; Sha = $Override.ToLowerInvariant() }
    }
    if ($Override) {
        $sha = Get-RemoteSha $Repo @("refs/heads/$Override", "refs/tags/$Override")
        if (-not $sha) { throw "Cannot resolve override '$Override' in $Repo" }
        return [pscustomobject]@{ Ref = $Override; Sha = $sha }
    }
    foreach ($candidate in @($Branch, 'develop')) {
        $sha = Get-RemoteSha $Repo @("refs/heads/$candidate")
        if ($sha) { return [pscustomobject]@{ Ref = $candidate; Sha = $sha } }
    }
    throw "Neither '$Branch' nor 'develop' exists in $Repo"
}

# Resolves all three siblings for the given mobile branch. The insertion order is
# also the line order of the build-state artifact, so do not reorder.
function Get-Siblings {
    param([string]$Branch)

    [ordered]@{
        'xecrets-net'          = Resolve-Sibling 'axantum/xecrets-net' $env:XECRETS_NET_REF $Branch
        'xecrets-common'       = Resolve-Sibling 'xecrets/xecrets-common' $env:XECRETS_COMMON_REF $Branch
        'xecrets-localization' = Resolve-Sibling 'xecrets/xecrets-localization' $env:XECRETS_LOCALIZATION_REF $Branch
    }
}
