using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Content.Shared._Mono.Xenobiology.Chemistry;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
public sealed class ContentSandboxCompatibilityTest
{
    [Test]
    public void SharedXenobiologyAssemblyAvoidsLauncherForbiddenReferences()
    {
        using var stream = File.OpenRead(typeof(ProceduralReagentGeneratorSystem).Assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var forbiddenMethods = new HashSet<string> { "ToHexStringLower", "ToInt32", "ToByte" };

        var methods = metadata.MemberReferences
            .Select(handle => metadata.GetString(metadata.GetMemberReference(handle).Name));
        var types = metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(type => $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}");

        Assert.Multiple(() =>
        {
            Assert.That(methods, Has.None.EqualTo("ToHexStringLower"));
            Assert.That(methods.Count(forbiddenMethods.Contains), Is.Zero);
            Assert.That(types, Has.None.EqualTo("System.Security.Cryptography.MD5"));
        });
    }
}
