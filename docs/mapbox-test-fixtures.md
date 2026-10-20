# Mapbox test fixtures

The vendored SDK tests originally expected four missing Resources assets. These
files are restored byte-for-byte from the
[Mapbox Unity SDK v2.0.0 tree](https://github.com/mapbox/mapbox-unity-sdk/tree/v2.0.0/sdkproject/Assets/Mapbox/Unity/Tests/Resources),
under `sdkproject/Assets/Mapbox/Unity/Tests/Resources/`:

| File | Git blob SHA-1 |
|---|---|
| `ChearRulerCs_fixtures.json` | `b23e5660a94d5cf22e1f4b5113c658de01011bc8` |
| `ProbeExtractorCs_fixture_probes.csv` | `70b4f3f74f8130879a1fedf514fadb7ddf359de4` |
| `ProbeExtractorCs_fixture_trace-foot.csv` | `07337dad645149f01d26d7c1594707e824aefb6f` |
| `ProbeExtractorCs_fixture_trace.csv` | `051dd50f94297fdddd534b74dc77ce6f000eed25` |

Their local locations are `Assets/Mapbox/Core/cheap-ruler-cs/Tests/Editor/Fixtures/`
and `Assets/Mapbox/Core/probe-extractor-cs/Tests/Editor/Fixtures/`. Tests load them
through AssetDatabase so test data stays outside player Resources. Original
numerical assertions remain intact; the spelling `ChearRuler` is upstream's.

MapMatching and TileJSON tests use small synthetic protocol fixtures, not recorded
live service results. They check outgoing options and decode explicit response
fields. FileSource tests separately exercise real Unity HTTP transport through a
temporary loopback server. These checks do not establish public service uptime,
token validity, current routing results or current tileset availability.

The old DNS-message assertion is replaced by a controlled connection failure;
DNS-specific behavior is not verified. Two existing SDK limitations found during
inspection are unchanged: MapMatching response geometry always decodes with
precision 5, even when a request asks for polyline6, and the TileJSON timestamp
helper passes integer values through its ticks overload. Tests distinguish
polyline6 request formatting from geometry decoding, and check raw timestamp
values and conversion presence without claiming correct Unix-millisecond dates.

## License for restored upstream data

From upstream `LICENSE`, blob `7e24a4595f4f06785e9de7ae71b57815b63694d9`:

The MIT License (MIT)

Mapbox Unity SDK copyright (c) 2016-2017 Mapbox.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
