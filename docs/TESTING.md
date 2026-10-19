# HeliSim verification and Editor safety

Read relevant source and tests before choosing checks. `AGENTS.md` owns simulation,
hardware and asset safeguards; [AGENT_WORKFLOW.md](AGENT_WORKFLOW.md) owns review.
The source tree and discovered tests are authoritative; do not assume a historical
report, fixture count or runner still describes the current checkout.

## Choose proportionate proof

| Change | Focused checks | Integrated acceptance when affected |
|---|---|---|
| Documentation/instructions | Links, paths, policy consistency, Git visibility and diff hygiene. | Workflow walkthroughs and independent review for consequential policy. No Unity run. |
| Pure logic or input/settings | Relevant Editor tests; disconnected/missing-device and persisted-setting cases. | Compile, owning UI/scene flow, keyboard fallback and actual device checks where needed. |
| Cameras/dashboard/UI | Relevant Editor tests and serialized references. | Owning scene, reassignment/full screen, local/remote camera state; rendered desktop/HMD checks and rendering cost when affected. |
| Multiplayer | Relevant role, prefab, lifecycle and integration fixtures. | Host, remote player, spectator without helicopter ownership, late join, disconnect/reconnect and target destruction; applicable player build and separate-process/network checks. |
| Dynamics/NOVA/VR | Pure conversion/state tests and disabled/disconnected paths without hardware output. | Affected scenes/build, coordinate/authority tracing and explicit human/hardware acceptance; preserve stop/reset/recenter behavior. |
| Telemetry | Field names/units, timestamps, variable-rate sampling and missing targets. | Recorded output/schema compatibility and allocation cost where changed. |

Inspect only relevant fixtures under `Assets/Code/Tests/Editor/` before expanding
coverage. They use the predefined Editor assembly; do not put NUnit in runtime code.
The current `MultiplayerIntegrationTests` uses `EnterPlayMode`/`ExitPlayMode` from
the **EditMode** runner, requires clean saved scenes, and restores scene setup.
Do not select it as a conventional PlayMode-assembly test just because it runs gameplay.
Inspect the fixture before execution: it is work in progress at this policy's adoption.

## Editor readiness and ownership

1. Read `ProjectSettings/ProjectVersion.txt` and package pins. Confirm the connected
   Editor targets this exact project/worktree and version; do not accept an upgrade.
2. Inspect Play Mode, compile/import readiness, Console, active tests/builds and dirty
   scene/asset state. Preserve unsaved work; do not silently save, discard or rebuild it.
3. Use supported Unity CLI/connected MCP/Editor operations for assets, tests and builds;
   consult the relevant installed Unity skill before commands. Do not assume a tool
   is installed or connected just because another project used it.
4. One operator and one Editor operation at a time, including manual Test Runner,
   discovery, scene operations, builds and delegated checks. Avoid a second batch
   Editor against a project already open interactively.
5. Confirm exact positive test discovery in the intended mode/assembly before running.
   Record expected fixtures/cases. Unknown, empty, duplicate or wrong-mode selection
   is a selection failure, not a pass. Preserve all assertions and full-suite meaning.

The existing `MultiplayerVerificationRunner` is a convenience helper, not ProjectI's
evidence harness. Its `RunPlayMode` actually selects the integration fixture in
EditMode, and `RunEditMode` selects a reflection-based subset. Inspect/discover that
selection before use; do not call its result a full-project suite or assume it provides
execution ownership, immutable manifests or source identity validation.

## Headless alternative

When the project is closed in other Editors, use the pinned executable and fresh
result/log paths. Example PowerShell, with the executable resolved locally first:

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe'
$projectPath = (Resolve-Path '.').Path
$runPath = Join-Path $projectPath ('TestResults\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runPath | Out-Null
& $unityExe -batchmode -projectPath $projectPath -runTests -testPlatform EditMode `
    -testResults (Join-Path $runPath 'editmode.xml') -logFile (Join-Path $runPath 'editor.log')
```

Run from the repository root. Resolve the actual installation and version from the
pin; the example path is not installation detection. Select focused tests through
supported discovered filters when appropriate. Wait for the owned process to finish
and inspect XML plus log; launch success, exit code or an empty report cannot prove a pass.
Do not add `-quit` if it would terminate the runner before results are written.

## Evidence and recovery

Use a unique directory per attempt under ignored `TestResults/`; never overwrite
prior evidence. Record requested/discovered/executed selection and mode, pass/fail/
skip counts, start/end, project/version and source identity (commit plus relevant
dirty diff), settings, evidence paths, cleanup and limits in the task record.
For build/runtime checks, retain the build result and matching source/settings
identity, target/scenes and actual scenarios; current source does not prove the
executable was built from it. Do not include secrets or private telemetry in reports.

Validate failures, skips, missing cases and unexpected scope. A zero-case run, missing
report, incomplete cleanup or interrupted operation stays incomplete. Distinguish
product failures from fixture, compile/import, selection, tooling and ownership failures;
retain primary and cleanup errors. Never weaken assertions to obtain green output.

A timeout is not termination. Inspect the owned operation and reports; confirm its
terminal state and any owned player-process cleanup before another run. Do not kill
unrelated Editors/players, clear locks/session state, delete reports or blindly retry.
Use supported cancellation/recovery, preserve authoring state, and escalate ambiguous
ownership. Retry only after diagnosis or a relevant external change, within a stated
bound; stop repeated unchanged failures and report the blocker.

After code/asset/settings changes, invalidate affected evidence. Label compatible
historical proof as reuse with paths/date/identity/limits. Report automated tests,
Editor/runtime observation, player build and actual human/hardware acceptance separately.
No physical NOVA movement is authorized by an ordinary verification request.
