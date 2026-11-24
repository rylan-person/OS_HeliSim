using NUnit.Framework;
using UnityEditor.Build;

public sealed class MapCredentialBuildGuardTests
{
    [TestCase("{\"AccessToken\":\"\",\"MemoryCacheSize\":500}")]
    [TestCase("{\"AccessToken\":null,\"DefaultTimeout\":30}")]
    public void MapboxConfigurationAllowsBlankAuthentication(string json)
    {
        Assert.DoesNotThrow(() => MapCredentialBuildGuard.ValidateMapboxConfiguration(json, "synthetic-config"));
    }

    [TestCase("not-json")]
    [TestCase("{}")]
    [TestCase("{\"AccessToken\":{\"hidden\":\"offline-test-secret\"}}")]
    [TestCase("{\"AccessToken\":\"offline-test-secret\"}")]
    [TestCase("{\"AccessToken\":\"offline-test-secret\",\"AccessToken\":\"\"}")]
    [TestCase("{\"Access\\u0054oken\":\"offline-test-secret\",\"AccessToken\":\"\"}")]
    public void MapboxConfigurationRejectsCredentialsOrAmbiguousShapeWithoutDisclosure(string json)
    {
        var error = Assert.Throws<BuildFailedException>(() =>
            MapCredentialBuildGuard.ValidateMapboxConfiguration(json, "synthetic-config"));
        Assert.That(error.Message, Does.Contain("synthetic-config"));
        Assert.That(error.Message, Does.Not.Contain("offline-test-secret"));
    }

    [TestCase("  defaultIonAccessToken: offline-test-secret")]
    [TestCase("  _defaultIonAccessToken: offline-test-secret")]
    [TestCase("  _ionAccessToken: offline-test-secret")]
    [TestCase("{\"AccessToken\":\"offline-test-secret\",\"MemoryCacheSize\":500}")]
    [TestCase("    propertyPath: _ionAccessToken\n    value: offline-test-secret")]
    [TestCase("  _ionAccessToken: |\n    offline-test-secret")]
    public void DetectsCredentialsWithoutReturningTheirValues(string serialized)
    {
        Assert.That(MapCredentialSerialization.ContainsCredential(serialized), Is.True);
    }

    [TestCase("  defaultIonAccessToken: \n  defaultIonAccessTokenId: \n")]
    [TestCase("  _ionAccessToken: ''\n  defaultIonAccessToken: \"\"")]
    [TestCase("{\"AccessToken\":\"\",\"MemoryCacheSize\":500,\"FileCacheSize\":2500}")]
    [TestCase("    propertyPath: _ionAccessToken\n    value: \n    objectReference: {fileID: 0}")]
    [TestCase("  cesiumToken: {fileID: 123}\n  mapboxToken: {fileID: 456}")]
    [TestCase("  defaultIonAccessTokenId: 123\n  _defaultIonAccessTokenID: 456")]
    public void AllowsBlankCredentialsAndPreservesUnrelatedSettings(string serialized)
    {
        Assert.That(MapCredentialSerialization.ContainsCredential(serialized), Is.False);
    }

    [TestCase("  m_Text: pk.eyJvZmZsaW5lIjp0cnVlfQ.fake-signature")]
    [TestCase("  m_Text: eyJvZmZsaW5lIjp0cnVlfQ.fake-payload.fake-signature")]
    public void DetectsProviderTokenLiteralsInOtherSerializedFields(string serialized)
    {
        Assert.That(MapCredentialSerialization.ContainsCredential(serialized), Is.True);
    }
}
