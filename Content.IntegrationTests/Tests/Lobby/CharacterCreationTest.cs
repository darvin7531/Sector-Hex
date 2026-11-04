using Content.Client.Lobby;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Lobby
{
    [TestFixture]
    [TestOf(typeof(ClientPreferencesManager))]
    [TestOf(typeof(ServerPreferencesManager))]
    public sealed class CharacterCreationTest
    {
        [TestCase("Vox", null, null, true)]
        [TestCase("Human", "John Doe", "John Doe", true)]
        [TestCase("Human", "ÀÖØöøÿĀſ", "ÀÖØöøÿĀſ", true)]
        [TestCase("Human", "Ёж Семёнов", "Ёж Семёнов", true)]
        [TestCase("Human", "John<>{}_/\\\"!42 O'Neil-Ёж", "John42 O'Neil-Ёж", true)]
        [TestCase("IPC", "Unit_42!", "Unit_42!", true)]
        [TestCase("Human", "John_Doe!", "John_Doe!", false)]
        public async Task RestrictedNamesRemainStableTest(string species, string name, string expected, bool restricted)
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            await server.WaitAssertion(() =>
            {
                var cfg = server.ResolveDependency<IConfigurationManager>();
                var oldRestricted = cfg.GetCVar(CCVars.RestrictedNames);
                var oldCase = cfg.GetCVar(CCVars.ICNameCase);
                try
                {
                    cfg.SetCVar(CCVars.RestrictedNames, restricted);
                    cfg.SetCVar(CCVars.ICNameCase, false);
                    var profile = HumanoidCharacterProfile.RandomWithSpecies(species);
                    profile.Name = name ?? profile.Name;
                    expected ??= profile.Name;
                    Assert.That(expected, Is.Not.Empty);

                    for (var i = 0; i < 3; i++)
                    {
                        profile.EnsureValid(null, server.EntMan.EntitySysManager.DependencyCollection);
                        Assert.That(profile.Name, Is.EqualTo(expected), $"Validation {i + 1}");
                    }
                }
                finally
                {
                    cfg.SetCVar(CCVars.RestrictedNames, oldRestricted);
                    cfg.SetCVar(CCVars.ICNameCase, oldCase);
                }
            });

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task CreateDeleteCreateTest()
        {
            await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
            var server = pair.Server;
            var client = pair.Client;

            var clientNetManager = client.ResolveDependency<IClientNetManager>();
            var clientStateManager = client.ResolveDependency<IStateManager>();
            var clientPrefManager = client.ResolveDependency<IClientPreferencesManager>();

            var serverPrefManager = server.ResolveDependency<IServerPreferencesManager>();


            // Need to run them in sync to receive the messages.
            await pair.RunTicksSync(1);

            await PoolManager.WaitUntil(client, () => clientStateManager.CurrentState is LobbyState, 600);

            Assert.That(clientNetManager.ServerChannel, Is.Not.Null);

            var clientNetId = clientNetManager.ServerChannel.UserId;
            HumanoidCharacterProfile profile = null;

            await client.WaitAssertion(() =>
            {
                clientPrefManager.SelectCharacter(0);

                var clientCharacters = clientPrefManager.Preferences?.Characters;
                Assert.That(clientCharacters, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(clientCharacters, Has.Count.EqualTo(1));

                    Assert.That(clientStateManager.CurrentState, Is.TypeOf<LobbyState>());
                });

                profile = HumanoidCharacterProfile.Random();
                clientPrefManager.CreateCharacter(profile);

                clientCharacters = clientPrefManager.Preferences?.Characters;

                Assert.That(clientCharacters, Is.Not.Null);
                Assert.That(clientCharacters, Has.Count.EqualTo(2));
                Assert.That(clientCharacters[1].MemberwiseEquals(profile));
            });

            await PoolManager.WaitUntil(server, () => serverPrefManager.GetPreferences(clientNetId).Characters.Count == 2, maxTicks: 60);

            await server.WaitAssertion(() =>
            {
                var serverCharacters = serverPrefManager.GetPreferences(clientNetId).Characters;

                Assert.That(serverCharacters, Has.Count.EqualTo(2));
                Assert.That(serverCharacters[1].MemberwiseEquals(profile));
            });

            await client.WaitAssertion(() =>
            {
                clientPrefManager.DeleteCharacter(1);

                var clientCharacters = clientPrefManager.Preferences?.Characters.Count;
                Assert.That(clientCharacters, Is.EqualTo(1));
            });

            await PoolManager.WaitUntil(server, () => serverPrefManager.GetPreferences(clientNetId).Characters.Count == 1, maxTicks: 60);

            await server.WaitAssertion(() =>
            {
                var serverCharacters = serverPrefManager.GetPreferences(clientNetId).Characters.Count;
                Assert.That(serverCharacters, Is.EqualTo(1));
            });

            await client.WaitIdleAsync();

            await client.WaitAssertion(() =>
            {
                profile = HumanoidCharacterProfile.Random();

                clientPrefManager.CreateCharacter(profile);

                var clientCharacters = clientPrefManager.Preferences?.Characters;

                Assert.That(clientCharacters, Is.Not.Null);
                Assert.That(clientCharacters, Has.Count.EqualTo(2));
                Assert.That(clientCharacters[1].MemberwiseEquals(profile));
            });

            await PoolManager.WaitUntil(server, () => serverPrefManager.GetPreferences(clientNetId).Characters.Count == 2, maxTicks: 120); //60->120 - Mono

            await server.WaitAssertion(() =>
            {
                var serverCharacters = serverPrefManager.GetPreferences(clientNetId).Characters;

                Assert.That(serverCharacters, Has.Count.EqualTo(2));
                Assert.That(serverCharacters[1].MemberwiseEquals(profile));
            });
            await pair.CleanReturnAsync();
        }
    }
}
