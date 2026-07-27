# Agent instructions for xecrets-mobile

## Version control

Never commit any changes resulting from agent coding sessions. Committing is always
done manually. Leave all changes uncommitted in the working tree.

## License headers

Every C# and XAML source file, and every PowerShell script under `.github/scripts`, starts
with the GPL license header. Copy it verbatim from an existing file when adding a new one;
the wording is identical everywhere and is not adapted per file.

The exceptions are files that are substantially third-party material rather than authored
here, currently `Resources/Styles/Styles.xaml`, `Resources/Styles/Colors.xaml` and the
generated `Resources/Fonts/FluentUI.cs`. Those carry a comment naming their origin instead.
Project, manifest and configuration files carry no header.

## Scripting

All scripting should whenever possible be done in PowerShell (`pwsh`), which is
cross-platform and available on all development machines and CI runners. This applies
to CI workflow `run:` steps (`.github/workflows/ci.yml` sets `defaults.run.shell: pwsh`),
utility scripts, and command snippets in documentation. Only fall back to another shell
when PowerShell genuinely cannot do the job.

## CI

The CI build is documented in `docs/ci-setup.md`. Tooling versions are pinned:
.NET SDK and workload set in `global.json`, Xcode in `.xcode-version`, runner images
and action SHAs in `.github/workflows/ci.yml`. Do not introduce dependencies on
"latest" tooling.
