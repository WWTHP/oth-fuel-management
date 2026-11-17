using System;
using Il2Cpp;
using Il2CppVehiclePhysics;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// Limp mode for an empty tank: the car drives worse than at maximum damage
    /// (max-damage values, then capped at LimpMaxKmh and scaled by LimpPower).
    ///
    /// The game's CarDamageSystem slows a damaged car by scaling the vehicle's speed limit and
    /// engine torque from its "original" values with the DamageEffects curves. We evaluate
    /// those same curves at their worst point and write the result into the same vehicle
    /// settings every frame (LateUpdate, after the game's own debuff update), so it's the
    /// game's own max-damage behaviour without touching the real damage value (repairs,
    /// UI and multiplayer damage state stay untouched). On refuel the settings captured on
    /// entry are restored and the game re-applies the debuffs for the real damage.
    /// </summary>
    internal static class Limp
    {
        public static bool Active { get; private set; }

        private static CarDamageSystem _cds;
        private static bool _savedLimiter, _savedTorqueCap;
        private static float _savedSpeedLimit, _savedPeakTorque, _savedTorqueCapLimit;
        private static float _baseSpeed, _speedMul, _baseTorque, _torqueMul;
        private static bool _useTorqueCap;

        public static void Enter(CarDamageSystem cds)
        {
            if (Active || cds == null) return;
            var vehicle = cds.vehicle;
            if (vehicle == null) { Mod.Log.Warning("Limp mode: car has no vehicle controller; skipped."); return; }
            var sc = vehicle.speedControl;
            var en = vehicle.engine;
            if (sc == null || en == null) { Mod.Log.Warning("Limp mode: vehicle settings missing; skipped."); return; }

            _savedLimiter = sc.speedLimiter;
            _savedSpeedLimit = sc.speedLimit;
            _savedPeakTorque = en.peakRpmTorque;
            _savedTorqueCap = en.torqueCap;
            _savedTorqueCapLimit = en.torqueCapLimit;

            float baseSpeed = cds.originalSpeedLimit > 0f ? cds.originalSpeedLimit : sc.speedLimit;
            float baseTorque = cds.originalMaxTorque > 0f ? cds.originalMaxTorque : en.peakRpmTorque;
            float speedMul = Worst(cds.damageEffects?.maxSpeedMultiplier, Settings.LimpFallbackSpeed.Value);
            float torqueMul = Worst(cds.damageEffects?.torqueMultiplier, Settings.LimpFallbackTorque.Value);

            // Which engine field the game treats as "max torque": whichever is closest to the original.
            _useTorqueCap = en.torqueCap && Math.Abs(en.torqueCapLimit - baseTorque) < Math.Abs(en.peakRpmTorque - baseTorque);
            _baseSpeed = baseSpeed; _speedMul = speedMul;
            _baseTorque = baseTorque; _torqueMul = torqueMul;
            _cds = cds;
            Active = true;
            Mod.Log.Msg($"Limp mode ON. speed limit {sc.speedLimit:0.0} m/s (orig {cds.originalSpeedLimit:0.0}) → " +
                        $"{SpeedTarget():0.0} (max damage x{speedMul:0.00}, cap {Settings.LimpMaxKmh.Value:0} km/h); " +
                        $"torque field {(_useTorqueCap ? "torqueCapLimit" : "peakRpmTorque")} " +
                        $"peak {en.peakRpmTorque:0} cap {en.torqueCapLimit:0}/{en.torqueCap} (orig {cds.originalMaxTorque:0}) " +
                        $"→ {TorqueTarget():0} (max damage x{torqueMul:0.00}, power x{Settings.LimpPower.Value:0.00}).");
            Enforce();
        }

        // Recomputed every frame so the F10 limp sliders apply immediately.
        private static float SpeedTarget() => LimpTargets.Speed(_baseSpeed, _speedMul, Settings.LimpMaxKmh.Value);
        private static float TorqueTarget() => LimpTargets.Torque(_baseTorque, _torqueMul, Settings.LimpPower.Value);

        /// <summary>Re-applies the limp values; call every frame (LateUpdate) while active.</summary>
        public static void Enforce()
        {
            if (!Active) return;
            if (_cds == null || _cds.WasCollected || _cds.vehicle == null) { Active = false; _cds = null; return; }
            var sc = _cds.vehicle.speedControl;
            var en = _cds.vehicle.engine;
            float speed = SpeedTarget(), torque = TorqueTarget();
            if (!float.IsPositiveInfinity(speed))
            {
                sc.speedLimiter = true;
                if (sc.speedLimit != speed) sc.speedLimit = speed;
            }
            if (_useTorqueCap) { if (en.torqueCapLimit != torque) en.torqueCapLimit = torque; }
            else if (en.peakRpmTorque != torque) en.peakRpmTorque = torque;
        }

        public static void Exit()
        {
            if (!Active) return;
            Active = false;
            var cds = _cds;
            _cds = null;
            if (cds == null || cds.WasCollected || cds.vehicle == null) return;
            var sc = cds.vehicle.speedControl;
            var en = cds.vehicle.engine;
            sc.speedLimiter = _savedLimiter;
            sc.speedLimit = _savedSpeedLimit;
            en.peakRpmTorque = _savedPeakTorque;
            en.torqueCap = _savedTorqueCap;
            en.torqueCapLimit = _savedTorqueCapLimit;
            try { if (cds.DamageAmount > 0f) cds.ApplyDriveabilityDebuffs(); }
            catch (Exception ex) { Mod.Log.Warning("Re-applying damage debuffs failed: " + ex.Message); }
            Mod.Log.Msg("Limp mode OFF (settings restored).");
        }

        /// <summary>
        /// The curve's worst (lowest) end value = maximum damage, whichever way its x axis runs,
        /// or the fallback if the curve is unusable.
        /// </summary>
        private static float Worst(AnimationCurve curve, float fallback)
        {
            try
            {
                if (curve != null && curve.length > 0)
                {
                    float v = Math.Min(curve.Evaluate(curve[0].time), curve.Evaluate(curve[curve.length - 1].time));
                    if (!float.IsNaN(v) && !float.IsInfinity(v) && v > 0f && v <= 1f) return v;
                }
            }
            catch (Exception ex) { Mod.Log.Warning("Damage curve unreadable: " + ex.Message); }
            return fallback;
        }
    }
}
