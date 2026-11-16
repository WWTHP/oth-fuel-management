using MelonLoader;

[assembly: MelonInfo(typeof(FuelManagement.Mod), "FuelManagement", FuelManagement.Mod.Version, "WWTHP")]
[assembly: MelonGame(null, "over the hill")]

namespace FuelManagement
{
    /// <summary>
    /// Fuel management for Over the Hill. Cars have a fuel tank that driving drains (climbing
    /// more than cruising); any house refills it. The tank is sized from the map so that a trip
    /// from a house to a typical question mark and back is relaxed, while remote ones need
    /// planning. Side jerry cans add range. An empty tank puts the car in limp mode (drives
    /// like maximum damage) until it reaches a house. Local only: no network messages.
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public const string Version = "0.1.0";
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings.Init();
            CrashGuard.Init();
            Fuel.Init();
            Log.Msg($"FuelManagement {Version} loaded. {Settings.MenuKey.Value} = menu. " +
                    $"Fuel {Fuel.Tank.Level * 100f:0}%.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            Fuel.OnSceneChanged();
            Houses.Reset();
        }

        public override void OnUpdate()
        {
            Hud.Update();
            if (!Settings.Enabled.Value)
            {
                Limp.Exit();
                return;
            }
            try
            {
                Car.Update();
                Houses.Update();
                Fuel.Update();
            }
            catch (System.Exception ex) { Log.Warning("Update: " + ex); }
        }

        public override void OnLateUpdate()
        {
            if (!Settings.Enabled.Value) return;
            try { Fuel.LateUpdate(); }
            catch (System.Exception ex) { Log.Warning("LateUpdate: " + ex.Message); }
        }

        public override void OnGUI() => Hud.Draw();

        public override void OnApplicationQuit() => Fuel.Save();
    }
}
