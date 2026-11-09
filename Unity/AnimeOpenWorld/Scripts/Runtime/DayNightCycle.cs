using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeOpenWorld
{
    /// <summary>
    /// 昼夜サイクル。太陽の角度・色、環境光、フォグ色、空の明るさを時刻に合わせて変える。
    /// 夜は同じライトを月明かりとして使う。
    /// </summary>
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        public Light sun;

        [Header("時間")]
        [Range(0f, 24f)] public float timeOfDay = 9f;
        [Tooltip("ゲーム内 1 日の長さ (実時間・分)")]
        public float dayLengthMinutes = 12f;
        public bool advanceTime = true;
        [Tooltip("太陽が昇る方角")]
        public float sunYaw = -40f;

        [Header("太陽と月")]
        public float maxSunIntensity = 1.1f;
        public Gradient sunColor = MakeGradient(
            (0.00f, new Color(0.20f, 0.25f, 0.45f)),
            (0.25f, new Color(1.00f, 0.60f, 0.40f)),
            (0.33f, new Color(1.00f, 0.90f, 0.80f)),
            (0.50f, new Color(1.00f, 0.98f, 0.94f)),
            (0.70f, new Color(1.00f, 0.90f, 0.75f)),
            (0.77f, new Color(1.00f, 0.55f, 0.35f)),
            (1.00f, new Color(0.20f, 0.25f, 0.45f)));
        public Color moonColor = new Color(0.55f, 0.65f, 1f);
        public float moonIntensity = 0.25f;

        [Header("環境光・フォグ")]
        public Gradient ambientColor = MakeGradient(
            (0.00f, new Color(0.08f, 0.10f, 0.20f)),
            (0.25f, new Color(0.45f, 0.35f, 0.40f)),
            (0.35f, new Color(0.45f, 0.50f, 0.62f)),
            (0.50f, new Color(0.50f, 0.56f, 0.68f)),
            (0.75f, new Color(0.50f, 0.38f, 0.42f)),
            (0.85f, new Color(0.12f, 0.12f, 0.25f)),
            (1.00f, new Color(0.08f, 0.10f, 0.20f)));
        public Gradient fogColor = MakeGradient(
            (0.00f, new Color(0.05f, 0.07f, 0.15f)),
            (0.25f, new Color(0.90f, 0.62f, 0.52f)),
            (0.35f, new Color(0.72f, 0.84f, 0.96f)),
            (0.50f, new Color(0.66f, 0.82f, 0.97f)),
            (0.75f, new Color(0.95f, 0.62f, 0.47f)),
            (0.85f, new Color(0.10f, 0.10f, 0.20f)),
            (1.00f, new Color(0.05f, 0.07f, 0.15f)));

        static readonly int AmbientId = Shader.PropertyToID("_ToonAmbientColor");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        /// <summary>0〜1。昼なら 1、夜なら 0</summary>
        public float Daylight { get; private set; }

        void Update()
        {
            if (Application.isPlaying && advanceTime && dayLengthMinutes > 0f)
                timeOfDay = Mathf.Repeat(timeOfDay + 24f * Time.deltaTime / (dayLengthMinutes * 60f), 24f);
            Apply();
        }

        void OnValidate() => Apply();

        public void Apply()
        {
            float t = timeOfDay / 24f;
            float sunAngle = t * 360f - 90f; // 6 時 = 地平線、12 時 = 真上
            float elevation = Mathf.Sin(sunAngle * Mathf.Deg2Rad);
            Daylight = SmoothStep(-0.08f, 0.2f, elevation);

            if (sun != null)
            {
                if (elevation > -0.05f)
                {
                    sun.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);
                    sun.color = sunColor.Evaluate(t);
                    sun.intensity = maxSunIntensity * SmoothStep(-0.05f, 0.2f, elevation);
                }
                else
                {
                    // 夜: 反対側から月明かり
                    sun.transform.rotation = Quaternion.Euler(sunAngle + 180f, sunYaw, 0f);
                    sun.color = moonColor;
                    sun.intensity = moonIntensity * SmoothStep(0.05f, 0.3f, -elevation);
                }
                RenderSettings.sun = sun;
            }

            Color ambient = ambientColor.Evaluate(t);
            Shader.SetGlobalColor(AmbientId, ambient);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fogColor = fogColor.Evaluate(t);

            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty(ExposureId))
                sky.SetFloat(ExposureId, Mathf.Lerp(0.15f, 1.25f, Daylight));
        }

        static float SmoothStep(float a, float b, float x)
        {
            float k = Mathf.Clamp01((x - a) / (b - a));
            return k * k * (3f - 2f * k);
        }

        static Gradient MakeGradient(params (float time, Color color)[] keys)
        {
            var colorKeys = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);
            var g = new Gradient();
            g.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
