using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeOpenWorld
{
    /// <summary>
    /// プレイヤーの周囲だけ地形チャンクを生成・破棄する無限オープンワールド。
    /// 地形はノイズから決定的に作られるので、同じ seed なら毎回同じ世界になる。
    /// 木・桜・岩もチャンクごとに 1 メッシュへまとめて描画負荷を抑える。
    /// </summary>
    public class WorldStreamer : MonoBehaviour
    {
        [Header("参照")]
        public Transform player;
        [Tooltip("ToonLit (頂点カラー ON・アウトライン OFF) 推奨")]
        public Material terrainMaterial;
        [Tooltip("ToonLit (頂点カラー ON) 推奨")]
        public Material propMaterial;

        [Header("チャンク")]
        public float chunkSize = 64f;
        [Range(8, 128)] public int chunkResolution = 48;
        [Tooltip("プレイヤーから何チャンク先まで読み込むか")]
        [Range(1, 12)] public int viewRadius = 5;
        [Tooltip("1 フレームに生成するチャンク数の上限 (カクつき対策)")]
        [Range(1, 8)] public int chunksPerFrame = 2;

        [Header("地形")]
        public int seed = 1234;
        public float seaLevel = 0f;
        public float continentScale = 0.0012f;
        public float baseHeight = 22f;
        public float hillScale = 0.006f;
        public float hillHeight = 14f;
        public float mountainScale = 0.0025f;
        public float mountainHeight = 90f;
        [Range(1, 8)] public int octaves = 4;

        [Header("地形の色 (頂点カラー)")]
        public Color sandColor = new Color(0.95f, 0.88f, 0.64f);
        public Color grassColorA = new Color(0.46f, 0.80f, 0.33f);
        public Color grassColorB = new Color(0.32f, 0.68f, 0.32f);
        public Color rockColor = new Color(0.58f, 0.55f, 0.60f);
        public Color snowColor = new Color(0.96f, 0.98f, 1f);
        public float snowHeight = 60f;

        [Header("配置物")]
        public int treeAttemptsPerChunk = 45;
        public int rocksPerChunk = 6;
        public Color trunkColor = new Color(0.47f, 0.32f, 0.24f);
        public Color leavesColorA = new Color(0.30f, 0.70f, 0.36f);
        public Color leavesColorB = new Color(0.56f, 0.84f, 0.36f);
        public Color blossomColor = new Color(1f, 0.74f, 0.84f);
        [Tooltip("木のうち桜になる割合")]
        [Range(0f, 1f)] public float blossomRatio = 0.15f;

        class Chunk
        {
            public GameObject gameObject;
            public Mesh terrainMesh;
            public Mesh propMesh;
        }

        readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        readonly List<Vector2Int> buildQueue = new List<Vector2Int>();
        Vector2Int currentCenter;
        bool hasCenter;
        Vector2 noiseOffset;

        // ---------------------------------------------------------------
        // ライフサイクル
        // ---------------------------------------------------------------

        void Awake()
        {
            var rng = new System.Random(seed);
            noiseOffset = new Vector2((float)rng.NextDouble() * 5000f + 1000f, (float)rng.NextDouble() * 5000f + 1000f);
            EnsureMaterials();
        }

        void Start()
        {
            if (player == null) return;

            // 足場がないまま落下しないよう、最初の周囲だけは即座に生成してから配置する
            Vector3 spawn = FindSpawnPoint(player.position);
            Vector2Int c = WorldToChunk(spawn);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var key = new Vector2Int(c.x + dx, c.y + dz);
                    if (!chunks.ContainsKey(key)) chunks[key] = BuildChunk(key);
                }

            var controller = player.GetComponent<ThirdPersonController>();
            if (controller != null)
            {
                controller.waterLevel = seaLevel;
                controller.Teleport(spawn);
            }
            else
            {
                player.position = spawn;
            }
        }

        void Update()
        {
            if (player == null) return;

            Vector2Int center = WorldToChunk(player.position);
            if (!hasCenter || center != currentCenter)
            {
                currentCenter = center;
                hasCenter = true;
                RebuildQueue();
                UnloadFarChunks();
            }

            int built = 0;
            while (built < chunksPerFrame && buildQueue.Count > 0)
            {
                Vector2Int key = buildQueue[0];
                buildQueue.RemoveAt(0);
                if (chunks.ContainsKey(key)) continue;
                chunks[key] = BuildChunk(key);
                built++;
            }
        }

        void OnDestroy()
        {
            foreach (var chunk in chunks.Values) DestroyChunk(chunk);
            chunks.Clear();
        }

        // ---------------------------------------------------------------
        // 地形の高さ (どこからでも呼べる)
        // ---------------------------------------------------------------

        /// <summary>ワールド座標 (x, z) の地面の高さ</summary>
        public float GetHeight(float x, float z)
        {
            x += noiseOffset.x;
            z += noiseOffset.y;

            float continent = Mathf.PerlinNoise(x * continentScale, z * continentScale);
            float hills = Fbm(x * hillScale, z * hillScale, octaves);
            float ridge = RidgedFbm(x * mountainScale, z * mountainScale, octaves);
            float mountainMask = SmoothStep(0.55f, 0.8f, continent);

            float h = (continent - 0.42f) * baseHeight * 2f      // 海・平原
                    + (hills - 0.5f) * hillHeight                // なだらかな丘
                    + ridge * ridge * mountainMask * mountainHeight; // 尾根のある山
            return seaLevel + h;
        }

        public Vector3 GetNormal(float x, float z, float eps = 1f)
        {
            float hL = GetHeight(x - eps, z), hR = GetHeight(x + eps, z);
            float hD = GetHeight(x, z - eps), hU = GetHeight(x, z + eps);
            return new Vector3(hL - hR, 2f * eps, hD - hU).normalized;
        }

        static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + i * 31.7f, z * freq + i * 17.3f) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        static float RidgedFbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq + i * 13.1f, z * freq + i * 47.9f) * 2f - 1f);
                sum += n * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------
        // ストリーミング
        // ---------------------------------------------------------------

        Vector2Int WorldToChunk(Vector3 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));

        void RebuildQueue()
        {
            buildQueue.Clear();
            float r2 = (viewRadius + 0.5f) * (viewRadius + 0.5f);
            for (int dz = -viewRadius; dz <= viewRadius; dz++)
                for (int dx = -viewRadius; dx <= viewRadius; dx++)
                {
                    if (dx * dx + dz * dz > r2) continue;
                    var key = new Vector2Int(currentCenter.x + dx, currentCenter.y + dz);
                    if (!chunks.ContainsKey(key)) buildQueue.Add(key);
                }
            // 近い順に作る
            buildQueue.Sort((a, b) => (a - currentCenter).sqrMagnitude.CompareTo((b - currentCenter).sqrMagnitude));
        }

        readonly List<Vector2Int> toRemove = new List<Vector2Int>();

        void UnloadFarChunks()
        {
            float r2 = (viewRadius + 1.5f) * (viewRadius + 1.5f);
            toRemove.Clear();
            foreach (var kv in chunks)
                if ((kv.Key - currentCenter).sqrMagnitude > r2) toRemove.Add(kv.Key);
            foreach (var key in toRemove)
            {
                DestroyChunk(chunks[key]);
                chunks.Remove(key);
            }
        }

        static void DestroyChunk(Chunk chunk)
        {
            if (chunk.gameObject != null) Destroy(chunk.gameObject);
            if (chunk.terrainMesh != null) Destroy(chunk.terrainMesh);
            if (chunk.propMesh != null) Destroy(chunk.propMesh);
        }

        Vector3 FindSpawnPoint(Vector3 around)
        {
            // 陸地で、かつ高すぎない場所を渦巻き状に探す
            for (int ring = 0; ring < 60; ring++)
            {
                float radius = ring * 24f;
                int steps = ring == 0 ? 1 : 12;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    float x = around.x + Mathf.Cos(a) * radius;
                    float z = around.z + Mathf.Sin(a) * radius;
                    float h = GetHeight(x, z);
                    if (h > seaLevel + 3f && h < seaLevel + 25f && GetNormal(x, z).y > 0.9f)
                        return new Vector3(x, h + 0.5f, z);
                }
            }
            return new Vector3(around.x, Mathf.Max(GetHeight(around.x, around.z), seaLevel) + 2f, around.z);
        }

        // ---------------------------------------------------------------
        // チャンク生成
        // ---------------------------------------------------------------

        Chunk BuildChunk(Vector2Int coord)
        {
            var go = new GameObject($"Chunk {coord.x},{coord.y}");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            Vector3 origin = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
            go.transform.position = origin;

            var chunk = new Chunk { gameObject = go };

            chunk.terrainMesh = BuildTerrainMesh(origin);
            go.AddComponent<MeshFilter>().sharedMesh = chunk.terrainMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
            go.AddComponent<MeshCollider>().sharedMesh = chunk.terrainMesh;

            chunk.propMesh = BuildProps(coord, origin, go);
            if (chunk.propMesh != null)
            {
                var props = new GameObject("Props");
                props.layer = go.layer;
                props.transform.SetParent(go.transform, false);
                props.AddComponent<MeshFilter>().sharedMesh = chunk.propMesh;
                props.AddComponent<MeshRenderer>().sharedMaterial = propMaterial;
            }
            return chunk;
        }

        Mesh BuildTerrainMesh(Vector3 origin)
        {
            int res = chunkResolution;
            int side = res + 1;
            float step = chunkSize / res;

            // 境界の外側 1 マス分も高さを取り、チャンク間で法線が途切れないようにする
            int hs = res + 3;
            var heights = new float[hs * hs];
            for (int z = 0; z < hs; z++)
                for (int x = 0; x < hs; x++)
                    heights[z * hs + x] = GetHeight(origin.x + (x - 1) * step, origin.z + (z - 1) * step);

            var vertices = new Vector3[side * side];
            var normals = new Vector3[side * side];
            var colors = new Color[side * side];
            var uvs = new Vector2[side * side];
            var triangles = new int[res * res * 6];

            for (int z = 0; z < side; z++)
                for (int x = 0; x < side; x++)
                {
                    int i = z * side + x;
                    int hi = (z + 1) * hs + (x + 1);
                    float h = heights[hi];
                    float hL = heights[hi - 1], hR = heights[hi + 1];
                    float hD = heights[hi - hs], hU = heights[hi + hs];
                    Vector3 n = new Vector3(hL - hR, 2f * step, hD - hU).normalized;

                    float wx = origin.x + x * step;
                    float wz = origin.z + z * step;
                    vertices[i] = new Vector3(x * step, h, z * step);
                    normals[i] = n;
                    colors[i] = TerrainColor(wx, h, wz, n);
                    uvs[i] = new Vector2(wx, wz) * 0.1f;
                }

            int t = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int i0 = z * side + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + side;
                    int i3 = i2 + 1;
                    triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                    triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
                }

            var mesh = new Mesh { name = "Terrain Chunk" };
            if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        Color TerrainColor(float x, float h, float z, Vector3 n)
        {
            float slope = 1f - n.y;
            float v = Mathf.PerlinNoise((x + noiseOffset.x) * 0.03f, (z + noiseOffset.y) * 0.03f);

            // アニメ背景っぽく、境界がくっきりした色分け
            Color c = Color.Lerp(grassColorA, grassColorB, SmoothStep(0.45f, 0.55f, v));

            float beach = seaLevel + 1.5f + (v - 0.5f) * 1.5f;
            if (h < beach) c = h < seaLevel - 0.5f ? sandColor * 0.85f : sandColor;

            c = Color.Lerp(c, rockColor, SmoothStep(0.30f, 0.36f, slope));

            float snowLine = snowHeight + (v - 0.5f) * 10f;
            if (h > snowLine && slope < 0.5f) c = snowColor;

            c.a = 1f;
            return c;
        }

        // ---------------------------------------------------------------
        // 木・桜・岩
        // ---------------------------------------------------------------

        Mesh BuildProps(Vector2Int coord, Vector3 origin, GameObject chunkObject)
        {
            var rng = new System.Random(unchecked(seed * 73856093 ^ coord.x * 19349663 ^ coord.y * 83492791));
            var builder = new MeshBuilder();

            for (int i = 0; i < treeAttemptsPerChunk; i++)
            {
                float lx = (float)rng.NextDouble() * chunkSize;
                float lz = (float)rng.NextDouble() * chunkSize;
                float scale = Mathf.Lerp(0.8f, 1.4f, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;
                bool blossom = rng.NextDouble() < blossomRatio;
                float colorT = (float)rng.NextDouble();
                Vector2 jitter = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());

                float wx = origin.x + lx, wz = origin.z + lz;
                float h = GetHeight(wx, wz);
                if (h < seaLevel + 2f || h > snowHeight - 8f) continue;
                if (GetNormal(wx, wz).y < 0.85f) continue;
                // 森の分布 (まばらな場所と密な場所を作る)
                float forest = Mathf.PerlinNoise((wx + noiseOffset.x) * 0.008f + 100f, (wz + noiseOffset.y) * 0.008f);
                if (forest < 0.45f) continue;

                Vector3 basePos = new Vector3(lx, h - 0.2f, lz);
                AddTree(builder, basePos, scale, yaw, blossom, colorT, jitter);

                var col = chunkObject.AddComponent<CapsuleCollider>();
                float trunkHeight = 2.6f * scale;
                col.center = basePos + Vector3.up * (trunkHeight * 0.5f);
                col.radius = 0.25f * scale;
                col.height = trunkHeight;
            }

            for (int i = 0; i < rocksPerChunk; i++)
            {
                float lx = (float)rng.NextDouble() * chunkSize;
                float lz = (float)rng.NextDouble() * chunkSize;
                Vector3 size = new Vector3(
                    Mathf.Lerp(0.8f, 2.6f, (float)rng.NextDouble()),
                    Mathf.Lerp(0.5f, 1.6f, (float)rng.NextDouble()),
                    Mathf.Lerp(0.8f, 2.6f, (float)rng.NextDouble()));
                Quaternion rot = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f);
                float shade = Mathf.Lerp(0.85f, 1.05f, (float)rng.NextDouble());

                float wx = origin.x + lx, wz = origin.z + lz;
                float h = GetHeight(wx, wz);
                if (h < seaLevel - 1f) continue;

                Vector3 pos = new Vector3(lx, h + size.y * 0.2f, lz);
                builder.Add(PrimitiveCache.Get(PrimitiveType.Cube), Matrix4x4.TRS(pos, rot, size), rockColor * shade);

                var rockCol = new GameObject("Rock Collider");
                rockCol.layer = chunkObject.layer;
                rockCol.transform.SetParent(chunkObject.transform, false);
                rockCol.transform.localPosition = pos;
                rockCol.transform.localRotation = rot;
                rockCol.transform.localScale = size;
                rockCol.AddComponent<BoxCollider>();
            }

            return builder.IsEmpty ? null : builder.ToMesh("Props Chunk");
        }

        void AddTree(MeshBuilder b, Vector3 basePos, float s, float yaw, bool blossom, float colorT, Vector2 jitter)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            float trunkHeight = 2.6f * s;
            float trunkRadius = 0.22f * s;

            // Unity の Cylinder は高さ 2・直径 1
            b.Add(PrimitiveCache.Get(PrimitiveType.Cylinder),
                Matrix4x4.TRS(basePos + Vector3.up * (trunkHeight * 0.5f), rot, new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f)),
                trunkColor);

            Color leaf = blossom ? blossomColor : Color.Lerp(leavesColorA, leavesColorB, colorT);
            AddSphere(b, basePos + Vector3.up * (trunkHeight + 0.6f * s), 3.0f * s, leaf);
            Vector3 side1 = rot * new Vector3(0.9f, 0f, 0.2f + jitter.x * 0.4f) * s;
            Vector3 side2 = rot * new Vector3(-0.8f, 0f, -0.3f - jitter.y * 0.4f) * s;
            AddSphere(b, basePos + Vector3.up * (trunkHeight + 0.1f * s) + side1, 2.0f * s, leaf * 0.94f);
            AddSphere(b, basePos + Vector3.up * (trunkHeight + 0.3f * s) + side2, 2.2f * s, leaf * 0.97f);
        }

        static void AddSphere(MeshBuilder b, Vector3 center, float diameter, Color color)
        {
            color.a = 1f;
            b.Add(PrimitiveCache.Get(PrimitiveType.Sphere), Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * diameter), color);
        }

        void EnsureMaterials()
        {
            if (terrainMaterial != null && propMaterial != null) return;
            var shader = Shader.Find("AnimeOpenWorld/ToonLit");
            if (shader == null)
            {
                Debug.LogError("[WorldStreamer] AnimeOpenWorld/ToonLit シェーダーが見つかりません。マテリアルを割り当ててください。");
                return;
            }
            if (terrainMaterial == null)
            {
                terrainMaterial = new Material(shader) { name = "Terrain (runtime)" };
                terrainMaterial.SetFloat("_UseVertexColor", 1f);
                terrainMaterial.SetFloat("_HighlightSize", 0f);
                terrainMaterial.SetFloat("_RimStrength", 0f);
                terrainMaterial.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }
            if (propMaterial == null)
            {
                propMaterial = new Material(shader) { name = "Props (runtime)" };
                propMaterial.SetFloat("_UseVertexColor", 1f);
                propMaterial.SetFloat("_HighlightSize", 0f);
            }
        }

        // ---------------------------------------------------------------
        // メッシュ結合ヘルパー
        // ---------------------------------------------------------------

        class MeshBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<int> triangles = new List<int>();

            public bool IsEmpty => vertices.Count == 0;

            public void Add(PrimitiveCache.Data src, Matrix4x4 matrix, Color color)
            {
                int start = vertices.Count;
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                for (int i = 0; i < src.vertices.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(src.vertices[i]));
                    normals.Add(normalMatrix.MultiplyVector(src.normals[i]).normalized);
                    colors.Add(color);
                }
                for (int i = 0; i < src.triangles.Length; i++) triangles.Add(start + src.triangles[i]);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static class PrimitiveCache
        {
            public class Data
            {
                public Vector3[] vertices;
                public Vector3[] normals;
                public int[] triangles;
            }

            static readonly Dictionary<PrimitiveType, Data> cache = new Dictionary<PrimitiveType, Data>();

            public static Data Get(PrimitiveType type)
            {
                if (cache.TryGetValue(type, out var data)) return data;
                var go = GameObject.CreatePrimitive(type);
                go.SetActive(false);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                data = new Data { vertices = mesh.vertices, normals = mesh.normals, triangles = mesh.triangles };
                UnityEngine.Object.Destroy(go);
                cache[type] = data;
                return data;
            }
        }
    }
}
