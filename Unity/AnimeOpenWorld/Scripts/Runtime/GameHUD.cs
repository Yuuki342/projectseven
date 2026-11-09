using UnityEngine;

namespace AnimeOpenWorld
{
    /// <summary>
    /// 仮の HUD (操作説明・時刻・スタミナゲージ)。
    /// 本格的な UI は UI Toolkit / uGUI で作り直す想定。
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public ThirdPersonController player;
        public DayNightCycle dayNight;
        public bool showHelp = true;

        GUIStyle label;
        GUIStyle clock;

        void Update()
        {
            if (GameInput.HelpPressed) showHelp = !showHelp;
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                label.normal.textColor = Color.white;
                clock = new GUIStyle(label) { fontSize = 22, alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            }

            if (showHelp)
            {
                GUI.Label(new Rect(12, 10, 640, 80),
                    "WASD: Move   Shift: Sprint   Space: Jump / Glide (in air)\n" +
                    "Mouse: Camera   Wheel: Zoom   Esc: Free cursor   Click: Lock cursor   F1: Hide help",
                    label);
            }

            if (dayNight != null)
            {
                int h = Mathf.FloorToInt(dayNight.timeOfDay);
                int m = Mathf.FloorToInt((dayNight.timeOfDay - h) * 60f);
                GUI.Label(new Rect(Screen.width - 132, 10, 120, 32), $"{h:00}:{m:00}", clock);
            }

            if (player != null && player.Stamina < player.maxStamina - 0.01f)
            {
                const float w = 220f, barH = 10f;
                float x = (Screen.width - w) * 0.5f, y = Screen.height - 60f;
                DrawRect(new Rect(x - 2, y - 2, w + 4, barH + 4), new Color(0f, 0f, 0f, 0.5f));
                Color c = player.IsExhausted ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.3f);
                DrawRect(new Rect(x, y, w * player.Stamina / player.maxStamina, barH), c);
            }
        }

        static void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
