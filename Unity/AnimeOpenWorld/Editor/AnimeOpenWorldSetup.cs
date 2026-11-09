using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeOpenWorld.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Anime Open World > Create Demo Scene」で
    /// マテリアル・プレイヤー・カメラ・ワールド・昼夜サイクルを一式組み立てたデモシーンを作る。
    /// </summary>
    public static class AnimeOpenWorldSetup
    {
        const string RootFolder = "Assets/AnimeOpenWorld";
        const string GeneratedFolder = RootFolder + "/Generated";
        const string SceneFolder = RootFolder + "/Scenes";
        const string ScenePath = SceneFolder + "/AnimeOpenWorldDemo.unity";

        [MenuItem("Tools/Anime Open World/Create Demo Scene", priority = 0)]
        public static void CreateDemoScene()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null || !pipeline.GetType().Name.Contains("Universal"))
            {
                EditorUtility.DisplayDialog("Anime Open World",
                    "URP (Universal Render Pipeline) が有効になっていません。\n\n" +
                    "Unity Hub で「Universal 3D」テンプレートからプロジェクトを作成し、そこにこのフォルダをコピーしてください。",
                    "OK");
                return;
            }

            Shader toon = Shader.Find("AnimeOpenWorld/ToonLit");
            Shader water = Shader.Find("AnimeOpenWorld/ToonWater");
            Shader sky = Shader.Find("Skybox/Procedural");
            if (toon == null || water == null)
            {
                EditorUtility.DisplayDialog("Anime Open World", "シェーダーが見つかりません。コンソールのコンパイルエラーを確認してください。", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(GeneratedFolder);
            EnsureFolder(SceneFolder);

            // ---- マテリアル ----
            var terrainMat = CreateMaterial("Terrain", toon, m =>
            {
                m.SetFloat("_UseVertexColor", 1f);
                m.SetColor("_ShadeColor", new Color(0.72f, 0.70f, 0.86f));
                m.SetFloat("_HighlightSize", 0f);
                m.SetFloat("_RimStrength", 0f);
                m.SetFloat("_OutlineWidth", 0f);
                m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            });
            var propMat = CreateMaterial("Props", toon, m =>
            {
                m.SetFloat("_UseVertexColor", 1f);
                m.SetFloat("_HighlightSize", 0f);
                m.SetFloat("_RimStrength", 0.15f);
                m.SetFloat("_OutlineWidth", 1.2f);
            });
            var outfitMat = CreateMaterial("PlayerOutfit", toon, m =>
            {
                m.SetColor("_BaseColor", new Color(0.28f, 0.38f, 0.80f));
                m.SetFloat("_HighlightSize", 0.03f);
            });
            var skinMat = CreateMaterial("PlayerSkin", toon, m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 0.88f, 0.80f));
                m.SetColor("_ShadeColor", new Color(0.95f, 0.72f, 0.72f));
                m.SetFloat("_HighlightSize", 0f);
            });
            var hairMat = CreateMaterial("PlayerHair", toon, m =>
            {
                m.SetColor("_BaseColor", new Color(0.98f, 0.62f, 0.72f));
                m.SetColor("_ShadeColor", new Color(0.75f, 0.55f, 0.80f));
                m.SetFloat("_HighlightSize", 0.06f);
                m.SetFloat("_RimStrength", 0.4f);
            });
            var eyeMat = CreateMaterial("PlayerEye", toon, m =>
            {
                m.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.22f));
                m.SetFloat("_OutlineWidth", 0f);
                m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            });
            var gliderMat = CreateMaterial("Glider", toon, m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 0.95f, 0.85f));
            });
            var waterMat = CreateMaterial("Water", water, null);
            Material skyMat = null;
            if (sky != null)
            {
                skyMat = CreateMaterial("Sky", sky, m =>
                {
                    m.SetFloat("_SunSize", 0.06f);
                    m.SetFloat("_AtmosphereThickness", 0.8f);
                    m.SetColor("_SkyTint", new Color(0.45f, 0.65f, 1f));
                    m.SetFloat("_Exposure", 1.2f);
                });
            }

            // ---- シーン ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.1f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            RenderSettings.sun = sun;
            if (skyMat != null) RenderSettings.skybox = skyMat;

            // ワールド
            var worldGo = new GameObject("World");
            var world = worldGo.AddComponent<WorldStreamer>();
            world.terrainMaterial = terrainMat;
            world.propMaterial = propMat;

            // フォグでチャンクの読み込み境界を隠す
            float viewDistance = world.viewRadius * world.chunkSize;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = viewDistance * 0.45f;
            RenderSettings.fogEndDistance = viewDistance - 10f;

            // プレイヤー
            var player = CreatePlayer(outfitMat, skinMat, hairMat, eyeMat, gliderMat);
            var controller = player.GetComponent<ThirdPersonController>();
            world.player = player.transform;

            // カメラ
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = viewDistance + 200f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = player.transform.position + new Vector3(0f, 3f, -6f);
            var orbit = camGo.AddComponent<OrbitCamera>();
            orbit.target = player.transform;
            controller.cameraTransform = camGo.transform;

            // 海
            var waterGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterGo.name = "Water";
            UnityEngine.Object.DestroyImmediate(waterGo.GetComponent<Collider>());
            var waterRenderer = waterGo.GetComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = waterMat;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
            float waterSize = (world.viewRadius + 2) * world.chunkSize * 2f / 10f; // Plane は 10m 四方
            waterGo.transform.localScale = new Vector3(waterSize, 1f, waterSize);
            var follower = waterGo.AddComponent<WaterFollower>();
            follower.target = player.transform;
            follower.world = world;

            // 昼夜
            var dayNight = new GameObject("Day Night Cycle").AddComponent<DayNightCycle>();
            dayNight.sun = sun;
            dayNight.Apply();

            // HUD
            var hud = new GameObject("HUD").AddComponent<GameHUD>();
            hud.player = controller;
            hud.dayNight = dayNight;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;

            EditorUtility.DisplayDialog("Anime Open World",
                "デモシーンを作成しました。\n" + ScenePath + "\n\n▶ (Play) を押して遊んでみてください。", "OK");
        }

        static GameObject CreatePlayer(Material outfit, Material skin, Material hair, Material eye, Material glider)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast"); // カメラや接地判定のレイに当たらないように

            var root = new GameObject("Player") { layer = layer };
            root.transform.position = new Vector3(0f, 30f, 0f); // 実行時に WorldStreamer が陸地へ移動させる

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.05f;

            var controller = root.AddComponent<ThirdPersonController>();

            // 仮キャラ (プリミティブ製)。VRM などのモデルに差し替える想定
            var visual = new GameObject("Visual") { layer = layer };
            visual.transform.SetParent(root.transform, false);

            Part(PrimitiveType.Capsule, "Body", visual.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 0.6f, 0.5f), outfit, layer);
            Part(PrimitiveType.Sphere, "Head", visual.transform, new Vector3(0f, 1.38f, 0f), Vector3.one * 0.5f, skin, layer);
            Part(PrimitiveType.Sphere, "Hair", visual.transform, new Vector3(0f, 1.47f, -0.07f), new Vector3(0.54f, 0.48f, 0.52f), hair, layer);
            Part(PrimitiveType.Sphere, "Ponytail", visual.transform, new Vector3(0f, 1.38f, -0.33f), new Vector3(0.22f, 0.36f, 0.22f), hair, layer);
            Part(PrimitiveType.Sphere, "Eye L", visual.transform, new Vector3(-0.09f, 1.38f, 0.235f), new Vector3(0.07f, 0.1f, 0.04f), eye, layer);
            Part(PrimitiveType.Sphere, "Eye R", visual.transform, new Vector3(0.09f, 1.38f, 0.235f), new Vector3(0.07f, 0.1f, 0.04f), eye, layer);

            var gliderGo = Part(PrimitiveType.Cube, "Glider", visual.transform, new Vector3(0f, 2.1f, 0f), new Vector3(2.4f, 0.05f, 0.9f), glider, layer);
            gliderGo.SetActive(false);
            controller.gliderVisual = gliderGo;

            return root;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material CreateMaterial(string name, Shader shader, Action<Material> configure)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else mat.shader = shader;
            mat.name = name;
            configure?.Invoke(mat);
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
