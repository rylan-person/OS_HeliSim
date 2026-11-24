# Map credentials in Windows builds

Open **Settings → World streaming** in the main menu. Enter either or both access tokens and choose **Save tokens**. A blank field keeps the saved value for that provider. The two account buttons open the respective sign-up pages.

HeliSim saves the pair in Windows Credential Manager for the signed-in Windows user on this computer. The Editor and Windows player share that entry. HeliSim reloads it before the first scene opens. The menu shows which providers have a saved token and keeps both input fields empty. Choose **Forget saved tokens** to remove both credentials. On a fresh account the simulator starts with online terrain and street maps unavailable until their tokens are supplied.

## Building a player

Leave credentials blank in the Cesium ion server, legacy Cesium runtime settings, ion tileset and imagery components, and the Mapbox Resources configuration. Keep URLs, asset IDs and nonsecret Mapbox configuration such as timeouts and cache sizes. Unity includes Resources assets even when Git ignores them.

The player build runs `MapCredentialBuildGuard` automatically. It checks map settings and prefabs in the build's scene and Resources dependencies, processed scenes, the authored token fields, Mapbox configuration and text-based StreamingAssets. A failure identifies the asset path. **HeliSim → Build → Validate Map Credential Exclusion** runs the project check without building.

For verification, run the EditMode fixtures `MapCredentialBuildGuardTests` and `MapServiceTokenTests`. The storage fixture uses a temporary Windows credential entry. Verify the resulting player files before distributing them. Previously released builds retain any credentials embedded when they were built.
