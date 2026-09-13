Run on Windows with .NET 10:

```powershell
dotnet run --project Tests.Wpf/BitExplorer.WpfTests.csproj
```

This package-free harness tests view models directly with a fake interaction
service. It never opens a real file chooser, confirmation, clipboard connection,
dialog, or window. Async commands use small files created in a unique directory
under the test output; that directory is removed after the run.

Checks cover independently ordered field interpretation versus source selection;
mapping edits, deletion, and undo; inspector commands and invalid input; unfinished
input across unrelated notifications; label command targets and cancellation;
byte order, numbering, and display settings; grid hit-testing and row reflow;
asynchronous open/export success and failure; pending-operation command guards;
completion after disposal; and save-decision cancellation. One in-process
integration test loads the compiled resources and lays out an unshown MainWindow
while capturing WPF binding errors. It verifies shared document/selection wiring,
byte-input and Enter-command bindings, field-list selection, inspector commands
resolved through the ancestor window, and field context-menu command targeting.

The STA harness pumps only its own dispatcher while awaiting async commands. It
does not call Application.Run or use external UI automation. The process exits
with code 1 if a check fails.
