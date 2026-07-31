# Agent instructions for xecrets-mobile

This repository is the Xecrets Ez mobile app. When making changes, preserve the existing code style and general practices instead of optimizing for any app-specific behavior.

Do not add unsolicited features, prompts, dialogs, or behavior. Implement only what was requested. If the requested behavior is ambiguous, or you see a problem that would require adding behavior outside the request, ask before changing it.

## Version control

Never commit any changes resulting from agent coding sessions. Committing is always
done manually. Leave all changes uncommitted in the working tree.

## Building

- Build with `dotnet build` from this project's directory, with no `-f`/`--framework` flag.
- Do not pass `-f`/`--framework` to `dotnet build` here: it sets `TargetFramework` as a global MSBuild property that leaks into the cross-repo `ProjectReference`s (Xecrets.Core, AxCrypt.*, Xecrets.Texts, Xecrets.Localization, etc.), which are not multi-targeted, causing a spurious `NETSDK1005` "Assets file doesn't have a target for ..." error even right after a clean restore.
- Plain `dotnet build` builds all configured `TargetFrameworks` (currently `net10.0-android` and `net10.0-windows10.0.19041.0` on Windows) and succeeds.

## License headers

Every C# and XAML source file, and every PowerShell script under `.github/scripts`, starts
with the GPL license header. Copy it verbatim from an existing file when adding a new one;
the wording is identical everywhere and is not adapted per file.

The exceptions are files that are substantially third-party material rather than authored
here, currently `Resources/Styles/Styles.xaml`, `Resources/Styles/Colors.xaml` and the
generated `Resources/Fonts/FluentUI.cs`. Those carry a comment naming their origin instead.
Project, manifest and configuration files carry no header.

## Thread marshalling

Never add `MainThread.InvokeOnMainThreadAsync`, `IUserInterfaceService.InvokeOnMainThreadAsync`,
`Dispatcher.Dispatch` or anything else that hops to the UI thread without first establishing
that a call site actually arrives off it. Say what that evidence is. Marshalling belongs at
the boundary where the off-thread call enters - `IncomingFileService.ReceiveAsync` is currently
the only such place - and everything downstream of it assumes the UI thread. Command handlers
on page models start on the UI thread and stay there, since no source file uses
`ConfigureAwait(false)` or `Task.Run`.

Finding such a call in the code being edited is not evidence that it is needed; neither is a
previous iteration of your own work. The same goes for any other defensive construct: establish
the need first, or leave it out.

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

## Code Style

- Prefer file-scoped namespace declarations.
- Keep `using` directives grouped at the top, with blank lines separating logical groups when helpful.
- Prefer explicit, descriptive names over abbreviations. Use private fields with the `_camelCase` convention.
- Name base classes with `Base` as a suffix, not a prefix, e.g. `PageModelBase`, not `BasePageModel`.
- Do not qualify member access with `this.` (e.g. call `Foo()`, not `this.Foo()`), except when calling an extension method, where `this.` is appropriate (e.g. `this.ApplyStandardHeader()`) — extension methods cannot be called unqualified.
- Keep nullable annotations enabled and initialize nullable-friendly defaults where appropriate, such as `string.Empty` and empty collection literals.
- Prefer small, focused classes and methods. Keep code-behind thin and move behavior into page models, services, utilities, or repositories.
- Follow the existing `async` naming pattern: methods that return `Task` or `Task<T>` should use an `Async` suffix.
- Use `await using` for disposable async resources, especially database connections and readers.
- Prefer `try/finally` when cleanup must always occur, and use the existing error-handling pattern rather than inventing a new one.
- Do not add defensive fallbacks that silently handle states that should not happen. Prefer making invalid assumptions visible by failing fast or propagating the error, unless the code is handling a real expected platform or user-cancel path.
- Do not add explicit guards or custom exceptions for states that are guaranteed by the build, generated assembly metadata, dependency injection, or framework initialization. Use the value directly (with the null-forgiving operator where required by nullable analysis) and let an invalid state fail naturally. For example, do not write `GetCustomAttribute<T>()?.Value ?? throw ...` for a required generated attribute; write `GetCustomAttribute<T>()!.Value`.
- Keep comments and XML documentation concise and factual. Add docs for public types and members when they improve readability.

## MAUI And XAML Practices

- Prefer MVVM structure: views bind to page models, and page models expose observable state and commands.
- Use CommunityToolkit MVVM patterns already present in the repo, such as `[ObservableProperty]` and `[RelayCommand]`.
- Keep constructors simple and favor dependency injection through `MauiProgram`.
- In XAML, prefer `x:DataType` and compiled bindings when the surrounding file already uses them.
- Put reusable styling, icons, and layout constants in shared resource dictionaries instead of duplicating values.
- Keep code-behind limited to initialization, event-to-command bridging, and platform or control glue.
- Prefer bindings, behaviors, converters, and templates over imperative UI code when the existing pattern already supports it.
- Prefer `[RelayCommand]` and command bindings over `On...` event handlers whenever an interaction can be represented as a command. Use event handlers only for event-to-command bridging or control/platform glue that cannot bind directly to a command.
- Always prefer `EventToCommandBehavior` or `EventToCommandBehavior<T>` over overriding `On...` event methods when the event can invoke a command.

## Platform Priorities

- Treat Android and iOS as the primary product targets.
- Treat Mac Catalyst and Windows as development and debugging targets unless a change explicitly says otherwise.
- Keep main-page commands and button behavior aligned across platforms as closely as practical.
- Prefer MAUI and CommunityToolkit abstractions for file picking, saving, sharing, launching, navigation, and UI state before adding native platform code.
- Keep platform-specific code small and only where the shared abstractions cannot provide the required behavior.

## Data And Services

- Keep repository methods focused on one responsibility and return `Task`-based APIs.
- Use parameterized SQL and explicit column mapping patterns consistent with the existing data layer.
- Keep service classes small and stateful only when the pattern already exists and is justified.
- Favor simple, direct error propagation and centralized UI error handling over broad exception suppression.

## File And Formatting Rules

- After editing any file, normalize its line endings to the host default: CRLF on Windows and LF on non-Windows systems.
- Do not leave mixed line endings in a file.
- Keep formatting consistent with the surrounding file rather than reformatting unrelated sections.
