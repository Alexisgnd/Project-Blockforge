using System.Collections.Generic;
using UnityEngine;

// =========================================================
// MODELE DE VITESSE DES ROBOTS (source unique)
// Utilise a la fois par la physique (RobotLocomotion regle la
// vitesse de pointe de chaque bloc de mouvement) et par les
// menus (HUD du garage, accueil, ecran Build) : la vitesse
// affichee est celle que le robot atteint vraiment en match.
// - Vitesse de base par famille (m/s) : roues 25 (90 km/h),
//   chenilles 15,5 (56 km/h), pattes 17 (les marcheurs courent :
//   61 km/h), survol 21,5 (77 km/h), rotors 20 (72 km/h) ;
//   propulseurs 42 (plafond, 151 km/h)
//   (oct. 2026 : parties jugees trop lentes, sol x1,55 environ,
//   air x1,43 ; acceleration et adherence relevees d'autant pour
//   que le robot atteigne sa vitesse aussi vite qu'avant)
// - Acceleration effective au sol = min(Accel, Friction x g) :
//   roues 17,7, chenilles 15,5, pattes 18,2, survol 9,3 m/s^2
// - Niveau du bloc (_N1.._N6) : +12 % par niveau au sol,
//   +30 % pour les rotors et propulseurs (TierStep)
// - Charge : chaque bloc porteur supporte une masse nominale
//   (x niveau). Au-dela, la vitesse baisse en 1/sqrt(surcharge),
//   jamais sous 30 %. Un robot lourd sur trois roues rampe, un
//   robot leger sur huit roues file.
// - Melange de familles au sol : chaque appui tire vers SA
//   vitesse de pointe, les plus lents freinent les plus rapides ;
//   la vitesse d'equilibre est resolue numeriquement, poussee des
//   propulseurs comprise (ils emmenent le robot au-dessus de la
//   vitesse de ses roues, jusqu'a leur plafond).
// =========================================================

public static class RobotSpeedModel
{
    // ---------- Reglages par famille ----------
    public const float WheelSpeed = 25f, TrackSpeed = 15.5f, LegSpeed = 17f, HoverSpeed = 21.5f, RotorSpeed = 20f;
    public const float WheelAccel = 22f, TrackAccel = 15.5f, LegAccel = 18.5f, HoverAccel = 14f;
    // Frottement (μ) : borne la traction ET l'adherence laterale (RobotLocomotion) ;
    // releve avec la vitesse pour que le robot ne derape pas plus dans les virages
    public const float WheelFriction = 1.8f, TrackFriction = 2f, LegFriction = 1.85f, HoverFriction = 0.95f;
    public const float ThrusterLimitBase = 42f, ThrusterAccelBase = 13f;

    // Masse nominale portee par un bloc de niveau 1 (kg)
    public const float WheelLoad = 1400f, TrackLoad = 2600f, LegLoad = 1500f, HoverLoad = 1000f, RotorLoad = 1500f;

    public const float GroundTierStep = 0.12f;
    public const float MinLoadFactor = 0.3f;

    public static float GroundTierFactor(int tier) => 1f + GroundTierStep * Mathf.Max(0, tier - 1);

    public static bool IsGround(LocomotionKind k) =>
        k == LocomotionKind.Wheel || k == LocomotionKind.Track || k == LocomotionKind.Leg || k == LocomotionKind.Hover;

    public static float BaseSpeed(LocomotionKind k) => k switch
    {
        LocomotionKind.Wheel => WheelSpeed,
        LocomotionKind.Track => TrackSpeed,
        LocomotionKind.Leg => LegSpeed,
        LocomotionKind.Hover => HoverSpeed,
        LocomotionKind.Rotor => RotorSpeed,
        _ => 0f,
    };

    public static float Accel(LocomotionKind k) => k switch
    {
        LocomotionKind.Wheel => WheelAccel,
        LocomotionKind.Track => TrackAccel,
        LocomotionKind.Leg => LegAccel,
        LocomotionKind.Hover => HoverAccel,
        _ => 0f,
    };

    public static float Friction(LocomotionKind k) => k switch
    {
        LocomotionKind.Wheel => WheelFriction,
        LocomotionKind.Track => TrackFriction,
        LocomotionKind.Leg => LegFriction,
        LocomotionKind.Hover => HoverFriction,
        _ => 1f,
    };

    // Points d'appui d'un bloc (les chenilles en ont trois, cf. RobotLocomotion.BuildUnit)
    public static int Contacts(LocomotionKind k) => k == LocomotionKind.Track ? 3 : IsGround(k) ? 1 : 0;

    public static float Load(LocomotionKind k, int tier)
    {
        float baseLoad = k switch
        {
            LocomotionKind.Wheel => WheelLoad,
            LocomotionKind.Track => TrackLoad,
            LocomotionKind.Leg => LegLoad,
            LocomotionKind.Hover => HoverLoad,
            LocomotionKind.Rotor => RotorLoad,
            _ => 0f,
        };
        return baseLoad * RobotLocomotion.TierFactor(tier);
    }

    // 1 tant que la masse reste sous la capacite des blocs porteurs, puis 1/sqrt(surcharge)
    public static float LoadFactor(float weightKg, float capacityKg)
    {
        if (capacityKg <= 0f || weightKg <= capacityKg)
            return 1f;
        return Mathf.Clamp(1f / Mathf.Sqrt(weightKg / capacityKg), MinLoadFactor, 1f);
    }

    public static float ThrusterLimit(int tier) => ThrusterLimitBase * RobotLocomotion.TierFactor(tier);

