# Working Rules for Open Browser Development

These rules apply to all agents (Claude Code, ChatGPT, Opus, and others) working on the project codebase.

## Starting Work

1. Work only in `D:\projects\open-browser`.
2. Always begin with `git status --short`.
3. Do not work on a dirty repo — if there are unstaged changes, tell the user.
4. Read `docs/development/journal.md` to see recent changes.

## Build and Tests

5. After every change to `.cs` files: `dotnet build`.
6. Warning MSB3277 (WindowsBase) can be ignored — as long as there are 0 errors.
7. After changes to `OpenBridgeProtocol`: `dotnet run --project tests/OpenBridgeProtocolSmoke/OpenBridgeProtocolSmoke.csproj`.
8. After changes to `OpenBridgeHost` or executors: `dotnet run --project tests/OpenBridgeHostSmoke/OpenBridgeHostSmoke.csproj`.
9. Full build command: `dotnet build D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj`.

## Commits

10. Keep commits small — one logical package per commit.
11. Do not amend other people's commits. Do not use `--no-verify`.
12. Do not commit files from `config/local/`.

## Prohibitions

13. **NEVER** run `git push` — under no circumstances.
14. **NEVER** install packages (`dotnet add package`) without explicit user approval.
15. **NEVER** restore the old `BridgeBrowserHelper` or Cloud Code connector.
16. **NEVER** create new layers, services, managers, registries, factories, dispatchers, or queues without approval.
17. **NEVER** design for unapproved future mechanisms (watchdog, cross-tab, multi-tab registry) unless the task explicitly requires it.
18. **NEVER** work outside `D:\projects\open-browser`.
19. **NEVER** insert emoji into code.

## Decisions

20. When an architectural decision is needed — stop, present options, wait for the user.
21. After a decision is made — record it in `docs/development/decisions.md`.

## Documentation

22. When you create a new folder anywhere in the project, you must add a `README.md` inside it. The README must state: the folder's purpose, who created it, when, and what rules apply to its contents.
23. When you enter a folder, read its `README.md` first.
24. Update `docs/development/journal.md` after every session — date and what was done.
25. Update `docs/development/decisions.md` after every architectural decision.
26. Historical or superseded files go to `D:\projects\old-files\` with the naming format: `<description>__by-<author>__keep-until-YYYYMMDD`.

## After Session

27. Update `docs/development/journal.md` — add date and what was done/changed.
28. If a handoff document was created — it goes to `docs/handoff/`, then to `old-files/` after use.
