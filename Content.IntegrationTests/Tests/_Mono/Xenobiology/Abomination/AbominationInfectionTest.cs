// SPDX-FileCopyrightText: 2026 Nous Research
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Mono.Xenobiology.Abomination;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Linq;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology.Abomination;

[TestFixture]
[NonParallelizable]
public sealed class AbominationInfectionTest
{
    [Test]
    public async Task VenomInfectsBiologicalHumanoidsAndAnimals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var effect = new AbominationInfectionEffect();

        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var animal = entities.SpawnEntity("MobMouse", MapCoordinates.Nullspace);

            effect.Effect(new EntityEffectBaseArgs(human, entities));
            effect.Effect(new EntityEffectBaseArgs(animal, entities));

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<AbominationInfectionComponent>(human), Is.True);
                Assert.That(entities.HasComponent<AbominationInfectionComponent>(animal), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidTargetsAreRejected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var infection = server.System<AbominationInfectionSystem>();
        var mobState = server.System<MobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var synth = entities.SpawnEntity("MobIPC", MapCoordinates.Nullspace);
            var dead = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var infected = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var abomination = entities.SpawnEntity("MobAbomination", MapCoordinates.Nullspace);
            mobState.ChangeMobState(dead, MobState.Dead, entities.GetComponent<MobStateComponent>(dead));

            Assert.Multiple(() =>
            {
                Assert.That(infection.TryInfect(synth), Is.False, "synthetics must be immune");
                Assert.That(infection.TryInfect(dead), Is.False, "dead-at-injection targets must be rejected");
                Assert.That(infection.TryInfect(infected), Is.True);
                Assert.That(infection.TryInfect(infected), Is.False, "infection must not reset");
                Assert.That(infection.TryInfect(abomination), Is.False, "abominations must be immune");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SymptomsAreDelayedAndDealDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var infection = server.System<AbominationInfectionSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(infection.TryInfect(human), Is.True);
            var component = entities.GetComponent<AbominationInfectionComponent>(human);
            var damage = entities.GetComponent<DamageableComponent>(human);

            infection.Update(0f);
            Assert.That(component.HasShownSymptoms, Is.False);
            Assert.That(damage.Damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero));

            component.InfectedAt -= component.SymptomDelay;
            component.NextDamageAt = component.InfectedAt;
            infection.Update(0f);

            Assert.That(component.HasShownSymptoms, Is.True);
            Assert.That(damage.Damage.GetTotal(), Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeathBeforeSymptomsDoesNotConvert()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var infection = server.System<AbominationInfectionSystem>();
        var mobState = server.System<MobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var before = CountAbominations(entities);
            var human = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(infection.TryInfect(human), Is.True);

            mobState.ChangeMobState(human, MobState.Dead, entities.GetComponent<MobStateComponent>(human));

            Assert.That(CountAbominations(entities), Is.EqualTo(before));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SymptomaticDeathConvertsExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var infection = server.System<AbominationInfectionSystem>();
        var mobState = server.System<MobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var before = CountAbominations(entities);
            var animal = entities.SpawnEntity("MobMouse", MapCoordinates.Nullspace);
            Assert.That(infection.TryInfect(animal), Is.True);
            entities.GetComponent<AbominationInfectionComponent>(animal).HasShownSymptoms = true;

            mobState.ChangeMobState(animal, MobState.Dead, entities.GetComponent<MobStateComponent>(animal));

            Assert.Multiple(() =>
            {
                Assert.That(infection.TryConvert(animal), Is.False, "a repeated death path must not convert twice");
                Assert.That(CountAbominations(entities), Is.EqualTo(before + 1));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static int CountAbominations(IEntityManager entities)
        => entities.EntityQuery<MetaDataComponent>().Count(meta =>
            !meta.Deleted && meta.EntityPrototype?.ID == AbominationInfectionSystem.AbominationPrototype);
}
