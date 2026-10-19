# Workflow adoption review — 20 October 2026

## Scope and sources

User request: compare ProjectI's agent guidance and AI workflow information, transfer
relevant practices and improve HeliSim's workflow. This is a documentation/configuration
task; existing runtime, scene, prefab, settings and test changes are outside its scope.

Read ProjectI's `AGENTS.md`, `Docs/AGENT_WORKFLOW.md`, `Docs/WORKFLOW_MAINTENANCE.md`,
both local skill entry points (`projecti-slice`, `projecti-prompt`), the delegated
verification reference, and relevant `TESTING.md`/`SETUP.md` sections. Source checkout:
`E:\IndieGameProject\ProjectI`. ProjectI was read only and remains independent.

Compared HeliSim's root guidance, both GitHub agent/instruction adapters, ignore rules,
version/package pins, current test layout, multiplayer fixture/helper and relevant
local implementation status. Historical status is context, not current proof.

## Transfer decisions

| Practice | HeliSim adaptation |
|---|---|
| Intent-first routing | Reviews/plans do not silently become implementation or Unity operations; documentation uses proportionate proof. |
| Risk-based review | Independent pre/post review for consequential changes; explicit NOVA, physics, XR, NGO and telemetry boundaries. Small wording fixes stay lightweight. |
| Compact task record | Scope, ownership, acceptance, checks, evidence and stopping point in one existing plan/task record. |
| Independent bounded agents | Read-only reviewers, disjoint implementation ownership, explicit model/effort and lead integration. |
| Serialized verification | One Editor operator, positive discovery, frozen inputs, unique reports, timeout/cleanup and bounded retries. |
| Evidence limits | Separate tests, Editor observations, build provenance and human/hardware acceptance; label historical reuse. |
| Policy maintenance | Walk through representative safe/unsafe cases; no unsupported speed/cost claims. |
| Thin tool adapters | GitHub guidance points to canonical policy and retains useful Unity coding guidance. |

Retained HeliSim's existing NOVA/VR/input/network/telemetry safeguards and model choices.
Did not import ProjectI gameplay architecture, paths, fixture catalogue, custom runner,
campaign gates, mandatory routine playtest timing question, lead Ultra setting, or
standing local-commit authorization. Existing installed skills remain available;
no new skill, global configuration, package, service or runtime harness is needed.

`AGENTS.md` and new shared documents were ignored by Git; `.github/` also had duplicate
ignore entries. Narrowed the documentation rules to expose just shared workflow files,
made the GitHub adapters eligible for tracking, and ignored new `TestResults/` output.
Historical untracked plans and existing local evidence remain ignored.

The structure follows [OpenAI's repository-rule guidance](https://developers.openai.com/blog/custom-code-review-rules-for-codex):
keep consequential project invariants explicit and scope review rules to useful findings.
The specific review tiers here are a HeliSim policy choice, not a measured universal optimum.

## Read-only policy walkthroughs

These check instruction consistency; they do not run tests or establish runtime quality.

| Example | Route and review | Proof, escalation and stopping point |
|---|---|---|
| Fix a documentation typo | Low; lead inspects affected text/links. | Document diff/link checks; stop after correction. Escalate if policy meaning changes. |
| Adjust a dashboard label/layout | Medium when rendered behavior changes; relevant UX/technical pre/post review. | Relevant Editor tests, actual rendered panel/reassignment/full-screen flow; HMD acceptance if affected. Escalate ownership/XR changes. |
| Change input binding/device selection | Medium only for bounded presentation; identity/persistence/control behavior increases risk. | Relevant rebinding tests, missing-device/keyboard fallback, owning flow; actual device acceptance separately. Stop at affected proof, never assume attached HOTAS. |
| Change follow-camera target | Medium if local and bounded; high if ownership/lifecycle or XR hierarchy changes. | Relevant camera tests, host/remote/spectator and target destruction checks as affected. Escalate new shared-state behavior. |
| Change one RPC or role approval | High despite small diff; independent technical and affected simulation/UX review before/after. | Exact discovered role/lifecycle/integration checks, affected build and client-role matrix; stop with separate-network acceptance explicit. |
| Change telemetry units | High schema/meaning boundary; independent technical and affected data/simulation review. | Field/unit/timestamp compatibility and recorded samples; prefer a new field, document schema; no relabelled historical pass. |
| Reverse a NOVA axis sign | High even if called tuning; trace the full coordinate path, technical plus independent simulation/hardware review. | Pure conversion/failsafe/disconnect proof first; no automatic physical movement. Hardware operation requires explicit authorization; pending acceptance blocks claims of hardware validation. |
| Add a UI button that resets the platform | High hardware/lifecycle boundary despite UI label. | Review command gating, active-run/connection checks, stop/reset behavior and rendered feedback; never classify solely as presentation. |

## Review and validation record

An independent read-only technical reviewer checked the transfer plan, emphasizing
scope, small high-risk changes, hardware authorization, Editor ownership/recovery,
accurate integration-test mode and conflicting adapters. Those points were incorporated.
Final independent review found no blocking policy contradictions or safety/scope gaps.
Document checks passed: 13 local Markdown links resolved, all seven task-owned files
were eligible for Git, and no trailing whitespace was found. Historical plan/local
evidence and new test-output paths remained ignored. No runtime/asset files were edited
by this task; unrelated in-progress changes were not reviewed or revalidated.

No Unity operation, build, runtime test or hardware test is needed for these prose/config
changes. This adoption does not resolve or revalidate ongoing multiplayer work and
does not establish faster execution, lower cost or simulator/hardware correctness.

## Documentation tracking follow-up

The user subsequently authorized committing/pushing the workflow changes and keeping
documentation in the repository. Removed the remaining blanket `docs/` exclusions so
existing implementation plans and future documentation are eligible for tracking.
Included the two existing in-flight-menu plans as documentation; their recorded status
is historical and does not establish fresh runtime verification. Local execution
artifacts under `.superpowers/sdd/` and generated `TestResults/` remain ignored.
This supersedes the adoption-time choice to keep untracked historical plans ignored.
