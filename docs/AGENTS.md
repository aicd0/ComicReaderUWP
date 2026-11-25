# Agent Rules

## Answering

- Clear the thing out before summarizing it. Say what matters and stop: no account of how you got there, no unrelated
  detail, no restating the question, no restating this rule. One sentence when one sentence does.

## Coding

- Always ask before introducing anything new: a new pattern, a new design, new behavior, a new coding style, a new dependency or API.
- Do not run the full test suite unless explicitly asked. Building x64 is the default verification.
- No redundant API shapes: if a property is settable, do not also accept it as an `Add*(…)` parameter, and do not add a
  method that only forwards one argument. One way to say a thing.
- Anything a second thread reads or writes needs a synchronization mechanism chosen from what the project already uses
  (`AtomicCell<T>` for a value replaced as a whole, `Interlocked` for a counter, `volatile` for a flag), and a comment
  when the reason is not obvious. "It is only a reference assignment" is not a mechanism.
- Ownership decides placement: state that has to agree with other state belongs to the object holding both (a view
  holds its picture and its words, so the view binds them), never to a caller that must remember the pairing.
- Never switch HEAD to another commit or stage/unstage changes without the user's explicit permission. The user may
  stage, unstage or commit at any time; that does not mean the change is approved or rejected, and is never to be
  treated as input.

## Building

`dotnet build` does not work here. Use that MSBuild:

```
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' src\ComicReaderUWP.sln /p:Platform=x64 /v:q /nologo
```

`Platform=x64` is required. The solution holds the app (`src\ComicReaderUWP`), `ComicReaderUWP.Core`, `ComicReaderUWP.SDK`
and `src\Tests\ComicReaderUWP.Core.Tests`.

Analyzer diagnostics are build errors: IDE0007 (`var` where the type is apparent), IDE0008 (explicit type where
it is not), IDE2001 (embedded statement on its own line), IDE1006 (naming).

## Testing

Only when asked. Build first, then run with `--no-build` and logging, so a failure can be read from the output
instead of needing a re-run:

```
dotnet test src\Tests\ComicReaderUWP.Core.Tests\ComicReaderUWP.Core.Tests.csproj --no-build -p:Platform=x64 --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath "$env:TEMP\tests.log"
```

Never test UI behavior. Leave it to the user.

### On a test failure

1. If the test expects an old behavior that the user explicitly asked to change, fix the test expectation.
2. If it is confirmed an implementation bug, that is not part of the design the user asked for and was only
   introduced by the agent's mistake, fix the bug.
3. Otherwise, always ask for the user's expectation before making any changes.
- A flaky test is investigated, not re-run. Never loop the suite hoping to catch it again: read the log of the run that
  failed and work out why it flakes. A re-run is only for confirming a fix.
