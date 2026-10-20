using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

public class MultiplayerRoleApprovalTests
{
    // Missing/empty payloads retain the legacy Player default. Present payloads
    // must contain exactly one supported role byte.
    [TestCase(null, true, ClientRole.Player)]
    [TestCase(new byte[0], true, ClientRole.Player)]
    [TestCase(new byte[] { 0 }, true, ClientRole.Player)]
    [TestCase(new byte[] { 1 }, true, ClientRole.Spectator)]
    [TestCase(new byte[] { 2 }, false, ClientRole.Player)]
    [TestCase(new byte[] { 255 }, false, ClientRole.Player)]
    [TestCase(new byte[] { 0, 0 }, false, ClientRole.Player)]
    [TestCase(new byte[] { 1, 0 }, false, ClientRole.Player)]
    public void RolePayloadAcceptsOnlyLegacyEmptyOrOneSupportedByte(byte[] payload, bool expected, ClientRole expectedRole)
    {
        var parse = typeof(CustomConnectionManager).GetMethod("TryParseRole", BindingFlags.Public | BindingFlags.Static);
        Assert.That(parse, Is.Not.Null, "Connection approval must expose and use strict role parsing.");
        object[] arguments = { payload, ClientRole.Player };
        Assert.That((bool)parse.Invoke(null, arguments), Is.EqualTo(expected));
        Assert.That((ClientRole)arguments[1], Is.EqualTo(expectedRole));
    }

    [Test]
    public void ApprovalRejectsMalformedRoleBeforeInspectingOrSpawningPrefabs()
    {
        var go = new GameObject("Approval test");
        try
        {
            var manager = go.AddComponent<CustomConnectionManager>();
            var response = new NetworkManager.ConnectionApprovalResponse();
            typeof(CustomConnectionManager).GetMethod("ApprovalCheck", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[]
                {
                    new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 42, Payload = new byte[] { 2 } },
                    response
                });
            Assert.That(response.Approved, Is.False);
            Assert.That(response.CreatePlayerObject, Is.False);
            Assert.That(response.Reason, Is.Not.Empty);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void MissingPrefabIsRejectedWithReadableReason()
    {
        var go = new GameObject("Missing prefab test");
        try
        {
            var manager = go.AddComponent<CustomConnectionManager>();
            var validate = typeof(CustomConnectionManager).GetMethod("TryValidateSpawnConfiguration");
            Assert.That(validate, Is.Not.Null);
            object[] arguments = { ClientRole.Player, null };
            Assert.That((bool)validate.Invoke(manager, arguments), Is.False);
            Assert.That(arguments[1] as string, Is.Not.Empty);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SpawnedSpectatorRoleIgnoresStalePlayerUiSelection()
    {
        var go = new GameObject("Spectator role test");
        var originalSelection = RoleSelectionUI.SelectedRole;
        try
        {
            var playerObject = go.AddComponent<NetworkObject>();
            go.AddComponent<ClientRoleMarker>();
            RoleSelectionUI.SelectedRole = ClientRole.Player;
            Assert.That(CustomConnectionManager.TryGetSpawnedRole(playerObject, out var role), Is.True);
            Assert.That(role, Is.EqualTo(ClientRole.Spectator));
        }
        finally
        {
            RoleSelectionUI.SelectedRole = originalSelection;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void UnmarkedNetworkObjectCannotBeClassifiedAsSpectator()
    {
        var go = new GameObject("Unmarked role test");
        try
        {
            Assert.That(CustomConnectionManager.TryGetSpawnedRole(go.AddComponent<NetworkObject>(), out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
