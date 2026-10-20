// Copyright (c) 2017 Mapbox. All rights reserved.
#if UNITY_5_6_OR_NEWER
namespace Mapbox.MapboxSdkCs.UnitTest
{
    using Mapbox.MapMatching;
    using Mapbox.Utils;
    using NUnit.Framework;

    [TestFixture]
    internal class MapMatcherTest
    {
        private const string Path = "/matching/v5/mapbox/driving/0.00000,0.00000;2.00000,1.00000.json";

        private static MapMatchingResource Resource()
        {
            return new MapMatchingResource { Coordinates = new[] { new Vector2d(0, 0), new Vector2d(1, 2) } };
        }

        private static MapMatchingResponse Match(MapMatchingResource resource, string query = "",
            string json = MapboxResponseFixtures.Basic, string path = Path, int status = 200)
        {
            var source = new MapboxResponseFixtureSource();
            MapMatchingResponse result = null;
            int callbacks = 0;
            var handle = new MapMatcher(source, 17).Match(resource, response => { result = response; callbacks++; });
            Assert.AreSame(source, handle);
            Assert.AreEqual(0, callbacks);
            source.Respond(path, query, json, status);
            Assert.IsTrue(handle.IsCompleted);
            Assert.AreEqual(1, callbacks);
            Assert.IsNotNull(result);
            return result;
        }

        private static void AssertSuccess(MapMatchingResponse response)
        {
            Assert.IsFalse(response.HasRequestError);
            Assert.IsFalse(response.HasMatchingError);
            Assert.AreEqual("Ok", response.Code);
            Assert.GreaterOrEqual(response.Tracepoints.Length, 2);
            Assert.GreaterOrEqual(response.Matchings.Length, 1);
            Assert.GreaterOrEqual(response.Matchings[0].Legs.Count, 1);
        }

        [Test]
        public void AsSimpleAsPossible()
        {
            var result = Match(Resource());
            AssertSuccess(result);
            Assert.AreEqual(2, result.Tracepoints.Length);
            Assert.AreEqual(1, result.Tracepoints[1].WaypointIndex);
            Assert.AreEqual(0, result.Tracepoints[1].MatchingsIndex);
            Assert.AreEqual("End", result.Tracepoints[1].Name);
            Assert.AreEqual(new Vector2d(1, 2), result.Tracepoints[1].Location);
            Assert.AreEqual(1, result.Matchings.Length);
            Assert.AreEqual(0.75f, result.Matchings[0].Confidence);
            Assert.AreEqual(25.5f, result.Matchings[0].Weight);
            Assert.AreEqual("routability", result.Matchings[0].WeightName);
            Assert.AreEqual(100, result.Matchings[0].Distance);
            Assert.AreEqual(20, result.Matchings[0].Duration);
            Assert.AreEqual(1, result.Matchings[0].Legs.Count);
            Assert.AreEqual(2, result.Matchings[0].Geometry.Count);
            Assert.AreEqual(new Vector2d(1, 2), result.Matchings[0].Geometry[1]);
            Assert.IsEmpty(result.Matchings[0].Legs[0].Steps);
        }

        [TestCase(Profile.MapboxWalking, "walking")]
        [TestCase(Profile.MapboxCycling, "cycling")]
        [TestCase(Profile.MapboxDrivingTraffic, "driving-traffic")]
        [TestCase(Profile.MapboxDriving, "driving")]
        public void Profiles(Profile profile, string expectedProfile)
        {
            var resource = Resource();
            resource.Profile = profile;
            var result = Match(resource, path: "/matching/v5/mapbox/" + expectedProfile + "/0.00000,0.00000;2.00000,1.00000.json");
            AssertSuccess(result);
            Assert.AreEqual(20, result.Matchings[0].Duration);
        }

        [Test]
        public void NoSegment()
        {
            var result = Match(Resource(), json: MapboxResponseFixtures.NoSegment);
            Assert.IsFalse(result.HasRequestError);
            Assert.IsTrue(result.HasMatchingError);
            Assert.AreEqual("NoSegment", result.Code);
            Assert.AreEqual("Could not find a matching segment for input coordinates", result.Message);
            Assert.IsNull(result.Tracepoints);
            Assert.IsEmpty(result.Matchings);
        }

