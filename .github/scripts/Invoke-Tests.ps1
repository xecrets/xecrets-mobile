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

# Runs the unit tests and fails if any test fails. Run from the repository root by the
# test job in ci.yml, and by hand for the same result locally.
#
# The solution is the single source of truth for what is tested: every project in
# Xecrets.Mobile.slnx whose file name ends in '.Test.csproj' is run, so adding a test
# project to the solution is enough to have CI run it. The test projects currently live
# in the sibling xecrets-net repository and cover the shared encryption library.
#
# Inputs (environment): none. Requires the pinned .NET SDK and side-by-side sibling
# checkouts, exactly like a normal build.

$ErrorActionPreference = 'Stop'

# Some of the library tests build their (fake, in-memory) paths from the MyDocuments
# special folder. On Linux .NET returns an empty string for a special folder that does
# not exist on disk, and a bare account such as the one on the GitHub runner image has
# no Documents folder, which then yields null paths and failing tests. Creating the
# folder is what the tests expect of any normal user profile, and is a no-op where it
# already exists, as on Windows and macOS.
[void][Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments, [Environment+SpecialFolderOption]::Create)

$solution = 'src/Xecrets.Mobile/Xecrets.Mobile.slnx'
$solutionDirectory = Split-Path -Parent $solution

$projects = ([xml](Get-Content -Raw $solution)).SelectNodes('//Project') |
    ForEach-Object { $_.Path } |
    Where-Object { $_ -like '*.Test.csproj' } |
    ForEach-Object { [IO.Path]::GetFullPath((Join-Path $solutionDirectory $_)) } |
    Sort-Object

if (-not $projects) {
    Write-Output "::error::No '*.Test.csproj' projects found in $solution"
    exit 1
}

# Every project is run even after one fails, so a single run reports all the damage
# instead of only the first failure. Hence the explicit exit-code checks rather than
# $PSNativeCommandUseErrorActionPreference.
$failed = @()
foreach ($project in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    Write-Output "::group::$name"
    dotnet test $project -c Release --nologo
    $succeeded = $LASTEXITCODE -eq 0
    Write-Output '::endgroup::'

    if (-not $succeeded) {
        $failed += $name
        Write-Output "::error::Tests failed in $name"
    }
}

if ($failed) {
    Write-Output "Failed test projects: $($failed -join ', ')"
    exit 1
}

Write-Output "All tests passed in $($projects.Count) test project(s)."
