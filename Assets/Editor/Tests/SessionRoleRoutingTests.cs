#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using Sandplay.Core;

public class SessionRoleRoutingTests
{
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(4097)]
    [TestCase(int.MaxValue)]
    public void RelayControlRejectsInvalidLengthsBeforeReadingBody(int length)
    {
        using var stream = new System.IO.MemoryStream(BitConverter.GetBytes(length));
        Assert.Throws<System.IO.IOException>(() => RelayCompatibility.ReadControlResponse(stream));
        Assert.AreEqual(4, stream.Position);
    }

    [Test]
    public void RelayControlRejectsTruncationAndPreservesValidPayload()
    {
        using var truncated = new System.IO.MemoryStream(new byte[] { 2, 0, 0, 0, 7 });
        Assert.Throws<System.IO.EndOfStreamException>(() => RelayCompatibility.ReadControlResponse(truncated));
        using var valid = new System.IO.MemoryStream(new byte[] { 2, 0, 0, 0, 7, 9 });
        CollectionAssert.AreEqual(new byte[] { 7, 9 }, RelayCompatibility.ReadControlResponse(valid));
    }

    [Test]
    public void RoleMessagePreservesRecipientAndDisplayName()
    {
        string token = Guid.NewGuid().ToString("N");
        var payload = NetworkBootstrapper.WriteSessionRole(PlayerRole.Patient, token, "Client 测试");
        Assert.IsTrue(NetworkBootstrapper.TryReadSessionRole(payload, out var role, out var recipient, out var name));
        Assert.AreEqual(PlayerRole.Patient, role);
        Assert.AreEqual(token, recipient);
        Assert.AreEqual("Client 测试", name);
    }

    [Test]
    public void UntargetedAndMalformedRolesAreRejected()
    {
        Assert.IsFalse(NetworkBootstrapper.TryReadSessionRole(null, out _, out _, out _));
        Assert.IsFalse(NetworkBootstrapper.TryReadSessionRole(new byte[] { 0 }, out _, out _, out _));
        Assert.IsFalse(NetworkBootstrapper.TryReadSessionRole(
            NetworkBootstrapper.WriteSessionRole(PlayerRole.Patient, "not-a-token", "Client"), out _, out _, out _));
    }
}
#endif
