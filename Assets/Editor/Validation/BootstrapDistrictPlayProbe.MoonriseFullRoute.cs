using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static bool MoonriseFullRouteRequested => Environment.GetCommandLineArgs().Contains("-moonriseFullRoute");
    private static double moonriseRouteArrivalAt;

    private static void RecordMoonriseRouteCheckpoint(string checkpoint)
    {
        if (!MoonriseFullRouteRequested) return;
        string path = Path.Combine(report.run, "moonrise-route-timing.csv");
        if (checkpoint == "moonrise_arrival")
        {
            moonriseRouteArrivalAt = Time.realtimeSinceStartupAsDouble;
            File.WriteAllText(path, "checkpoint,real_seconds_since_moonrise_arrival,frame\n");
            report.scope = "One fresh isolated production session: Bootstrap/Maison portal (declared Maison UCC approach fixture, intro omitted), real Moonrise walking/A for Ancient Flame and Luc/Jon, current production Juggernaut pursuit/impacts/counter/KO/death, victory A, torch/ladder, disk save/menu/resume, upper landing walk/return/A, shared-selection levers and Stillhaven portal/A. No injected inventory, knowledge, damage, defeat or player pose in Moonrise. Timing is an automated fast route including save/load, not a measured human 15-20 minute discovery session or multiplayer/feel qualification.";
        }
        File.AppendAllText(path, checkpoint + "," + (Time.realtimeSinceStartupAsDouble - moonriseRouteArrivalAt).ToString("F3", CultureInfo.InvariantCulture) + "," + Time.frameCount + "\n");
    }
}