    // Famille d'un bloc de mouvement (meme regle que RobotLocomotion.BuildUnit)
    public static bool TryClassify(BlockDefinition def, out LocomotionKind kind)
    {
        kind = LocomotionKind.Wheel;
        if (def == null || def.category != BlockCategory.Mouvement)
            return false;
        string family = (def.family ?? "").ToLowerInvariant();
        if (family.Contains("wheel")) kind = LocomotionKind.Wheel;
        else if (family.Contains("track")) kind = LocomotionKind.Track;
        else if (family.Contains("leg")) kind = LocomotionKind.Leg;
        else if (family.Contains("hover")) kind = LocomotionKind.Hover;
        else if (family.Contains("rotor")) kind = LocomotionKind.Rotor;
        else if (family.Contains("thruster")) kind = LocomotionKind.Thruster;
        else if (family.Contains("rudder")) kind = LocomotionKind.Rudder;
        else return false;
        return true;
    }

    // ---------- Estimation ----------

    public struct Unit
    {
        public LocomotionKind kind;
        public int tier;
        public Unit(LocomotionKind kind, int tier) { this.kind = kind; this.tier = tier; }
    }

    public static float Capacity(IEnumerable<Unit> units)
    {
        float capacity = 0f;
        bool rotors = false;
        foreach (var u in units)
            rotors |= u.kind == LocomotionKind.Rotor;
        foreach (var u in units)
        {
            // Un robot a rotors est porte par ses rotors ; sinon par ses appuis au sol
            if (rotors ? u.kind == LocomotionKind.Rotor : IsGround(u.kind))
                capacity += Load(u.kind, u.tier);
        }
        return capacity;
    }

    // Vitesse de pointe en ligne droite (m/s), boost exclu
    public static float EstimateTopSpeed(IReadOnlyList<Unit> units, float weightKg)
    {
        if (units == null || units.Count == 0)
            return 0f;
        float load = LoadFactor(weightKg, Capacity(units));

        int bestRotor = 0, bestThruster = 0, thrusters = 0;
        float thrustAccel = 0f;
        foreach (var u in units)
        {
            if (u.kind == LocomotionKind.Rotor) bestRotor = Mathf.Max(bestRotor, u.tier);
            if (u.kind == LocomotionKind.Thruster)
            {
                bestThruster = Mathf.Max(bestThruster, u.tier);
                thrusters++;
                thrustAccel += ThrusterAccelBase * RobotLocomotion.TierFactor(u.tier);
            }
        }
        if (thrusters > 0)
            thrustAccel /= thrusters;   // la poussee totale est repartie entre les propulseurs
        float thrusterLimit = thrusters > 0 ? ThrusterLimit(bestThruster) : 0f;

        // Robot volant : vitesse des rotors, ou plafond des propulseurs s'il est plus haut
        if (bestRotor > 0)
            return Mathf.Max(RotorSpeed * RobotLocomotion.TierFactor(bestRotor) * load, thrusterLimit);

        // Appuis au sol : vitesse d'equilibre (forces de traction + poussee = 0)
        int contacts = 0;
        foreach (var u in units)
            contacts += Contacts(u.kind);
        if (contacts == 0)
            return thrusterLimit;   // propulseurs seuls, le robot glisse sur son chassis

        float g = -Physics.gravity.y;
        float Force(float v)
        {
            float f = 0f;
            foreach (var u in units)
            {
                int c = Contacts(u.kind);
                if (c == 0)
                    continue;
                float target = BaseSpeed(u.kind) * GroundTierFactor(u.tier) * load;
                float cap = Mathf.Min(Accel(u.kind), Friction(u.kind) * g);
                f += c * Mathf.Clamp(target - v, -cap, cap);
            }
            f /= contacts;
            if (thrusters > 0)
                f += thrustAccel * (v > thrusterLimit ? 0.2f : 1f);
            return f;
        }

        float lo = 0f, hi = 200f;
        for (int i = 0; i < 40; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (Force(mid) > 0f) lo = mid;
            else hi = mid;
        }
        return 0.5f * (lo + hi);
    }

    public static float EstimateTopSpeed(IEnumerable<BlockDefinition> defs, float weightKg)
    {
        var units = new List<Unit>();
        foreach (var def in defs)
        {
            if (TryClassify(def, out var kind) && kind != LocomotionKind.Rudder)
                units.Add(new Unit(kind, RobotLocomotion.TierOf(def.name)));
        }
        return EstimateTopSpeed(units, weightKg);
    }

    // Blueprint sauvegarde + catalogue : false si un bloc est inconnu du catalogue
    public static bool TryEstimateKmh(RobotBlueprint blueprint, IReadOnlyDictionary<string, BlockDefinition> catalog, out int kmh)
    {
        kmh = 0;
        if (blueprint?.blocks == null || catalog == null)
            return false;
        var defs = new List<BlockDefinition>();
        int weight = 0;
        foreach (var b in blueprint.blocks)
        {
            if (b == null || string.IsNullOrEmpty(b.blockId))
                continue;   // cases de peinture seules
            if (!catalog.TryGetValue(b.blockId, out var def) || def == null)
                continue;
            defs.Add(def);
            weight += def.weightKg;
        }
        if (defs.Count == 0)
            return false;
        kmh = ToKmh(EstimateTopSpeed(defs, weight));
        return true;
    }

    public static int ToKmh(float metersPerSecond) => Mathf.RoundToInt(metersPerSecond * 3.6f);
}
