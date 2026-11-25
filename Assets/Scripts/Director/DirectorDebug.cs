using System.Collections.Generic;
using UnityEngine;

namespace ZombieFPS
{
    /// <summary>
    /// Displays your own Director values in the debug overlay (F1).
    /// Call these from Tick, e.g. to show utility scores or your intensity metric.
    /// </summary>
    public static class DirectorDebug
    {
        internal const int PlotLength = 240;
        const int LogLength = 14;

        internal sealed class PlotData
        {
            public readonly float[] values = new float[PlotLength];
            public int head;
            public int count;
            public float min;
            public float max;
            public Color color;
            public float last;
        }

        internal static readonly SortedDictionary<string, string> watches = new SortedDictionary<string, string>();
        internal static readonly Dictionary<string, PlotData> plots = new Dictionary<string, PlotData>();
        internal static readonly List<string> plotOrder = new List<string>();
        internal static readonly LinkedList<string> log = new LinkedList<string>();
        internal static string DirectorName = "-";

        static readonly Color[] palette =
        {
            new Color(1f, 0.45f, 0.35f), new Color(0.35f, 0.8f, 1f), new Color(0.5f, 1f, 0.45f),
            new Color(1f, 0.85f, 0.3f), new Color(0.85f, 0.5f, 1f), new Color(1f, 0.6f, 0.85f),
        };

        /// <summary>Shows "key: value" in the Director panel. Call again to update it.</summary>
        public static void Watch(string key, float value) => watches[key] = value.ToString("0.###");

        /// <summary>Shows "key: value" in the Director panel. Call again to update it.</summary>
        public static void Watch(string key, string value) => watches[key] = value;

        /// <summary>
        /// Adds a sample to a scrolling graph (one sample per call, call it once per Tick).
        /// Plots sharing the same min/max are easy to compare, e.g. utility scores in [0, 1].
        /// </summary>
        public static void Plot(string key, float value, float min = 0f, float max = 1f)
        {
            if (!plots.TryGetValue(key, out PlotData plot))
            {
                plot = new PlotData { color = palette[plotOrder.Count % palette.Length] };
                plots[key] = plot;
                plotOrder.Add(key);
            }
            plot.min = min;
            plot.max = max;
            plot.last = value;
            plot.values[plot.head] = value;
            plot.head = (plot.head + 1) % PlotLength;
            plot.count = Mathf.Min(plot.count + 1, PlotLength);
        }

        /// <summary>Adds a line to the Director log panel.</summary>
        public static void Log(string message) => AddLog(message);

        internal static void LogAction(string message) => AddLog("> " + message);

        static void AddLog(string message)
        {
            float time = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
            log.AddFirst($"[{time:000.0}] {message}");
            while (log.Count > LogLength) log.RemoveLast();
        }

        internal static void Reset()
        {
            watches.Clear();
            plots.Clear();
            plotOrder.Clear();
            log.Clear();
            DirectorName = "-";
        }
    }
}
