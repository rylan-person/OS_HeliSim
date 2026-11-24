using System.Collections;
using System;
using System.Linq;
using CesiumForUnity;
using HeliSim.FrontEnd;
using Mapbox.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MapServiceTokenTests
{
    private const string MenuPrefab = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private const string TestTargetKey = "HeliSim.MapServiceTokens.TestTarget";

    [UnitySetUp]
    public IEnumerator IsolateCredentials()
    {
        SessionState.SetString(TestTargetKey, "HeliSim/MapServiceTokens/Tests/" + Guid.NewGuid().ToString("N"));
        Assert.That(MapServiceTokens.TryForget(), Is.True);
        yield break;
    }

    [UnityTearDown]
    public IEnumerator RemoveTestCredentials()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        Assert.That(SessionState.GetString(TestTargetKey, ""), Does.StartWith("HeliSim/MapServiceTokens/Tests/"));
        try { Assert.That(MapServiceTokens.TryForget(), Is.True); }
        finally { SessionState.EraseString(TestTargetKey); }
    }

    [Test]
    public void SavedPair_PreservesBlankProviderAndCanBeForgotten()
    {
        Assert.That(MapServiceTokens.TryGetSavedStatus(out bool cesiumSaved, out bool mapboxSaved), Is.True);
        Assert.That(cesiumSaved || mapboxSaved, Is.False);
        Assert.That(MapServiceTokens.TrySave("synthetic-cesium", ""), Is.True);
        Assert.That(MapServiceTokens.HasSavedCesium, Is.True);
        Assert.That(MapServiceTokens.HasSavedMapbox, Is.False);
        Assert.That(MapServiceTokens.TrySave(" synthetic-cesium ", "synthetic-mapbox"), Is.True);
        Assert.That(MapServiceTokens.TrySave("replacement", "  "), Is.True);
        Assert.That(MapServiceTokens.HasSavedCesium, Is.True);
        Assert.That(MapServiceTokens.HasSavedMapbox, Is.True);
        Assert.That(MapServiceTokens.TrySave(new string('x', 3000), "rejected"), Is.False);
        Assert.That(MapServiceTokens.HasSavedCesium, Is.True);
        Assert.That(MapServiceTokens.HasSavedMapbox, Is.True);
        Assert.That(MapServiceTokens.TryForget(), Is.True);
        Assert.That(MapServiceTokens.HasSavedCesium, Is.False);
        Assert.That(MapServiceTokens.HasSavedMapbox, Is.False);
    }

    [UnityTest]
    public IEnumerator SavedPair_ReloadsAcrossPlaySessionsAndForgetPersists()
    {
        Assert.That(MapServiceTokens.TrySave("synthetic-cesium", "synthetic-mapbox"), Is.True);
        Assert.That(MapServiceTokens.TrySave(" replacement-cesium ", " "), Is.True);
        Assert.That(MapServiceTokens.TryImportMissing("must-not-overwrite", "must-not-overwrite"), Is.True);
        Assert.That(MapServiceTokens.TrySave(new string('x', 3000), "rejected"), Is.False);
        yield return new EnterPlayMode();
        Assert.That(MapServiceTokens.HasSavedCesium, Is.True);
        Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.True);
        Assert.That(CesiumIonServer.serverForNewObjects.defaultIonAccessToken == "replacement-cesium", Is.True);
        yield return new ExitPlayMode();
        yield return new EnterPlayMode();
        Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.True);
        Assert.That(CesiumIonServer.serverForNewObjects.defaultIonAccessToken == "replacement-cesium", Is.True);
        Assert.That(MapServiceTokens.TryForget(), Is.True);
        Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.False);
        yield return new ExitPlayMode();
        yield return new EnterPlayMode();
        Assert.That(MapServiceTokens.HasSavedCesium, Is.False);
        Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.False);
        yield return new ExitPlayMode();
    }

    [Test]
    public void StreamingPage_HasMaskedEmptyFieldsAndAccountButtons()
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefab);
        var section = menu.GetComponentInChildren<CesiumSettingsPage>(true)
            .GetComponentInChildren<MapServiceTokenEntry>(true);
        Assert.That(section, Is.Not.Null);
        var controls = new SerializedObject(section);
        foreach (string name in new[] { "cesiumToken", "mapboxToken" })
        {
            var field = (TMP_InputField)controls.FindProperty(name).objectReferenceValue;
            Assert.That(field.contentType, Is.EqualTo(TMP_InputField.ContentType.Password));
            Assert.That(field.text, Is.Empty);
        }
        foreach (string name in new[] { "cesiumSignupButton", "mapboxSignupButton", "applyButton", "forgetButton" })
            Assert.That(controls.FindProperty(name).objectReferenceValue, Is.InstanceOf<Button>());
        Assert.That(MapServiceTokenEntry.CesiumSignupUrl, Is.EqualTo("https://ion.cesium.com/signup"));
        Assert.That(MapServiceTokenEntry.MapboxSignupUrl, Is.EqualTo("https://account.mapbox.com/auth/signup/"));
    }

    [UnityTest]
    public IEnumerator ApplyFromMenu_SavesIndependentProvidersAndForgetClearsBoth()
    {
        yield return new EnterPlayMode();
        var menu = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefab));
        var terrain = new GameObject("Inactive ion tileset");
        terrain.SetActive(false);
        var tileset = terrain.AddComponent<Cesium3DTileset>();
        var bundled = CesiumIonServer.defaultServer;
        string original = bundled.defaultIonAccessToken;
        tileset.ionServer = bundled;
        try
        {
            yield return null;
            var router = menu.GetComponent<MainMenuPageRouter>();
            Assert.That(router.SelectPage("streaming"), Is.True);
            var section = menu.GetComponentInChildren<MapServiceTokenEntry>();
            var input = section.GetComponentsInChildren<TMP_InputField>().Single(f => f.name == "CesiumToken");
            var mapbox = section.GetComponentsInChildren<TMP_InputField>().Single(f => f.name == "MapboxToken");
            var button = section.GetComponentsInChildren<Button>().Single(b => b.name == "ApplyTokens");
            var forget = section.GetComponentsInChildren<Button>().Single(b => b.name == "ForgetTokens");
            var controls = new SerializedObject(section);
            var status = (TMP_Text)controls.FindProperty("statusLabel").objectReferenceValue;
            Assert.That(forget.interactable, Is.False);
            input.text = "  offline-test-token  ";
            button.onClick.Invoke();
            Assert.That(input.text, Is.Empty);
            Assert.That(EffectiveToken(tileset) == "offline-test-token", Is.True);
            Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.False);
            Assert.That(MapServiceTokens.HasSavedCesium, Is.True);
            Assert.That(MapServiceTokens.HasSavedMapbox, Is.False);
            Assert.That(status.text, Does.Contain("Cesium ion saved").And.Contain("No saved Mapbox token"));
            mapbox.text = "offline-mapbox-token";
            button.onClick.Invoke();
            Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.True);
            Assert.That(mapbox.text, Is.Empty);
            Assert.That(MapServiceTokens.HasSavedMapbox, Is.True);
            Assert.That(status.text, Does.Contain("Cesium ion saved").And.Contain("Mapbox saved"));
            Assert.That(status.text, Does.Not.Contain("offline-mapbox-token"));
            Assert.That(EffectiveToken(tileset) == "offline-test-token", Is.True);
            button.onClick.Invoke();
            Assert.That(EffectiveToken(tileset) == "offline-test-token", Is.True);
            input.text = "replacement";
            button.onClick.Invoke();
            Assert.That(EffectiveToken(tileset) == "replacement", Is.True);
            Assert.That(bundled.defaultIonAccessToken == original, Is.True, "The project asset must remain untouched.");
            Assert.That(forget.interactable, Is.True);
            forget.onClick.Invoke();
            Assert.That(MapServiceTokens.HasSavedCesium, Is.False);
            Assert.That(MapServiceTokens.HasSavedMapbox, Is.False);
            Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.False);
            Assert.That(tileset.ionServer, Is.SameAs(bundled));
            Assert.That(forget.interactable, Is.False);
            Assert.That(status.text, Does.Contain("No saved Cesium ion token").And.Contain("No saved Mapbox token"));
        }
        finally
        {
            Object.DestroyImmediate(terrain);
            Object.DestroyImmediate(menu);
        }
        yield return new ExitPlayMode();
        Assert.That(MapboxAccess.HasRuntimeAccessToken, Is.False);
    }

    private static string EffectiveToken(Cesium3DTileset tileset) =>
        string.IsNullOrEmpty(tileset.ionAccessToken) ? tileset.ionServer.defaultIonAccessToken : tileset.ionAccessToken;
}
