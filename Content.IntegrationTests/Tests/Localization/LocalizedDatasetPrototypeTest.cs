using System.Globalization;
using System.Linq;
using Content.Shared.Dataset;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Localization;

[TestFixture]
public sealed class LocalizedDatasetPrototypeTest
{
    [Test]
    public async Task MissingRussianStringFallsBackToEnglish()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var localizationMan = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            // This English-only objective also exercises the content MAKEPLURAL function.
            Assert.That(localizationMan.GetString(
                    "objective-condition-steal-multiply-description-faction-beacon-crocus-repair",
                    ("count", 2), ("itemName", "beacon")),
                Is.EqualTo("Ensure that atleast 2x beacons (any) are repaired, and they are in range of an extraction beacon."));
            Assert.That(localizationMan.GetString("objective-condition-steal-title-no-owner", ("itemName", "маяк")),
                Is.EqualTo("Украдите маяк."));
            Assert.That(localizationMan.DefaultCulture?.Name, Is.EqualTo("ru-RU"));
            Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo("ru-RU"));
            Assert.That(CultureInfo.CurrentUICulture.Name, Is.EqualTo("ru-RU"));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("game-ticker-get-info-preround-text", true)]
    [TestCase("game-ticker-get-info-text", false)]
    public async Task RussianRoundInfoUsesCurrentTickerArguments(string messageId, bool preround)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var localizationMan = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var text = localizationMan.GetString(messageId,
                ("roundId", 42), ("playerCount", 3), ("readyCount", 2),
                ("gmTitle", "Режим"), ("desc", "Описание"));
            var ready = preround ? " ([color=white]2[/color] готовы)" : "";
            Assert.That(text, Is.EqualTo(
                "Привет и добро пожаловать в [color=white]Space Station 14![/color]\n" +
                "Текущий раунд: [color=white]#42[/color]\n" +
                $"Текущее количество игроков: [color=white]3[/color]{ready}\n" +
                "Текущий режим игры: [color=white]Режим[/color]\n" +
                ">[color=yellow]Описание[/color]"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ValidProtoIdsTest()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var localizationMan = server.ResolveDependency<ILocalizationManager>();

        var protos = protoMan.EnumeratePrototypes<LocalizedDatasetPrototype>().OrderBy(p => p.ID);

        // Check each prototype
        foreach (var proto in protos)
        {
            // Check each value in the prototype
            foreach (var locId in proto.Values)
            {
                // Make sure the localization manager has a string for the LocId
                Assert.That(localizationMan.HasString(locId), $"LocalizedDataset {proto.ID} with prefix \"{proto.Values.Prefix}\" specifies {proto.Values.Count} entries, but no localized string was found matching {locId}!");
            }
        }

        await pair.CleanReturnAsync();
    }
}