        [Test]
        public void Radiuses()
        {
            var resource = Resource();
            resource.Radiuses = new uint[] { 10, 30 };
            var result = Match(resource, "radiuses=10;30");
            AssertSuccess(result);
            Assert.AreEqual(2, result.Tracepoints.Length);
            Assert.AreEqual(25.5f, result.Matchings[0].Weight);
        }

        [Test]
        public void AlternativesWithSteps()
        {
            var resource = Resource();
            resource.Radiuses = new uint[] { 10, 30 };
            resource.Steps = true;
            var result = Match(resource, "radiuses=10;30&steps=true", MapboxResponseFixtures.English);
            AssertSuccess(result);
            Assert.GreaterOrEqual(result.Tracepoints[0].AlternativesCount, 2);
            Assert.GreaterOrEqual(result.Tracepoints[1].AlternativesCount, 19);
            var steps = result.Matchings[0].Legs[0].Steps;
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(1, steps[0].Intersections.Count);
            CollectionAssert.AreEqual(new[] { 60 }, steps[0].Intersections[0].Bearings);
            CollectionAssert.AreEqual(new[] { true }, steps[0].Intersections[0].Entry);
            Assert.AreEqual(new Vector2d(1, 2), steps[1].Maneuver.Location);
            Assert.AreEqual(new Vector2d(1, 2), steps[0].Geometry[1]);
        }

        [Test]
        public void OverviewSimplified()
        {
            var resource = Resource();
            resource.Overview = Overview.Simplified;
            var result = Match(resource, "overview=simplified");
            AssertSuccess(result);
            Assert.AreEqual(2, result.Matchings[0].Geometry.Count);
        }

        [Test]
        public void OverviewFull()
        {
            var resource = Resource();
            resource.Overview = Overview.Full;
            var result = Match(resource, "overview=full", MapboxResponseFixtures.Full);
            AssertSuccess(result);
            Assert.AreEqual(3, result.Matchings[0].Geometry.Count);
            Assert.AreEqual(new Vector2d(0.5, 1), result.Matchings[0].Geometry[1]);
            Assert.AreEqual(new Vector2d(1, 2), result.Matchings[0].Geometry[2]);
        }

        [Test]
        public void Timestamps()
        {
            var resource = Resource();
            resource.Timestamps = new long[] { 946684800, 946684980 };
            AssertSuccess(Match(resource, "timestamps=946684800;946684980"));
        }

        [Test]
        public void Annotation()
        {
            var resource = Resource();
            resource.Profile = Profile.MapboxDrivingTraffic;
            resource.Overview = Overview.Full;
            resource.Annotations = Annotations.Distance | Annotations.Duration | Annotations.Speed | Annotations.Congestion;
            var result = Match(resource, "overview=full&annotations=duration,distance,speed,congestion",
                MapboxResponseFixtures.Full, "/matching/v5/mapbox/driving-traffic/0.00000,0.00000;2.00000,1.00000.json");
            AssertSuccess(result);
            AssertAnnotations(result);
        }

        private static void AssertAnnotations(MapMatchingResponse result)
        {
            var annotation = result.Matchings[0].Legs[0].Annotation;
            CollectionAssert.AreEqual(new double[] { 40, 60 }, annotation.Distance);
            CollectionAssert.AreEqual(new double[] { 8, 12 }, annotation.Duration);
            CollectionAssert.AreEqual(new[] { "5", "5" }, annotation.Speed);
            CollectionAssert.AreEqual(new[] { "low", "moderate" }, annotation.Congestion);
        }

        [Test]
        public void Tidy()
        {
            var resource = Resource();
            resource.Coordinates = new[] { new Vector2d(0, 0), new Vector2d(0.5, 1), new Vector2d(1, 2) };
            resource.Tidy = true;
            var result = Match(resource, "tidy=true", MapboxResponseFixtures.Tidy,
                "/matching/v5/mapbox/driving/0.00000,0.00000;1.00000,0.50000;2.00000,1.00000.json");
            AssertSuccess(result);
            Assert.AreEqual(3, result.Tracepoints.Length);
            Assert.IsNull(result.Tracepoints[1]);
            Assert.IsNotNull(result.Tracepoints[0]);
            Assert.AreEqual(1, result.Tracepoints[2].WaypointIndex);
        }

