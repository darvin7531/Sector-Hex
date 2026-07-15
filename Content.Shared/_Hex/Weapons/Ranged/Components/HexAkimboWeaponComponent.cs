namespace Content.Shared._Hex.Weapons.Ranged.Components;

/// <summary>
/// Explicitly overrides the automatic akimbo classification for a weapon.
/// Weapons without this component are classified through the Hex akimbo configuration prototype.
/// </summary>
[RegisterComponent]
public sealed partial class HexAkimboWeaponComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public HexAkimboWeaponClass WeaponClass = HexAkimboWeaponClass.Pistol;
}

public enum HexAkimboWeaponClass : byte
{
    Pistol,
    SubMachineGun,
}
