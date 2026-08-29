# Contributing to Xecrets Ez for Mobile

Thank you for your interest in contributing. This document covers how to build the
project and what to expect when contributing changes.

## How to Build

Building the app requires side-by-side checkouts of these sibling repositories:

- [xecrets-mobile](https://github.com/xecrets/xecrets-mobile)
- [xecrets-net](https://github.com/axantum/xecrets-net)
- [xecrets-localization](https://github.com/xecrets/xecrets-localization)
- [xecrets-common](https://github.com/xecrets/xecrets-common)

The project uses relative project references and expects all four repository directories to have
the same parent directory. The directory names must be exactly the repository names listed above.
After  checking them out in that layout, install the tooling versions pinned by the repositories
and run  the following from `xecrets-mobile/src/Xecrets.Mobile`:

`dotnet workload restore`

`dotnet restore`

`dotnet build`

There are no external dependencies that are not resolved with NuGet.

The solution file is `Xecrets.Mobile.slnx`.

## Running the Tests

The app itself is a thin user interface over the shared encryption library; the extensive
unit tests of that library live in [xecrets-net](https://github.com/axantum/xecrets-net)
and are included in this solution, the same way as in the Xecrets Cli solution. Run them
from the repository root with:

`./.github/scripts/Invoke-Tests.ps1`

That runs every `*.Test.csproj` in `Xecrets.Mobile.slnx` and fails if any test fails —
exactly what the `test` job in CI runs, so a green run locally means a green run there.
Individual projects can of course also be run with `dotnet test` or from the IDE.

Tests must pass before a pull request is submitted. If you add a test project, add it to
the solution and CI will pick it up automatically.

## License Headers

Every C# and XAML source file, and every PowerShell script under `.github/scripts`, starts
with the GPL license header. When you add a file, copy the header verbatim from an existing
one. Files that are substantially third-party material carry a comment naming their origin
instead, and project, manifest and configuration files carry no header at all.

## Code Style

Formatting (indentation, spacing, naming conventions) is enforced through the repository's
[`.editorconfig`](.editorconfig) — most editors and IDEs pick this up automatically. Beyond
formatting, coding conventions and MAUI/XAML practices are documented in
[`AGENTS.md`](AGENTS.md) and [`src/Xecrets.Mobile/AGENTS.md`](src/Xecrets.Mobile/AGENTS.md).
These apply equally whether the change is written by hand or with AI assistance.

Minimum requirement: the application must build without warnings.

## How to Contribute

Talk to us first. Due to the nature of the application, pull requests are audited very carefully.
Before opening a pull request, it is best if we discuss the change — please
[contact Axantum support](https://www.axantum.com/support) or open a GitHub issue to start the
conversation.

Found a security vulnerability instead? Please see [SECURITY.md](SECURITY.md) rather than opening
a public issue or pull request.

## AI Use

AI may be used for coding assistance, but you are responsible for everything you submit. Every
issue and pull request must be manually checked and verified by a human before it is opened —
that includes understanding the change, confirming it builds and behaves as described, and
being able to answer questions about it.

Issues or pull requests that are obviously AI-generated without evident human verification will
be rejected immediately without further consideration.

## Contact

Contact us via our [support site](https://www.axantum.com/support) or through GitHub.
