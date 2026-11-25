using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieFPS
{
    /// <summary>
    /// F1: shows the DirectorContext (left) and the Director's own debug values (right).
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        public DirectorRunner runner;
        public bool visible;

        const float PanelWidth = 430f;
        const float PlotHeight = 46f;

        readonly StringBuilder sb = new StringBuilder();
        GUIStyle textStyle;
        GUIStyle boxStyle;
        Texture2D background;

        void Awake()
        {
            if (runner == null) runner = FindAnyObjectByType<DirectorRunner>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();

            DirectorContext ctx = runner != null ? runner.Context : null;
            string left = ctx != null ? BuildContextText(ctx) : "No IAIDirector found on the AI Director GameObject.";
            DrawPanel(new Rect(10f, 10f, PanelWidth, Screen.height - 20f), left);

            float x = Screen.width - PanelWidth - 10f;
            float y = 10f;
            y = DrawPanelAuto(x, y, BuildWatchText());
            y = DrawPlots(x, y + 6f);
            DrawPanelAuto(x, y + 6f, BuildLogText());
        }

        // ---------------------------------------------------------------- Context

        string BuildContextText(DirectorContext ctx)
        {
            PlayerInfo p = ctx.Player;
            GameStats s = ctx.Stats;
            WorldEventsInfo w = ctx.WorldEvents;
            sb.Clear();

            sb.AppendLine($"<b>CONTEXT</b>   t={ctx.GameTime:0.0}s   {ctx.State}");
            sb.AppendLine();
            sb.AppendLine("<b>Player</b>");
            sb.AppendLine($"  Health {p.Health:0}/{p.MaxHealth:0} ({p.HealthRatio:0.00})   Alive {p.IsAlive}");
            string reserve = p.ReserveAmmo < 0 ? "∞" : p.ReserveAmmo.ToString();
            sb.AppendLine($"  {p.CurrentWeapon} {p.AmmoInMagazine}/{reserve}   AmmoRatio {p.AmmoRatio:0.00}   Grenades {p.Grenades}/{p.MaxGrenades}");
            sb.AppendLine($"  Weapons  P:{Flag(p.HasWeapon(WeaponType.Pistol))} S:{Flag(p.HasWeapon(WeaponType.Shotgun))} R:{Flag(p.HasWeapon(WeaponType.Rifle))}");
            sb.AppendLine($"  Progress {p.Progress:0.000}  ({p.DistanceAlongPath:0}/{ctx.PathLength:0} m)");
            sb.AppendLine($"  Zone {p.CurrentZone} {ctx.GetZoneName(p.CurrentZone)}   SafeRoom {p.IsInSafeRoom}");
            sb.AppendLine($"  Speed {p.Speed:0.0}   Sprinting {p.IsSprinting}");
            sb.AppendLine();

            sb.AppendLine($"<b>Enemies</b>  {ctx.CountEnemies()}/{ctx.MaxAliveEnemies}   alerted {ctx.CountAlertedEnemies()}");
            sb.AppendLine($"  Common {ctx.CountEnemies(EnemyType.Common)}  Runner {ctx.CountEnemies(EnemyType.Runner)}  Tank {ctx.CountEnemies(EnemyType.Tank)}  Bloater {ctx.CountEnemies(EnemyType.Bloater)}");
            sb.AppendLine($"  Within 5m {ctx.CountEnemiesWithin(5f)}   10m {ctx.CountEnemiesWithin(10f)}   25m {ctx.CountEnemiesWithin(25f)}");
            sb.AppendLine($"  Modifiers  damage x{ctx.EnemyDamageMultiplier:0.##}   speed x{ctx.EnemySpeedMultiplier:0.##}");
            sb.AppendLine();

            sb.AppendLine($"<b>Pickups</b>  {ctx.Pickups.Count}/{ctx.MaxPickups}");
            sb.AppendLine($"  Health {ctx.CountPickups(PickupType.Health)}  Ammo {ctx.CountPickups(PickupType.Ammo)}  Grenade {ctx.CountPickups(PickupType.Grenade)}  Shotgun {ctx.CountPickups(PickupType.Shotgun)}  Rifle {ctx.CountPickups(PickupType.Rifle)}");
            sb.AppendLine();

            sb.AppendLine("<b>Spawn points</b>");
            AppendSpawnPoints("Enemy", ctx.EnemySpawnPoints);
            AppendSpawnPoints("Pickup", ctx.PickupSpawnPoints);
            sb.AppendLine();

            sb.AppendLine("<b>World events</b>");
            foreach (WorldEventType type in System.Enum.GetValues(typeof(WorldEventType)))
            {
                string state = w.IsActive(type)
                    ? type == WorldEventType.Horde ? $"<color=#FF7050>ACTIVE</color> ({w.PendingHordeEnemies} left)" : $"<color=#FF7050>ACTIVE</color> {w.GetRemainingTime(type):0.0}s"
                    : "-";
                sb.AppendLine($"  {type,-11} {state}   last {Seconds(w.TimeSinceLastTrigger(type))}   x{w.TriggerCount(type)}");
            }
            sb.AppendLine();

            sb.AppendLine("<b>Stats</b>          total      last 10s     last 30s");
            sb.AppendLine($"  Kills          {s.TotalKills,5}      {s.KillsInLast(10f),5}       {s.KillsInLast(30f),5}");
            sb.AppendLine($"  Headshots      {s.TotalHeadshots,5}      {s.HeadshotsInLast(10f),5}       {s.HeadshotsInLast(30f),5}");
            sb.AppendLine($"  Dmg taken      {s.TotalDamageTaken,5:0}      {s.DamageTakenInLast(10f),5:0}       {s.DamageTakenInLast(30f),5:0}");
            sb.AppendLine($"  Dmg dealt      {s.TotalDamageDealt,5:0}      {s.DamageDealtInLast(10f),5:0}       {s.DamageDealtInLast(30f),5:0}");
            sb.AppendLine($"  Healing        {s.TotalHealing,5:0}      {s.HealingInLast(10f),5:0}       {s.HealingInLast(30f),5:0}");
            sb.AppendLine($"  Accuracy       {s.Accuracy,5:0.00}      {Ratio(s.AccuracyInLast(10f)),5}       {Ratio(s.AccuracyInLast(30f)),5}");
            sb.AppendLine($"  Since combat {Seconds(s.TimeSinceLastCombat)}   dmg taken {Seconds(s.TimeSinceDamageTaken)}   kill {Seconds(s.TimeSinceKill)}");
            return sb.ToString();
        }

        void AppendSpawnPoints(string label, IReadOnlyList<SpawnPointInfo> points)
        {
            int visible = 0, ahead = 0, occupied = 0;
            foreach (SpawnPointInfo point in points)
            {
                if (point.IsVisibleToPlayer) visible++;
                if (point.IsAheadOfPlayer) ahead++;
                if (point.IsOccupied) occupied++;
            }
            sb.Append($"  {label,-7} {points.Count,3}   visible {visible,3}   ahead {ahead,3}");
            if (label == "Pickup") sb.Append($"   occupied {occupied}");
            sb.AppendLine();
        }

        static string Flag(bool value) => value ? "✔" : "-";
        static string Seconds(float value) => float.IsInfinity(value) ? "never" : $"{value:0.0}s";
        static string Ratio(float value) => value < 0f ? "-" : value.ToString("0.00");

        // ---------------------------------------------------------------- Director debug

        string BuildWatchText()
        {
            sb.Clear();
            sb.AppendLine($"<b>DIRECTOR</b>   {DirectorDebug.DirectorName}");
            foreach (KeyValuePair<string, string> pair in DirectorDebug.watches)
                sb.AppendLine($"  {pair.Key}: {pair.Value}");
            return sb.ToString().TrimEnd();
        }

        string BuildLogText()
        {
            sb.Clear();
            sb.AppendLine("<b>Log</b>");
            foreach (string line in DirectorDebug.log) sb.AppendLine(line);
            return sb.ToString().TrimEnd();
        }

        float DrawPlots(float x, float y)
        {
            foreach (string key in DirectorDebug.plotOrder)
            {
                DirectorDebug.PlotData plot = DirectorDebug.plots[key];
                var rect = new Rect(x, y, PanelWidth, PlotHeight + 18f);
                GUI.Box(rect, GUIContent.none, boxStyle);
                Color previous = GUI.color;
                GUI.color = plot.color;
                GUI.Label(new Rect(x + 6f, y + 1f, PanelWidth, 18f), $"{key}: {plot.last:0.###}   [{plot.min:0.##} .. {plot.max:0.##}]", textStyle);

                float graphX = x + 6f;
                float graphY = y + 18f;
                float graphW = PanelWidth - 12f;
                float step = graphW / DirectorDebug.PlotLength;
                float range = Mathf.Max(0.0001f, plot.max - plot.min);
                int start = (plot.head - plot.count + DirectorDebug.PlotLength) % DirectorDebug.PlotLength;
                int offset = DirectorDebug.PlotLength - plot.count;
                for (int i = 0; i < plot.count; i++)
                {
                    float value = plot.values[(start + i) % DirectorDebug.PlotLength];
                    float t = Mathf.Clamp01((value - plot.min) / range);
                    float py = graphY + (1f - t) * (PlotHeight - 3f);
                    GUI.DrawTexture(new Rect(graphX + (offset + i) * step, py, Mathf.Max(1.5f, step), 2.5f), Texture2D.whiteTexture);
                }
                GUI.color = previous;
                y += rect.height + 2f;
            }
            return y;
        }

        // ---------------------------------------------------------------- Drawing

        void DrawPanel(Rect rect, string text)
        {
            float height = Mathf.Min(rect.height, textStyle.CalcHeight(new GUIContent(text), rect.width - 12f) + 12f);
            rect.height = height;
            GUI.Box(rect, GUIContent.none, boxStyle);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), text, textStyle);
        }

        float DrawPanelAuto(float x, float y, string text)
        {
            float height = textStyle.CalcHeight(new GUIContent(text), PanelWidth - 12f) + 12f;
            DrawPanel(new Rect(x, y, PanelWidth, height), text);
            return y + height;
        }

        void EnsureStyles()
        {
            if (textStyle != null) return;

            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            background.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;

            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true,
                wordWrap = false,
                font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Lucida Console" }, 12),
            };
            textStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        }

        void OnDestroy()
        {
            if (background != null) Destroy(background);
        }
    }
}
