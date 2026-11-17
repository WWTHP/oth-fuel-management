using MelonLoader;

[assembly: MelonInfo(typeof(FuelManagement.Mod), "FuelManagement", FuelManagement.Mod.Version, "WWTHP")]
[assembly: MelonGame(null, "over the hill")]

namespace FuelManagement
{
    /// <summary>
    /// Fuel management for Over the Hill. Cars have a fuel tank that driving drains (climbing
    /// more than cruising); any house refills it. The tank has a fixed range by default
    /// (optionally sized from the map so that typical question-mark round trips are relaxed
    /// and remote ones need planning). Side jerry cans add range. An empty tank puts the car in limp mode (drives
    /// worse than maximum damage) until it reaches a house. In multiplayer, players with the mod
    /// can siphon fuel from each other's cars (see <see cref="Siphon"/>).
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public const string Version = "0.2.3";
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings.Init();
            CrashGuard.Init();
            Fuel.Init();
            Net.Init();
            Siphon.Init();
            Log.Msg($"FuelManagement {Version} loaded. {Settings.MenuKey.Value} = menu, " +
                    $"hold {Settings.SiphonKey.Value} next to another car = siphon. " +
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
            try
            {
                // Always on (even with the fuel system off) so a peer's request gets an answer.
                Net.EnsureHooked();
                Net.ProcessInbox();
            }
            catch (System.Exception ex) { Log.Warning("Net: " + ex.Message); }
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
                Siphon.Update();
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

        public override void OnApplicationQuit()
        {
            Fuel.Save();
            Net.Unhook();
        }
    }
}
