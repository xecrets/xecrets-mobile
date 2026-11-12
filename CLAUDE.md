# Claude instructions for xecrets-mobile

## Output verbosity

- Keep output brief. Summaries of completed work and recaps of context do not need much detail.
- Keep plans relatively short. Go into more detail only when something warrants it.

## Tools

- Read, edit and search files only with the built-in Read/Edit/Write/Grep/Glob tools. Never use sed, awk, cat, heredocs or shell redirection to change files.

## Defensive code

- Do not write defensive code for cases that cannot realistically happen in the context where the code runs. Before writing any catch, guard, null check, null-conditional chain, `is not ... return` or other early return, name the concrete, realistic cause for it, such as a user action, provider or OS behavior, I/O, persisted data from earlier versions, or a documented framework behavior. If there is none, leave it out. For example, a handler that `Dispose` unsubscribes needs no check that the object is still alive.
- Nullable annotations in the .NET bindings for Android and iOS APIs are often generic rather than a real contract. When a value cannot be null in context, use the null-forgiving operator instead of a check, after confirming with the native source or the decompiled code if unsure. For example, write `context.GetActivity()!.Window!` for a renderer created with the activity as context, and `WindowCompat.GetInsetsController(window, window.DecorView)!`, which never returns null.
- See also the related rules in `AGENTS.md` under Code Style and Thread marshalling.