        [Test]
        public void LanguageEnglish()
        {
            var resource = Resource();
            resource.Steps = true;
            var result = Match(resource, "steps=true", MapboxResponseFixtures.English);
            AssertSuccess(result);
            Assert.AreEqual("Head northeast on Test road", result.Matchings[0].Legs[0].Steps[0].Maneuver.Instruction);
            Assert.AreEqual("You have arrived at your destination", result.Matchings[0].Legs[0].Steps[1].Maneuver.Instruction);
        }

        [Test]
        public void LanguageGerman()
        {
            var resource = Resource();
            resource.Steps = true;
            resource.Language = InstructionLanguages.German;
            var result = Match(resource, "steps=true&language=de", MapboxResponseFixtures.German);
            AssertSuccess(result);
            Assert.AreEqual("Fahren Sie Richtung Nordosten auf Teststraße", result.Matchings[0].Legs[0].Steps[0].Maneuver.Instruction);
            Assert.AreEqual("Sie haben Ihr Ziel erreicht", result.Matchings[0].Legs[0].Steps[1].Maneuver.Instruction);
        }

        [Test]
        public void AllParameters()
        {
            var resource = Resource();
            resource.Profile = Profile.MapboxDrivingTraffic;
            resource.Geometries = Geometries.Polyline;
            resource.Radiuses = new uint[] { 10, 30 };
            resource.Steps = true;
            resource.Overview = Overview.Full;
            resource.Timestamps = new long[] { 946684800, 946684980 };
            resource.Annotations = Annotations.Distance | Annotations.Duration | Annotations.Speed | Annotations.Congestion;
            resource.Tidy = true;
            resource.Language = InstructionLanguages.German;
            var result = Match(resource,
                "geometries=polyline&radiuses=10;30&steps=true&overview=full&timestamps=946684800;946684980&annotations=duration,distance,speed,congestion&tidy=true&language=de",
                MapboxResponseFixtures.German, "/matching/v5/mapbox/driving-traffic/0.00000,0.00000;2.00000,1.00000.json");
            AssertSuccess(result);
            AssertAnnotations(result);
            Assert.AreEqual(3, result.Matchings[0].Geometry.Count);
            Assert.AreEqual("Sie haben Ihr Ziel erreicht", result.Matchings[0].Legs[0].Steps[1].Maneuver.Instruction);
        }

        [Test]
        public void Polyline6RequestOption()
        {
            // Existing converter decodes precision 5 only. Do not mislabel a p5
            // response as p6 or claim this test proves p6 response coordinates.
            var resource = Resource();
            resource.Geometries = Geometries.Polyline6;
            var result = Match(resource, "geometries=polyline6", MapboxResponseFixtures.NoSegment);
            Assert.AreEqual("NoSegment", result.Code);
            Assert.IsTrue(result.HasMatchingError);
        }

        [Test]
        public void CoordinatesNull()
        {
            var resource = new MapMatchingResource();
            var source = new MapboxResponseFixtureSource();
            Assert.Throws<System.Exception>(() => resource.Coordinates = null);
            Assert.Throws<System.Exception>(() => new MapMatcher(source, 17).Match(resource, response => { }));
            Assert.AreEqual(0, source.RequestCount);
        }

        [Test]
        public void InvalidCoordinate()
        {
            var resource = Resource();
            resource.Coordinates = new[] { new Vector2d(100, 2), new Vector2d(101, 3) };
            var result = Match(resource, json: MapboxResponseFixtures.InvalidInput,
                path: "/matching/v5/mapbox/driving/2.00000,100.00000;3.00000,101.00000.json", status: 422);
            Assert.IsTrue(result.HasRequestError);
            Assert.IsTrue(result.HasMatchingError);
            Assert.AreEqual("InvalidInput", result.Code);
            Assert.AreEqual("Coordinate is invalid: 2,100", result.Message);
            Assert.AreEqual(1, result.RequestExceptions.Count);
            Assert.AreEqual("Fixture HTTP 422", result.RequestExceptions[0].Message);
        }

        [Test]
        public void MalformedResponseIsRejected()
        {
            var source = new MapboxResponseFixtureSource();
            int callbacks = 0;
            new MapMatcher(source, 17).Match(Resource(), response => callbacks++);
            Assert.Throws<Mapbox.Json.JsonSerializationException>(() => source.Respond(Path, "", "{"));
            Assert.AreEqual(0, callbacks);
        }
    }
}
#endif
