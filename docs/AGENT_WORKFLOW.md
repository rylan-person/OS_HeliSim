# HeliSim agent workflow

`AGENTS.md` owns project invariants and model selection. This guide owns task routing,
review and delegation; [TESTING.md](TESTING.md) owns Editor safety and evidence.
Read these procedures when relevant, rather than loading every plan or fixture.
Explicit task constraints and accepted feature contracts remain binding.

## Task routing

| Request | Authorized route |
|---|---|
| Feature, fix, refactor or experiment | Inspect architecture, classify risk, plan substantial work, implement the smallest complete change, review and verify. |
| Review or retrospective audit | Inspect existing source/diff and relevant evidence; return prioritized findings with locations and failure scenarios. Do not infer permission to repair, run tests/builds, operate Unity or commit. Explicitly requested checks retain their scope. |
| Research or planning | Produce the requested answer/plan. Do not start implementation or spawn implementers merely because a plan exists. |
| Documentation or workflow maintenance | Check consistency, links, paths and applicable ignore rules. Review consequential policy independently; no routine Unity operation. |
| Isolated lookup or wording fix | Handle directly with focused inspection; no mandatory task packet or reviewer team. |

For mixed requests, distinguish the authorized actions in one record. Inspect dirty
and staged changes before edits. Do not reset, revert, move or commit other work.
Use a separate worktree only when isolation is needed; account for Unity's import
cost and the fact that uncommitted changes are not copied into a fresh checkout.

## Risk and review

Classify by consequences, uncertainty, coupling and reversibility, not diff size.
Review effort and verification scope are separate: fewer reviewers never remove
affected tests, build checks or acceptance requirements.

| Risk | HeliSim examples | Review |
|---|---|---|
| High | NOVA commands, coordinate conversion, limits/failsafes; flight dynamics or authoritative physics; NGO authority/RPCs, role/spawn/disconnect lifecycle; XR tracking/recenter hierarchy; telemetry schema/units; broad shared state changes. Treat uncertain consequential boundaries as high. | Independent technical review before implementation and against the integrated result, plus independent simulation/hardware or user-experience perspectives where affected. Record coverage; combine roles only where that preserves independent scrutiny. |
| Medium | Bounded local camera/dashboard/UI behavior, input display/rebinding presentation, narrow Editor tooling with understood contracts. | Relevant independent specialist pre/post review; record why the selected perspective covers the boundary. Escalate if ownership, lifecycle, storage or hardware behavior changes. |
| Low | Wording/link repair, isolated lookup, mechanical documentation correction with no behavior or policy change. | Lead inspection and focused checks; no automatic subagent team. |

A one-line axis sign, clamp, RPC or timestamp change can be high risk. A camera
change that reparents an XR rig or enables remote input is not merely presentation.
Workflow changes affecting authority, evidence or acceptance require independent
review even though they change no runtime code.

For high-risk reviews, cover technical correctness (authority, lifecycle, units,
threading, serialization and failure paths), simulation/hardware behavior (control
response, coordinate path, missing devices, stops and disconnects), and UX when
affected (seated VR, spectator/local views, feedback, accessibility and actual flows).
Do not demand unrelated specialties or duplicate blanket review rounds.

## Plan and implement

For substantial work, keep one compact record in the task or an existing plan:

- Problem, intended behavior, accepted requirements, preserved behavior and exclusions.
- Affected subsystem boundaries, risk/reasons, files/assets and writer ownership.
- Small implementation steps, relevant independent perspectives and acceptance criteria.
- Focused checks, final integration/build/scene gates, evidence location and likely duration
  or unknown; escalation and stopping condition.
- Decisions, actual results, omissions and pending human/hardware acceptance.

Resume from that record after compaction; do not restart completed work. Trace state
from its authoritative source through input, simulation, networking, presentation and
hardware only as relevant. Preserve surrounding error handling and safeguards.

For tuning/experiments, state an observable hypothesis, try a small reversible change,
and keep/tune/revert from evidence. A prototype still preserves authority, failsafes
and accepted behavior. Promotion requires the affected normal integration gates.
Do not use simulated hardware or loopback tests as proof of real NOVA/VR/network use.

Human review is separate from automated verification. Honor supplied timing and pause
feedback-dependent work when acceptance is required. Ask about timing only if it
materially changes the work; do not add a routine question to every task. Missing
acceptance stays pending; silence is not approval. Actual platform motion requires
explicitly authorized hardware operation and appropriate operator conditions.

## Delegate and integrate

Use the model policy in `AGENTS.md`; keep the lead's configured model. Give each
agent a clean, bounded packet: goal, relevant requirements/source/diff, explicit
read-only or writer ownership, constraints, checks, expected output and stop condition.
Delegate independent implementation when useful, with disjoint owned files. Reviewers
return findings before seeing one another's conclusions; reconcile using source,
measurements and user intent, not votes.

The lead owns difficult diagnosis, architecture, integration and evidence acceptance.
Reviewers never repair their own findings concurrently with implementers. Review the
actual final diff for correctness, missed requirements, unnecessary abstractions and
unrelated changes. Fix blockers, use compact delta rechecks, and rerun only invalidated
proof. Optional style preferences do not reopen otherwise completed work.

For a substantial verification batch, one designated operator receives exact selected
checks, frozen inputs/build identity, unique output paths, expected results, retry bounds
and cleanup requirements. Transfer exclusive Unity/test/build/player-operation ownership;
other agents may read but must not edit tested inputs or operate that Editor. The
operator reports evidence and failures, without silently changing code, assertions,
selection or retry budgets. Confirm completion and cleanup before ownership returns.

## Verify and close

Use [TESTING.md](TESTING.md). Freeze integrated inputs, run agreed broad checks once,
and repeat only checks invalidated by changes, failures or unresolved concerns.
Reuse historical evidence only for compatible unchanged source/assets/settings/builds;
label its date, identity, scope and limits. A plan or prior report is not a fresh pass.

Report changes and why, actual focused/integrated checks, evidence paths, historical
reuse, omissions/reasons and remaining human/hardware acceptance. Tool success does
not prove runtime behavior; review does not prove testing. If tooling is unavailable,
finish independent work and state specific remaining checks. Stop at agreed scope/proof.
Follow `AGENTS.md` for commit authorization; no automatic push or accumulated dirty-work commit.

## Maintain this workflow

Keep invariants in `AGENTS.md` and procedures here/testing; tool-specific adapters
should point to these rather than duplicate policy. Read only affected plan sections.
Do not copy ProjectI-specific paths, runner APIs, gameplay gates or authorization.

When changing policy, walk through wording-only work, a UI change, input/camera work,
a small RPC, telemetry units and a NOVA conversion. Record expected route, review,
checks, escalation, stopping point and limits. Check an unsafe counterexample: calling
a platform axis change "tuning" must not bypass hardware review. Policy walkthroughs
prove consistency only, not speed, cost savings or simulator quality.
