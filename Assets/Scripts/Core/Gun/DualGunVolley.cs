using UnityEngine;

/// <summary>Launches one shot of each dual color from two settled tray guns.</summary>
public static class DualGunVolley
{
    public static bool CanFire(Gun source, BlockGroup group) =>
        TryGetPlans(source, group, false, true, out _, out _, out _);

    // Loss checks must not mistake cooldowns or already-reserved shots for an impossible pair.
    public static bool HasPotentialPair(Gun source, BlockGroup group) =>
        TryGetPlans(source, group, true, false, out _, out _, out _);

    public static bool TryFire(Gun source, BlockGroup group)
    {
        if (!TryGetPlans(source, group, false, true, out Gun partner,
            out Gun.ShootTarget sourcePlan, out Gun.ShootTarget partnerPlan))
            return false;
        if (!group.TryReserveDualPair()) return false;

        // Both plans and ammo were checked before either reservation or shot is consumed.
        Bullet first = source.FireReservedShot(sourcePlan);
        Bullet second = partner.FireReservedShot(partnerPlan);
        Bullet.PairDualShots(first, second);
        return true;
    }

    private static bool TryGetPlans(Gun source, BlockGroup group, bool ignoreReservations, bool requireReady,
        out Gun partner, out Gun.ShootTarget sourcePlan, out Gun.ShootTarget partnerPlan)
    {
        partner = null;
        sourcePlan = partnerPlan = new Gun.ShootTarget(null, Vector2.zero, null);
        if (source == null || group == null || group.groupType != BlockType.Dual || group.IsDead() ||
            !source.CanParticipateInDualVolley(requireReady) ||
            !group.TryGetDualColors(out BlockColorData firstColor, out BlockColorData secondColor))
            return false;
        BlockColorData partnerColor;
        if (source.currentColor == firstColor) partnerColor = secondColor;
        else if (source.currentColor == secondColor) partnerColor = firstColor;
        else return false;

        if (!ignoreReservations && !group.CanReserveDualPair()) return false;
        if (!source.TryPlanDualShot(group, ignoreReservations, out sourcePlan)) return false;

        foreach (TraySlot slot in TraySlot.GetSortedSlots())
        {
            Gun candidate = slot != null ? slot.CurrentGun : null;
            if (candidate == null || candidate == source || candidate.currentColor != partnerColor ||
                candidate.gameObject.scene != source.gameObject.scene ||
                !candidate.CanParticipateInDualVolley(requireReady) ||
                !candidate.TryPlanDualShot(group, ignoreReservations, out Gun.ShootTarget candidatePlan))
                continue;
            partner = candidate;
            partnerPlan = candidatePlan;
            return true;
        }
        return false;
    }
}
