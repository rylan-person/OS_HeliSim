# Mapbox verification repairs — 2026-10-20

The requested multiplayer dependency and menu commits are `5c6c3e3` and
`4c65175`, respectively. Continue with the Mapbox failures exposed by the menu
verification, preserving unrelated `docs/controls-debugging.md`.

## Evidence and scope

The previous integrated run recorded 24 Mapbox failures in
`TestResults/main-menu-20261020/integrated-failures.txt`: missing CheapRuler and
ProbeExtractor data, one inconsistent geocoding URL expectation, an uncontrolled
HTTP 403 endpoint, and service-dependent MapMatching/TileJSON snapshots. This is
SDK verification work, with no changes to helicopter physics, networking roles,
NOVA, XR, map scenes, credentials or package versions planned.

Bounded medium risk: independent SDK/network test review before and after the
change covers transport cleanup, response decoding and test validity. The lead
owns investigation, integration and all Editor operations. Transport and response
fixture implementers own disjoint Editor test files.

## Implementation and proof

1. Restore the four original data blobs from Mapbox Unity SDK tag v2.0.0, keeping
   them under Editor folders and loading by explicit asset paths. Preserve the
   original numerical checks and document provenance.
2. Correct the isolated desktop geocoding expectation. Exercise real FileSource
   transport against a bounded loopback peer for success, concurrency, cancel,
   connection failure, HTTP 403 and empty completion.
3. Replace mutable live response snapshots with explicit protocol fixtures and
   independent outgoing URL assertions. Preserve option, converter, schema and
   error coverage; no ignored or skipped tests. These checks do not certify the
   availability or response contents of the public service.
4. Import through the connected pinned Editor, confirm discovery and run affected
   Mapbox cases. Review the integrated diff independently, then run the complete
   discovered EditMode suite once. Retain XML, source identity and cleanup evidence
   under `TestResults/mapbox-20261020/`. Repeat only invalidated proof.

Test-only changes require compilation and actual NUnit XML validation; they do
not invalidate the previous menu player build. Do not run NOVA output or change
the user's saved authoring scene. Generate new metadata through Unity. Stop when
the authorized fixes and applicable proof are complete; Mapbox changes remain
available for review without assuming another commit or push request.

## Results

- Restored all four original data blobs, independently verified their Git hashes,
  and generated new metadata through the pinned connected Editor. Original
  numerical checks remain unchanged. Added provenance and license in
  `docs/mapbox-test-fixtures.md`.
- Corrected the desktop bbox URL expectation. Replaced six uncontrolled transport
  tests with six real loopback cases, including explicit token/SKU and existing
  query preservation assertions. Replaced live response snapshots with 20
  MapMatcher and six TileJSON cases, including negative JSON checks.
- Independent network/SDK review found a missing credential-query assertion;
  repaired it and obtained a clean delta review. No runtime source, scene,
  package or settings changes were needed.
- First focused run: 112 cases, 92 passed, 20 failed. The new expected URLs omitted
  the SDK's fixed five-decimal formatting, and malformed JSON expected the wrong
  exception class. Corrected those test mistakes against source and actual XML;
  response delta rerun passed 26/26.
- Final integrated run: **371/371 passed, zero failed or skipped**: 259 project
  cases and 112 Mapbox cases. XML is
  `TestResults/mapbox-20261020/full-editmode.xml`; discovery, selected names,
  tracked diff, 22 source/metadata hashes and selection validation are alongside
  it. NUnit omits Unity's `GeneratedTestCaseN` suffixes for four colliding byte-array
  parameter display names; normalized case-name multisets match and all 371
  result IDs are unique. Frozen source hashes remained unchanged during the run.
- Verified terminal cleanup: no active runner, Play mode, compilation or import;
  no temporary integration folders; saved `MainMenu.unity` restored clean. The
  Console at test completion contained the expected duplicate-page-ID negative test log.
  `git diff --check` passed. Earlier incidental vendor script-reload exceptions
  were outside test execution and did not recur in the final run.
- Test execution cleared the two preloaded XR asset references in PlayerSettings.
  Restored the original GUID/local-ID pair through `PlayerSettings.SetPreloadedAssets`
  and persisted through Unity's Save Project menu. Verified no settings, XR,
  RenderTexture, network-prefab or font diff remained. The first cleanup lookup
  encountered a null imported subasset before making changes; filtering null
  entries allowed both original references to be restored. Individual asset-save
  APIs did not persist PlayerSettings; direct serialization rejected persistent
  objects without altering the file. Save Project completed the cleanup.
- The previous Windows menu build remains compatible: these changes are confined
  to Editor tests/data and documentation, so no new player build or hardware run
  was needed. External service authentication/availability, DNS-specific behavior,
  precision6 response geometry and exact TileJSON UTC conversion are not proved
  by these repairs; see the fixture documentation for existing SDK limitations.
- Source identity: menu commit `4c65175` plus the recorded Mapbox test diff/new
  fixtures. Mapbox repairs remain uncommitted; the unrelated controls-debugging
  document is preserved. No push performed.
