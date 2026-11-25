using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieFPS
{
    /// <summary>Player HUD, built at runtime.</summary>
    public class Hud : MonoBehaviour
    {
        const string ControlsHelp =
            "WASD/ZQSD move  ·  Shift sprint  ·  Space jump  ·  LMB fire  ·  R reload  ·  1-3 / wheel weapons  ·  G grenade  ·  F flashlight  ·  F1 debug  ·  Esc pause";

        static Hud instance;

        Font font;
        Image healthFill;
        Text healthText;
        Text weaponText;
        Text ammoText;
        Text grenadeText;
        Text zoneText;
        Image progressFill;
        Text messageText;
        Text feedText;
        Text centerText;
        Text helpText;
        Text hitMarker;
        Image damageOverlay;
        Image adrenalineOverlay;
        Image reloadFill;

        float messageEnd;
        float damageAlpha;
        float hitMarkerEnd;
        readonly List<(string text, float end)> feed = new List<(string, float)>();
        readonly StringBuilder builder = new StringBuilder();

        void Awake()
        {
            instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------------------------------------------------------------- Static API

        public static void ShowMessage(string text, float duration)
        {
            if (instance == null) return;
            instance.messageText.text = text;
            instance.messageEnd = Time.time + duration;
        }

        public static void ShowPickup(string text)
        {
            if (instance == null) return;
            instance.feed.Add((text, Time.time + 2.5f));
        }

        public static void OnPlayerDamaged(float amount)
        {
            if (instance == null) return;
            instance.damageAlpha = Mathf.Min(0.6f, instance.damageAlpha + 0.15f + amount * 0.01f);
        }

        public static void ShowHitMarker(bool headshot)
        {
            if (instance == null) return;
            instance.hitMarker.color = headshot ? new Color(1f, 0.3f, 0.2f) : Color.white;
            instance.hitMarkerEnd = Time.time + 0.12f;
        }

        // ---------------------------------------------------------------- Update

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            PlayerController player = gm.player;

            float health = player.Health.Current;
            float healthRatio = health / player.Health.maxHealth;
            SetFill(healthFill, healthRatio);
            healthFill.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.3f, 0.85f, 0.3f), healthRatio);
            healthText.text = Mathf.CeilToInt(health).ToString();

            WeaponSlot weapon = player.Weapons.Current;
            weaponText.text = weapon.displayName;
            ammoText.text = weapon.infiniteReserve ? $"{weapon.magazine} / ∞" : $"{weapon.magazine} / {weapon.reserve}";
            grenadeText.text = $"Grenades  {player.Weapons.Grenades}";
            SetFill(reloadFill, player.Weapons.ReloadProgress);

            Level level = gm.level;
            SetFill(progressFill, level.GetProgress(player.transform.position));
            zoneText.text = level.GetZoneName(gm.CurrentZone);

            messageText.enabled = Time.time < messageEnd;
            hitMarker.enabled = Time.time < hitMarkerEnd;

            builder.Clear();
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                if (Time.time > feed[i].end) feed.RemoveAt(i);
            }
            foreach ((string text, float _) in feed) builder.AppendLine(text);
            feedText.text = builder.ToString();

            damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.unscaledDeltaTime * 0.8f);
            float lowHealth = health < 30f ? 0.15f + Mathf.Sin(Time.time * 5f) * 0.05f : 0f;
            damageOverlay.color = new Color(0.6f, 0f, 0f, Mathf.Max(damageAlpha, lowHealth));

            bool adrenaline = gm.worldEvents.IsActive(WorldEventType.Adrenaline);
            adrenalineOverlay.color = new Color(1f, 0.8f, 0.2f, adrenaline ? 0.06f + Mathf.Sin(Time.time * 8f) * 0.02f : 0f);

            helpText.enabled = gm.GameTime < 15f || gm.State == GameState.Paused;
            UpdateCenterText(gm);
        }

        void UpdateCenterText(GameManager gm)
        {
            switch (gm.State)
            {
                case GameState.Paused:
                    centerText.text = "PAUSED\n\n<size=22>Esc  resume     R  restart</size>";
                    break;
                case GameState.Victory:
                case GameState.Defeat:
                    GameStats stats = gm.Stats;
                    string title = gm.State == GameState.Victory ? "<color=#7CFC7C>YOU MADE IT</color>" : "<color=#FF5040>YOU DIED</color>";
                    centerText.text =
                        $"{title}\n\n<size=22>Time {gm.GameTime:0}s   ·   Kills {stats.TotalKills} ({stats.TotalHeadshots} headshots)   ·   " +
                        $"Accuracy {stats.Accuracy * 100f:0}%   ·   Damage taken {stats.TotalDamageTaken:0}\n\nPress R to restart</size>";
                    break;
                default:
                    centerText.text = string.Empty;
                    break;
            }
        }

        static void SetFill(Image image, float amount)
        {
            image.rectTransform.localScale = new Vector3(Mathf.Clamp01(amount), 1f, 1f);
        }

        // ---------------------------------------------------------------- Construction

        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = canvasGo.GetComponent<RectTransform>();

            damageOverlay = CreateImage(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            adrenalineOverlay = CreateImage(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);

            // Crosshair & hit marker
            CreateText(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), "+", 32, TextAnchor.MiddleCenter);
            hitMarker = CreateText(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), "×", 44, TextAnchor.MiddleCenter);
            reloadFill = CreateImage(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-40f, -40f), new Vector2(40f, -34f), new Color(1f, 1f, 1f, 0.8f), true);

            // Health (bottom left)
            CreateImage(root, Vector2.zero, Vector2.zero, new Vector2(40f, 40f), new Vector2(440f, 72f), new Color(0f, 0f, 0f, 0.5f));
            healthFill = CreateImage(root, Vector2.zero, Vector2.zero, new Vector2(44f, 44f), new Vector2(436f, 68f), Color.green, true);
            healthText = CreateText(root, Vector2.zero, new Vector2(240f, 100f), new Vector2(400f, 40f), "100", 30, TextAnchor.MiddleLeft);

            // Weapon (bottom right)
            weaponText = CreateText(root, new Vector2(1f, 0f), new Vector2(-240f, 130f), new Vector2(400f, 36f), "", 26, TextAnchor.MiddleRight);
            ammoText = CreateText(root, new Vector2(1f, 0f), new Vector2(-240f, 85f), new Vector2(400f, 50f), "", 44, TextAnchor.MiddleRight);
            grenadeText = CreateText(root, new Vector2(1f, 0f), new Vector2(-240f, 45f), new Vector2(400f, 30f), "", 22, TextAnchor.MiddleRight);

            // Progress (top)
            CreateImage(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-300f, -34f), new Vector2(300f, -26f), new Color(0f, 0f, 0f, 0.5f));
            progressFill = CreateImage(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-300f, -34f), new Vector2(300f, -26f), new Color(0.9f, 0.85f, 0.6f, 0.9f), true);
            zoneText = CreateText(root, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(600f, 30f), "", 22, TextAnchor.MiddleCenter);

            // Messages
            messageText = CreateText(root, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 60f), "", 40, TextAnchor.MiddleCenter);
            feedText = CreateText(root, new Vector2(1f, 0f), new Vector2(-240f, 260f), new Vector2(400f, 120f), "", 22, TextAnchor.LowerRight);
            centerText = CreateText(root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600f, 400f), "", 64, TextAnchor.MiddleCenter);
            helpText = CreateText(root, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1800f, 30f), ControlsHelp, 18, TextAnchor.MiddleCenter);
        }

        Text CreateText(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        static Image CreateImage(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color, bool leftPivot = false)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (leftPivot) rect.pivot = new Vector2(0f, 0.5f);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
