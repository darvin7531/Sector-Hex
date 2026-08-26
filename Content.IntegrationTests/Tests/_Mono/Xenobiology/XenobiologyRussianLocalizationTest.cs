using System.Globalization;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class XenobiologyRussianLocalizationTest
{
    private static readonly string[] PlayerFacingIds =
    [
        "mono-reagent-property-name-toxic",
        "mono-reagent-property-desc-toxic",
        "mono-reagent-property-name-neogenetic",
        "mono-reagent-property-desc-neogenetic",
        "mono-reagent-property-ciphering",
        "mono-cipher-toxin",
        "research-data-terminal-contract-header",
        "reagent-name-abomination-venom",
        "mono-xrf-scanner-title",
        "mono-synthesis-simulator-title",
        "mono-research-terminal-title",
    ];

    [Test]
    public async Task PlayerFacingXenobiologyStringsAreRussian()
    {
        await using var pair = await PoolManager.GetServerClient();
        var locale = pair.Server.ResolveDependency<ILocalizationManager>();

        await pair.Server.WaitAssertion(() =>
        {
            locale.SetCulture(new CultureInfo("ru-RU"));
            foreach (var id in PlayerFacingIds)
                Assert.That(locale.GetString(id), Does.Match("[А-Яа-яЁё]"), id);
        });

        await pair.CleanReturnAsync();
    }
}
