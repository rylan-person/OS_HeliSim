---
applyTo: "**"
---

# HeliSim Unity project instructions

Follow [AGENTS.md](../../AGENTS.md) for project boundaries, hardware safeguards, model
selection and delivery. Use [the shared workflow](../../docs/AGENT_WORKFLOW.md) for
substantial work and [TESTING.md](../../docs/TESTING.md) before checks/Editor operations.
These are the canonical policies; avoid duplicating conflicting tool-specific rules.

- Keep MonoBehaviours focused and follow nearby C# style. Prefer `[SerializeField] private`
  for new Inspector configuration; preserve existing serialized compatibility.
- Validate required references with useful errors; optional hardware must fail gracefully.
- Avoid per-frame allocations, LINQ, repeated component lookups and expensive searches in
  Update, FixedUpdate and networking hot paths. Cache references where appropriate.
- Explain authority, host/client behavior and interpolation implications for networked
  physics changes. Keep spectator/camera-only state separate from helicopter authority.
- Preserve seated OpenXR and desktop rendering. Use profiling evidence for performance claims.
- Required scene/prefab/settings changes are part of authorized scope. Preserve GUIDs and
  pair assets with their Editor-generated metas; do not silently upgrade packages or Editor.
- Report actual compilation/testing separately from proposed manual checks. Reviews do not
  replace runtime or hardware acceptance; documentation changes need document checks only.
