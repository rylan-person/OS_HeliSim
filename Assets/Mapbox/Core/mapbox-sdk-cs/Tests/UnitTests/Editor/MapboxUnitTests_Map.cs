//-----------------------------------------------------------------------
// <copyright file="MapTest.cs" company="Mapbox">
//     Copyright (c) 2016 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

// TODO: figure out how run tests outside of Unity with .NET framework, something like '#if !UNITY'
#if UNITY_5_6_OR_NEWER

namespace Mapbox.MapboxSdkCs.UnitTest
{

	using Mapbox.Map;
	using Mapbox.Platform;
	using Mapbox.Utils;
	using NUnit.Framework;
#if UNITY_5_6_OR_NEWER
	using System.Collections;
	using UnityEngine.TestTools;
#endif


	[TestFixture]
	internal class MapTest
	{

		private FileSource _fs;


		[SetUp]
		public void SetUp()
		{
#if UNITY_5_6_OR_NEWER
			_fs = new FileSource(Unity.MapboxAccess.Instance.Configuration.GetMapsSkuToken, Unity.MapboxAccess.Instance.Configuration.AccessToken);
#else
			// when run outside of Unity FileSource gets the access token from environment variable 'MAPBOX_ACCESS_TOKEN'
			_fs = new FileSource();
#endif
		}



#if UNITY_5_6_OR_NEWER
		[UnityTest]
		public IEnumerator World()
#else
		[Test]
		public void World()
#endif
		{
			var map = new Map<VectorTile>(_fs);

			map.Vector2dBounds = Vector2dBounds.World();
			map.Zoom = 3;

			var mapObserver = new Utils.VectorMapObserver();
			map.Subscribe(mapObserver);
			map.Update();

#if UNITY_5_6_OR_NEWER
			IEnumerator enumerator = _fs.WaitForAllRequests();
			while (enumerator.MoveNext()) { yield return null; }
#else
			_fs.WaitForAllRequests();
#endif

			Assert.AreEqual(64, mapObserver.Tiles.Count);

			map.Unsubscribe(mapObserver);
		}



#if UNITY_5_6_OR_NEWER
		[UnityTest]
		public IEnumerator RasterHelsinki()
#else
		[Test]
		public void RasterHelsinki()
#endif
		{
			var map = new Map<RasterTile>(_fs);

			map.Center = new Vector2d(60.163200, 24.937700);
			map.Zoom = 13;

			var mapObserver = new Utils.RasterMapObserver();
			map.Subscribe(mapObserver);
			map.Update();

#if UNITY_5_6_OR_NEWER
			IEnumerator enumerator = _fs.WaitForAllRequests();
			while (enumerator.MoveNext()) { yield return null; }
#else
			_fs.WaitForAllRequests();
#endif

			Assert.AreEqual(1, mapObserver.Tiles.Count);
			Assert.IsNotNull(mapObserver.Tiles[0]);

			map.Unsubscribe(mapObserver);
		}



		[Test]
		public void ChangeTilesetId()
		{
			var source = new MapboxResponseFixtureSource();
			var map = new Map<ClassicRasterTile>(source);
			var observer = new Utils.ClassicRasterMapObserver();
			map.Subscribe(observer);
			map.Center = new Vector2d(60.163200, 24.937700);
			map.Zoom = 13;
			map.TilesetId = "invalid";
			map.Update();
			const string tilePath = "/13/4663/2371.png";
			source.RespondTile("/v4/invalid" + tilePath, new byte[0], 1, 404);
			Assert.AreEqual(0, observer.Tiles.Count);

			byte[] terrain = MapboxResponseFixtureSource.MakeRasterFixture(1);
			map.TilesetId = "mapbox.terrain-rgb";
			map.Update();
			source.RespondTile("/v4/mapbox.terrain-rgb" + tilePath, terrain, 2);
			Assert.AreEqual(1, observer.Tiles.Count);
			CollectionAssert.AreEqual(terrain, observer.Tiles[0]);

			byte[] satellite = MapboxResponseFixtureSource.MakeRasterFixture(2);
			map.TilesetId = null;
			map.Update();
			source.RespondTile("/v4/mapbox.satellite" + tilePath, satellite, 3);
			Assert.AreEqual(2, observer.Tiles.Count);
			CollectionAssert.AreEqual(satellite, observer.Tiles[1]);
			CollectionAssert.AreNotEqual(observer.Tiles[0], observer.Tiles[1]);
			map.Unsubscribe(observer);
		}



		[Test]
		public void SetVector2dBoundsZoom()
		{
			var map1 = new Map<RasterTile>(_fs);
			var map2 = new Map<RasterTile>(_fs);

			map1.Zoom = 3;
			map1.Vector2dBounds = Vector2dBounds.World();

			map2.SetVector2dBoundsZoom(Vector2dBounds.World(), 3);

			Assert.AreEqual(map1.Tiles.Count, map2.Tiles.Count);
		}



		[Test]
		public void TileMax()
		{
			var map = new Map<RasterTile>(_fs);

			map.SetVector2dBoundsZoom(Vector2dBounds.World(), 2);
			map.Update();
			Assert.Less(map.Tiles.Count, Map<RasterTile>.TileMax);

			// Should stay the same, ignore requests.
			map.SetVector2dBoundsZoom(Vector2dBounds.World(), 5);
			map.Update();
			Assert.AreEqual(16, map.Tiles.Count);
		}



		[Test]
		public void Zoom()
		{
			var map = new Map<RasterTile>(_fs);

			map.Zoom = 50;
			Assert.AreEqual(20, map.Zoom);

			map.Zoom = -50;
			Assert.AreEqual(0, map.Zoom);
		}
	}
}

#endif
