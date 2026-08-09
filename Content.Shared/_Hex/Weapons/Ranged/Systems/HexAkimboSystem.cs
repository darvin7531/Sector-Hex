using System.Numerics;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Content.Shared._Hex.Weapons.Ranged.Components;
using Content.Shared._Hex.Weapons.Ranged.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Hex.Weapons.Ranged.Systems;

/// <summary>
/// Fires one compatible weapon from another hand whenever the active weapon fires.
/// Ammunition, cooldowns, prediction and projectile creation remain in the regular gun system.
/// </summary>
public sealed partial class HexAkimboSystem : EntitySystem
{
    private static readonly ProtoId<HexAkimboConfigPrototype> DefaultConfig = "HexAkimboDefault";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<GunComponent> source, ref GunShotEvent args)
    {
        if (!TryComp<HandsComponent>(args.User, out var hands) ||
            hands.ActiveHandEntity != source.Owner ||
            !IsEligible(source.Owner))
        {
            return;
        }

        if (!TryFindPartner(args.User,
                source.Owner,
                hands,
                out var partner,
                out var partnerGun,
                out var partnerHand) ||
            !_prototype.TryIndex(DefaultConfig, out var config))
        {
            return;
        }

        partnerGun.Target = source.Comp.Target;

        // The active weapon is the trigger master. The secondary weapon still uses its own
        // ammo provider and NextFire value through the regular gun system.
        var spreadCoordinates = GetSpreadCoordinates(
            args.User,
            args.ToCoordinates,
            partnerHand,
            config.SecondarySpreadDegrees);

        _gun.AttemptShoot(args.User, partner, partnerGun, spreadCoordinates);
    }

    private bool TryFindPartner(
        EntityUid user,
        EntityUid source,
        HandsComponent hands,
        out EntityUid partner,
        out GunComponent partnerGun,
        out HandLocation partnerHand)
    {
        partner = default;
        partnerGun = default!;
        partnerHand = default;

        foreach (var hand in _hands.EnumerateHands(user, hands))
        {
            if (hand.HeldEntity is not { } held)
                continue;

            if (held == source ||
                !TryComp(held, out GunComponent? gun) ||
                !IsEligible(held))
            {
                continue;
            }

            partner = held;
            partnerGun = gun;
            partnerHand = hand.Location;
            return true;
        }

        return false;
    }

    private EntityCoordinates GetSpreadCoordinates(
        EntityUid user,
        EntityCoordinates target,
        HandLocation partnerHand,
        float spreadDegrees)
    {
        if (spreadDegrees <= 0f)
            return target;

        var fromMap = _transform.GetMapCoordinates(user);
        var targetMap = _transform.ToMapCoordinates(target);

        if (fromMap.MapId != targetMap.MapId ||
            fromMap.MapId == MapId.Nullspace)
        {
            return target;
        }

        var direction = targetMap.Position - fromMap.Position;
        if (direction == Vector2.Zero)
            return target;

        var side = partnerHand == HandLocation.Left ? -1f : 1f;
        var spread = Angle.FromDegrees(spreadDegrees * side);
        var spreadTarget = new MapCoordinates(
            fromMap.Position + spread.RotateVec(direction),
            targetMap.MapId);

        return _transform.ToCoordinates(target.EntityId, spreadTarget);
    }

    /// <summary>
    /// Returns whether a gun is a configured one-handed pistol or submachine gun.
    /// </summary>
    public bool IsEligible(EntityUid weapon)
    {
        if (!TryComp<ItemComponent>(weapon, out var item) ||
            HasComp<GunRequiresWieldComponent>(weapon) ||
            HasComp<WieldableComponent>(weapon))
        {
            return false;
        }

        if (!_prototype.TryIndex(DefaultConfig, out var config) ||
            !_prototype.TryIndex(item.Size, out ItemSizePrototype? itemSize) ||
            !_prototype.TryIndex(config.MaximumItemSize, out ItemSizePrototype? maximumSize) ||
            itemSize > maximumSize)
        {
            return false;
        }

        var prototypeId = MetaData(weapon).EntityPrototype?.ID;
        if (prototypeId == null)
            return false;

        var entityPrototypeId = new EntProtoId(prototypeId);
        if (InheritsFrom(entityPrototypeId, config.DeniedPrototypes))
            return false;

        if (TryComp<HexAkimboWeaponComponent>(weapon, out var explicitConfig))
        {
            return explicitConfig.Enabled &&
                   explicitConfig.WeaponClass is HexAkimboWeaponClass.Pistol or HexAkimboWeaponClass.SubMachineGun;
        }

        return InheritsFrom(entityPrototypeId, config.PistolParents) ||
               InheritsFrom(entityPrototypeId, config.SubMachineGunParents) ||
               InheritsFrom(entityPrototypeId, config.EnergyPistolParents) ||
               InheritsFrom(entityPrototypeId, config.EnergySubMachineGunParents);
    }

    private bool InheritsFrom(EntProtoId prototypeId, List<EntProtoId> allowedParents)
    {
        if (allowedParents.Contains(prototypeId))
            return true;

        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype) ||
            prototype.Parents == null)
        {
            return false;
        }

        foreach (var parent in prototype.Parents)
        {
            if (InheritsFrom(new EntProtoId(parent), allowedParents))
                return true;
        }

        return false;
    }
}
