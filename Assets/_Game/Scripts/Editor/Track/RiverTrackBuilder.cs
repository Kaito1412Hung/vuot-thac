using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;
using Debug = UnityEngine.Debug;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Dựng địa hình, mặt nước, đồ trang trí và công trình từ một <see cref="RiverTrack"/>.
    /// Mọi thứ nằm trong GameObject con tên "Generated", và bị xóa rồi dựng lại mỗi lần bấm nút.
    /// Mesh được lưu vào Assets/_Game/Generated để scene không phải chứa dữ liệu mesh.
    /// </summary>
    public static partial class RiverTrackBuilder
    {
        public const string GeneratedFolder = "Assets/_Game/Generated";
        const string RootName = "Generated";
        const int ChunkCells = 32;
        const float WaterStep = 2f;
        const int WaterColumns = 4;

        /// <summary>Dữ liệu dùng chung trong một lần dựng.</summary>
        class BuildContext
        {
            public RiverTrack Track;
            public TrackMaterials Mats;
            public PropLibrary Props;
            public System.Random Rng;
            public List<Mesh> Meshes = new();
            /// <summary>Vùng cấm đặt đồ trang trí: x, z là tâm, w là bán kính.</summary>
            public List<Vector3> Exclusions = new();
            public Dictionary<string, int> Counts = new();

            public void Exclude(Vector3 p, float radius) => Exclusions.Add(new Vector3(p.x, p.z, radius));

            public bool IsExcluded(Vector3 p, float extra = 0f)
            {
                foreach (var e in Exclusions)
                {
                    float dx = e.x - p.x, dz = e.y - p.z, r = e.z + extra;
                    if (dx * dx + dz * dz < r * r)
                        return true;
                }
                return false;
            }

            public void Count(string key, int n) => Counts[key] = n;
        }

        public static void Build(RiverTrack track)
        {
            var timer = Stopwatch.StartNew();

            if (track.transform.lossyScale != Vector3.one)
                Debug.LogWarning("[Vượt Thác] RiverTrack nên có scale bằng 1, nếu không địa hình sẽ lệch khỏi spline.", track);

            track.InvalidateCache();
            Directory.CreateDirectory(GeneratedFolder);

            var ctx = new BuildContext
            {
                Track = track,
                Mats = TrackMaterials.LoadOrCreate(),
                Props = PropLibrary.LoadOrCreate(),
                Rng = new System.Random(track.seed),
            };

            FloralSurfaceMaterials.Setup();
            NaturalSurfaceMaterials.Setup();

            var old = track.transform.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(track.transform, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            track.appliedRaceStripWidth=0;
            // Biết trước đoạn nhánh nào đi ngầm trong núi, vì mọi bước đào đất và dựng núi phía sau đều cần
            PrepareBranchUnderground(ctx);
            PrepareMountainWaterfall(ctx);
            PrepareForestCave(ctx);
            DisableCavesOverBranches(ctx);
            PrepareBranches(ctx);
            PrepareFeatures(ctx);
            BuildTerrain(ctx, Group(root, "Terrain"));
            var waterGroup = Group(root, "Water");
            BuildWater(ctx, waterGroup);
            BuildBranchWater(ctx, waterGroup);

            // Công trình đặt trước để đồ trang trí tránh chỗ của chúng
            BuildGates(ctx, Group(root, "Gates"));
            if (track.cabin) BuildCabin(ctx, Group(root, "Cabin"));
            if (track.bridge) BuildBridge(ctx, Group(root, "Bridge"));
            if (track.turnSigns) BuildTurnSigns(ctx, Group(root, "Signs"));
            if (track.village) BuildVillages(ctx, Group(root, "Villages"));
            BuildBranchDetails(ctx, Group(root, "Branches"));

            BuildLandmarks(ctx, Group(root, "Landmarks"));
            BuildRiversideHomes(ctx, Group(root, "RiversideHomes"));
            BuildGardenDetails(ctx, Group(root, "Gardens"));
            BuildVillageFinish(ctx, Group(root, "VillageFinish"));
            BuildScenicDetails(ctx, Group(root, "ScenicDetails"));
            BuildVillagers(ctx, Group(root, "Villagers"));
            BuildFarmyards(ctx, Group(root, "Farmyards"));
            BuildRiverRocks(ctx, Group(root, "RiverRocks"));
            BuildBankRocks(ctx, Group(root, "BankRocks"));
            BuildTrees(ctx, Group(root, "Trees"));
            BuildDeepForest(ctx, Group(root, "DeepForest"));
            BuildBushes(ctx, Group(root, "Bushes"));
            BuildFlowers(ctx, Group(root, "Flowers"));
            BuildMeadowGrass(ctx, Group(root, "MeadowGrass"));
            BuildLogs(ctx, Group(root, "Logs"));
            BuildReeds(ctx, Group(root, "Reeds"));
            BuildLilyPads(ctx, Group(root, "LilyPads"));
            if (track.mountains)
            {
                BuildMountains(ctx, Group(root, "Mountains"));
                BuildMountainForest(ctx, Group(root, "MountainForest"));
            }
            BuildForestCave(ctx, Group(root,"ForestCave"));
            BuildWaterfall(ctx, Group(root, "MountainWaterfall"));
            BuildClouds(ctx, Group(root, "Clouds"));
            if (track.animals)
            {
                BuildAnimals(ctx, Group(root, "Animals"));
                BuildBirds(ctx, Group(root, "Birds"));
            }

            if(track.insertedRaceStripWidth>0)RaceStripInsertion.Apply(track,track.insertedRaceStripWidth,ctx.Meshes);
            // Hầm và chi tiết thác dựng sau khi mở rộng lòng sông để ôm đúng mép nước thật
            BuildTunnels(ctx, Group(root, "Tunnels"));
            BuildBranchCaves(ctx, Group(root, "BranchCaves"));
            BuildBarriers(ctx, Group(root, "Barriers"));
            BuildFallDetails(ctx, Group(root, "Falls"));
            DryBuildingPlacement.Repair(track);
            WaterJunctionRepair.Repair(track);
            JunctionFillets.Build(track);
            // Giữ bản đất/núi trước khi các bước làm khớp bờ và khoét hang chia nhỏ lưới,
            // để cuối cùng gộp lại những vùng chia thừa (xem TerrainMeshCompactor)
            var terrainBeforeSubdivision = SnapshotGround(track);
            ShorelineFinish.Repair(track);
            MajesticCaverns.Build(track);
            CaveInteriorThemes.Apply(track);
            MountainCaveAccess.Build(track);
            CaveAccessFinisher.Finish(track);
            CaveDoorClearance.Clear(track);
            CaveLightBudget.Apply(track);
            ForestDistribution.Balance(track);
            Debug.Log("[Vượt Thác] Gộp lưới đất: " + TerrainMeshCompactor.Run(track, terrainBeforeSubdivision, true).Split('\n')[0]);
            Debug.Log("[Vượt Thác] Bịt chỗ nối chữ T: " + TerrainMeshCompactor.CloseAll(track, true).Split('\n')[0]);
            foreach (var copy in terrainBeforeSubdivision.Values) Object.DestroyImmediate(copy);
            SaveMeshes(track, ctx.Meshes);
            SetStatic(root.gameObject);
            EditorSceneManager.MarkSceneDirty(track.gameObject.scene);

            var parts = new List<string>();
            foreach (var kv in ctx.Counts)
                parts.Add($"{kv.Key} {kv.Value}");
            Debug.Log($"[Vượt Thác] Dựng xong '{track.name}' trong {timer.ElapsedMilliseconds} ms. " +
                      $"Sông dài {track.Length:0} m, đường đua {track.RaceLength:0} m. {string.Join(", ", parts)}.", track);
        }

        // ---------- Địa hình ----------

        static void BuildTerrain(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            float g = track.gridSize;
            float margin = track.terrainMargin;
            Bounds b = track.GetCenterlineBounds();

            float minX = b.min.x - margin - g;
            float minZ = b.min.z - margin - g;
            int nx = Mathf.CeilToInt((b.size.x + 2f * (margin + g)) / g) + 1;
            int nz = Mathf.CeilToInt((b.size.z + 2f * (margin + g)) / g) + 1;

            var pos = new Vector3[nx * nz];
            var dist = new float[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    // Lệch đỉnh một chút để lưới không thẳng hàng như bàn cờ
                    float jx = (Hash(ix, iz, track.seed) - 0.5f) * g * 0.4f;
                    float jz = (Hash(ix, iz, track.seed + 7) - 0.5f) * g * 0.4f;
                    var p = new Vector3(minX + ix * g + jx, 0f, minZ + iz * g + jz);
                    p.y = track.TerrainHeight(p, out float d);
                    int i = iz * nx + ix;
                    pos[i] = p;
                    dist[i] = d;
                }
            }

            float sandLimit = track.HalfWidth + track.bankWidth * 0.8f;
            float noiseOffset = (track.seed % 977) * 0.71f;
            var mats = ctx.Mats;
            var materials = new[] { mats.Sand, mats.Grass, mats.Cliff, mats.GrassDark };
            int totalTris = 0;

            void AddTri(FlatMeshBuilder fb, int ia, int ib, int ic)
            {
                Vector3 a = pos[ia], bb = pos[ib], c = pos[ic];
                float avgDist = (dist[ia] + dist[ib] + dist[ic]) / 3f;
                float up = Mathf.Abs(Vector3.Cross(bb - a, c - a).normalized.y);
                int sub = up < 0.72f ? 2 : (avgDist < sandLimit ? 0 : 1);
                if (sub == 1)
                {
                    // Mảng cỏ đậm nhạt xen kẽ cho đỡ đơn điệu
                    Vector3 m = (a + bb + c) / 3f;
                    if (Mathf.PerlinNoise(m.x * 0.035f + noiseOffset, m.z * 0.035f + noiseOffset) > 0.56f)
                        sub = 3;
                }
                fb.TriUp(sub, a, bb, c);
            }

            for (int cz = 0; cz * ChunkCells < nz - 1; cz++)
            {
                for (int cx = 0; cx * ChunkCells < nx - 1; cx++)
                {
                    var fb = new FlatMeshBuilder(4);
                    int zEnd = Mathf.Min((cz + 1) * ChunkCells, nz - 1);
                    int xEnd = Mathf.Min((cx + 1) * ChunkCells, nx - 1);

                    for (int iz = cz * ChunkCells; iz < zEnd; iz++)
                    {
                        for (int ix = cx * ChunkCells; ix < xEnd; ix++)
                        {
                            int i00 = iz * nx + ix, i10 = i00 + 1, i01 = i00 + nx, i11 = i01 + 1;
                            if (dist[i00] > margin && dist[i10] > margin && dist[i01] > margin && dist[i11] > margin)
                                continue;

                            if (((ix + iz) & 1) == 0)
                            {
                                AddTri(fb, i00, i01, i11);
                                AddTri(fb, i00, i11, i10);
                            }
                            else
                            {
                                AddTri(fb, i00, i01, i10);
                                AddTri(fb, i10, i01, i11);
                            }
                        }
                    }

                    if (fb.IsEmpty)
                        continue;

                    var mesh = fb.ToMesh($"{track.name}_Terrain_{cx}_{cz}");
                    ctx.Meshes.Add(mesh);
                    totalTris += fb.VertexCount / 3;

                    var go = CreateMeshObject($"Terrain_{cx}_{cz}", mesh, materials, parent);
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            }

            ctx.Count("tam giác địa hình", totalTris);
        }

        // ---------- Mặt nước ----------

        static void BuildWater(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            float extent = track.HalfWidth + track.waterSurfaceExtraWidth;
            int cols = WaterColumns + 1;

            // Lấy mẫu dày hơn ở thác để mặt nước đổ xuống mượt, không gãy khúc
            var rowDistances = new List<float>();
            for (float d = 0f; d < track.Length; )
            {
                rowDistances.Add(d);
                d += track.IsNearFeature(d, 4f) && FallNear(track, d) ? 0.6f : WaterStep;
            }
            rowDistances.Add(track.Length);
            int rows = rowDistances.Count;

            var verts = new List<Vector3>(rows * cols);
            var normals = new List<Vector3>(rows * cols);
            var uvs = new List<Vector2>(rows * cols);
            var tris = new List<int>((rows - 1) * WaterColumns * 6);
            var fallTris = new List<int>();

            for (int r = 0; r < rows; r++)
            {
                float d = rowDistances[r];
                var s = track.EvaluateAtDistance(d);
                for (int c = 0; c < cols; c++)
                {
                    float u = c / (float)WaterColumns;
                    verts.Add(s.center + s.right * Mathf.Lerp(-extent, extent, u));
                    normals.Add(Vector3.up);
                    // v tính theo mét chia cho bề rộng để texture sau này không bị kéo dãn
                    uvs.Add(new Vector2(u, d / (2f * extent)));
                }
            }

            for (int r = 0; r < rows - 1; r++)
            {
                // Đoạn nước dốc (thác đổ) dùng vật liệu nước đổ trắng bọt, phần còn lại là mặt sông bình thường
                float rise = verts[r * cols].y - verts[(r + 1) * cols].y;
                float run = Mathf.Max(0.01f, rowDistances[r + 1] - rowDistances[r]);
                var list = rise / run > 0.18f ? fallTris : tris;
                for (int c = 0; c < WaterColumns; c++)
                {
                    int a = r * cols + c, b = a + cols, cc = b + 1, d = a + 1;
                    list.Add(a); list.Add(b); list.Add(cc);
                    list.Add(a); list.Add(cc); list.Add(d);
                }
            }

            var mesh = new Mesh { name = $"{track.name}_Water" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris, 0);
            mesh.SetTriangles(fallTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ctx.Meshes.Add(mesh);

            var fallMat = FlowMaterial("RiverFallWater", new Color(0.2f, 0.55f, 0.7f, 0.95f), 0.9f, 0.85f);
            fallMat.SetFloat("_Cascade", 1f);
            EditorUtility.SetDirty(fallMat);
            var go = CreateMeshObject("Water", mesh, new[] { ctx.Mats.Water, fallMat }, parent);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- Cổng, nhà, cầu, biển báo ----------

        static void BuildGates(BuildContext ctx, Transform parent)
        {
            BuildGate(ctx, parent, ctx.Track.StartDistance, "StartGate", ctx.Mats.GateStart);
            BuildGate(ctx, parent, ctx.Track.FinishDistance, "FinishGate", ctx.Mats.GateFinish);
        }

        static void BuildGate(BuildContext ctx, Transform parent, float distance, string name, Material banner)
        {
            var track = ctx.Track;
            var s = track.EvaluateAtDistance(distance);
            var gate = new GameObject(name).transform;
            gate.SetParent(parent, false);
            gate.SetPositionAndRotation(s.center, Quaternion.LookRotation(s.tangent, Vector3.up));

            // Cột cắm từ dưới mặt nước (-sink) lên tới độ cao height
            const float height = 6f;
            const float sink = 1.5f;
            float span = track.HalfWidth + 1.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                var p = Primitive(PrimitiveType.Cylinder, $"Pole_{(side < 0 ? "L" : "R")}", ctx.Mats.GatePole, gate);
                p.localPosition = new Vector3(side * span, (height - sink) * 0.5f, 0f);
                // Cylinder mặc định cao 2 m nên scale.y bằng nửa chiều dài cột
                p.localScale = new Vector3(0.4f, (height + sink) * 0.5f, 0.4f);
                ctx.Exclude(p.position, 3f);
            }

            var b = Primitive(PrimitiveType.Cube, "Banner", banner, gate);
            b.localPosition = new Vector3(0f, height - 0.6f, 0f);
            b.localScale = new Vector3(span * 2f + 0.4f, 1f, 0.2f);
        }

        static void BuildCabin(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var s = track.EvaluateAtDistance(track.StartDistance + 55f);
            Vector3 toRiver = -s.right;

            // Nhà gỗ trên bờ phải, cửa quay ra sông
            Vector3 housePos = s.center + s.right * (track.HalfWidth + track.bankWidth + 8f);
            float floor = MaxTerrain(track, housePos, 3f);
            var house = CreateMeshObject("Cabin", ctx.Props["Cabin"],
                new[] { ctx.Mats.Plaster, ctx.Mats.WoodDark, ctx.Mats.Roof, ctx.Mats.Rock }, parent);
            // Mặt trước của nhà là -z, nên trục +z phải quay ra xa sông
            house.transform.SetPositionAndRotation(new Vector3(housePos.x, floor, housePos.z), Quaternion.LookRotation(-toRiver, Vector3.up));
            house.AddComponent<BoxCollider>();
            ctx.Exclude(housePos, 7f);

            // Bến gỗ chĩa ra sông, cách nhà vài mét về phía hạ lưu
            var sd = track.EvaluateAtDistance(track.StartDistance + 62f);
            Vector3 dockPos = sd.center + sd.right * (track.HalfWidth + 0.5f);
            var dock = CreateMeshObject("Dock", ctx.Props["Dock"], new[] { ctx.Mats.Wood, ctx.Mats.WoodDark }, parent);
            dock.transform.SetPositionAndRotation(new Vector3(dockPos.x, sd.WaterHeight, dockPos.z), Quaternion.LookRotation(-sd.right, Vector3.up));
            dock.AddComponent<BoxCollider>();
            ctx.Exclude(dockPos, 5f);
            ctx.Exclude(dockPos - sd.right * 3f, 4f);
        }

        static void BuildBridge(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            // Tìm đoạn thẳng nhất trong quãng giữa đường đua để bắc cầu
            float from = track.StartDistance + track.RaceLength * 0.4f;
            float to = track.StartDistance + track.RaceLength * 0.65f;
            float best = from, bestTurn = float.MaxValue;
            for (float d = from; d <= to; d += 5f)
            {
                // Không bắc cầu ngay chỗ thác hay hầm
                if (track.IsNearFeature(d, 35f))
                    continue;
                float turn = Mathf.Abs(TurnAngle(track, d - 15f, d + 15f));
                if (turn < bestTurn)
                {
                    bestTurn = turn;
                    best = d;
                }
            }

            var s = track.EvaluateAtDistance(best);
            float span = 2f * (track.HalfWidth + track.bankWidth + 3f);
            var mesh = PropLibrary.BuildBridge($"{track.name}_Bridge", span, 3.5f);
            ctx.Meshes.Add(mesh);

            float endHeight = track.TerrainHeight(s.center + s.right * (span * 0.5f), out _);
            float endHeight2 = track.TerrainHeight(s.center - s.right * (span * 0.5f), out _);
            var go = CreateMeshObject("Bridge", mesh, new[] { ctx.Mats.Wood, ctx.Mats.WoodDark }, parent);
            // Trục x của cầu nằm ngang qua sông
            go.transform.SetPositionAndRotation(
                new Vector3(s.center.x, Mathf.Max(endHeight, endHeight2) - 0.1f, s.center.z),
                Quaternion.LookRotation(s.tangent, Vector3.up));

            foreach (float side in new[] { -1f, 1f })
                ctx.Exclude(s.center + s.right * side * (span * 0.5f), 5f);
            ctx.Exclude(s.center, 4f);
            ctx.Count("cầu", 1);
        }

        static void BuildTurnSigns(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            float lastSign = float.MinValue;
            int count = 0;
            for (float d = track.StartDistance + 40f; d < track.FinishDistance - 40f; d += 10f)
            {
                float turn = TurnAngle(track, d, d + 60f);
                if (Mathf.Abs(turn) < 20f || d - lastSign < 150f)
                    continue;

                lastSign = d;
                var s = track.EvaluateAtDistance(d);
                // Rẽ phải thì cắm biển ở bờ trái (phía ngoài khúc cua) và ngược lại
                float side = turn > 0f ? -1f : 1f;
                Vector3 p = s.center + s.right * side * (track.HalfWidth + 2f);
                float h = track.TerrainHeight(p, out float signDist);
                // Bỏ qua chỗ rơi vào cửa nhánh sông
                if (signDist < track.HalfWidth + 1f || ctx.IsExcluded(p))
                    continue;

                var go = CreateMeshObject($"Sign_{count:00}", ctx.Props["Sign"],
                    new[] { ctx.Mats.Wood, ctx.Mats.GatePole, ctx.Mats.GateFinish }, parent);
                // Mặt bảng là -z nên trục +z xuôi dòng thì người chơi nhìn thấy mặt bảng
                go.transform.SetPositionAndRotation(new Vector3(p.x, h, p.z), Quaternion.LookRotation(s.tangent, Vector3.up));
                go.transform.localScale = new Vector3(turn > 0f ? 1.3f : -1.3f, 1.3f, 1.3f);
                ctx.Exclude(p, 2.5f);
                count++;
            }
            ctx.Count("biển báo", count);
        }

        // ---------- Đá ----------

        static void BuildRiverRocks(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            // Chừa 35 m đầu cho người chơi quen tay và 10 m trước vạch đích
            float from = track.StartDistance + 35f;
            float to = track.FinishDistance - 10f;
            if (to <= from)
                return;

            int target = Mathf.RoundToInt((to - from) * track.rocksPer100m / 100f);
            float limit = track.HalfWidth - 1.5f;
            var placed = new List<Vector3>();

            for (int attempt = 0; attempt < target * 30 && placed.Count < target; attempt++)
            {
                var s = track.EvaluateAtDistance(Range(rng, from, to));
                Vector3 p = s.center + s.right * Range(rng, -limit, limit);
                // Hai tảng cách nhau ít nhất 9 m để luôn còn khe cho thuyền đi qua
                if (TooClose(placed, p, 9f) || ctx.IsExcluded(p, 1f))
                    continue;
                placed.Add(p);

                float size = Range(rng, 0.9f, 1.8f);
                var mesh = Pick(rng, ctx.Props.Rocks);
                var go = CreateMeshObject($"Rock_{placed.Count:00}", mesh, new[] { ctx.Mats.Rock }, parent);
                go.transform.SetPositionAndRotation(
                    new Vector3(p.x, s.WaterHeight - 0.35f * size, p.z),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = new Vector3(
                    size * Range(rng, 0.9f, 1.3f),
                    size * Range(rng, 1.2f, 1.6f),
                    size * Range(rng, 0.9f, 1.3f));

                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
            }

            ctx.Count("đá giữa dòng", placed.Count);
        }

        static void BuildBankRocks(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.bankRocksPer100m / 100f);
            int count = 0;

            for (int attempt = 0; attempt < target * 5 && count < target; attempt++)
            {
                if (!TryBankPoint(ctx, 0.5f, track.bankWidth + 8f, 0.3f, out var p, out float h, out float d))
                    continue;

                float size = Range(rng, 0.8f, 2.5f);
                var mesh = Pick(rng, ctx.Props.Rocks);
                var go = CreateMeshObject($"BankRock_{count:00}", mesh, new[] { ctx.Mats.Rock }, parent);
                go.transform.SetPositionAndRotation(
                    new Vector3(p.x, h - 0.2f * size, p.z),
                    Quaternion.Euler(Range(rng, -10f, 10f), Range(rng, 0f, 360f), Range(rng, -10f, 10f)));
                go.transform.localScale = Vector3.one * size;

                // Chỉ đá sát mép nước mới cần va chạm
                if (d < track.HalfWidth + track.bankWidth + 1f)
                {
                    var col = go.AddComponent<MeshCollider>();
                    col.sharedMesh = mesh;
                    col.convex = true;
                }

                count++;
            }

            ctx.Count("đá trên bờ", count);
        }

        // ---------- Cây cối ----------

        static void BuildTrees(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.treesPer100m / 100f);
            float inner = track.bankWidth + 2.5f;
            float spread = Mathf.Max(1f, track.terrainMargin - track.HalfWidth - inner - 6f);
            var placed = new List<Vector3>();

            for (int attempt = 0; attempt < target * 10 && placed.Count < target; attempt++)
            {
                // Lũy thừa để cây mọc dày hơn ở gần bờ, nơi người chơi nhìn thấy rõ
                float lateral = inner + Mathf.Pow((float)rng.NextDouble(), 1.3f) * spread;
                if (!TryBankPoint(ctx, lateral, lateral, inner - 0.5f, out var p, out float h, out _)
                    || TooClose(placed, p, 3.5f) || ctx.IsExcluded(p, 2f))
                    continue;
                placed.Add(p);

                double roll = rng.NextDouble();
                var kind = roll < 0.45 ? PropLibrary.TreeKind.Pine
                    : roll < 0.7 ? PropLibrary.TreeKind.PineTall
                    : PropLibrary.TreeKind.Round;
                var leaves = kind == PropLibrary.TreeKind.Round ? ctx.Mats.Leaves : ctx.Mats.LeavesDark;

                var go = CreateMeshObject($"Tree_{placed.Count:000}", ctx.Props.Tree(kind), new[] { ctx.Mats.Trunk, leaves }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, h - 0.15f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 0.8f, 1.4f);
            }

            ctx.Count("cây", placed.Count);
        }

        /// <summary>
        /// Rừng rậm ở vùng đất xa bờ: cây mọc dày theo từng mảng (lấy theo nhiễu Perlin), xen giữa là bãi trống.
        /// Bãi trống để chừa chỗ cho các nhánh sông, đường tắt làm sau này.
        /// </summary>
        static void BuildDeepForest(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.forestTreesPer100m / 100f);
            float inner = track.bankWidth + 30f;
            float outer = track.terrainMargin - track.HalfWidth - 4f;
            if (outer <= inner || target == 0)
                return;

            float noiseOffset = (track.seed % 389) * 1.91f;
            var leaves = new[] { ctx.Mats.Trunk, ctx.Mats.LeavesDark };
            var placed = new List<Vector3>();

            for (int attempt = 0; attempt < target * 12 && placed.Count < target; attempt++)
            {
                float lateral = Range(rng, inner, outer);
                if (!TryBankPoint(ctx, lateral, lateral, inner - 2f, out var p, out float h, out _))
                    continue;

                // Chỉ mọc ở chỗ nhiễu cao để thành mảng rừng, chỗ nhiễu thấp để trống
                float grove = Mathf.PerlinNoise(p.x * 0.018f + noiseOffset, p.z * 0.018f + noiseOffset);
                if (grove < 0.45f || TooClose(placed, p, grove > 0.6f ? 3f : 4.5f) || ctx.IsExcluded(p, 2f))
                    continue;
                placed.Add(p);

                double roll = rng.NextDouble();
                var kind = roll < 0.5 ? PropLibrary.TreeKind.PineTall : roll < 0.8 ? PropLibrary.TreeKind.Pine : PropLibrary.TreeKind.Round;
                var mats = kind == PropLibrary.TreeKind.Round ? new[] { ctx.Mats.Trunk, ctx.Mats.Leaves } : leaves;
                var go = CreateMeshObject($"ForestTree_{placed.Count:0000}", ctx.Props.Tree(kind), mats, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, h - 0.2f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 1f, 1.7f);
            }

            ctx.Count("cây rừng rậm", placed.Count);
        }

        static void BuildBushes(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.bushesPer100m / 100f);
            int count = 0;

            for (int attempt = 0; attempt < target * 5 && count < target; attempt++)
            {
                // Bụi mọc khắp vùng đất, dày ở gần bờ và thưa dần vào sâu trong rừng
                float lateral = 0.8f + Mathf.Pow((float)rng.NextDouble(), 1.6f) * (track.terrainMargin - track.HalfWidth - 6f);
                if (!TryBankPoint(ctx, lateral, lateral, 0.6f, out var p, out float h, out _) || ctx.IsExcluded(p, 1f))
                    continue;

                var go = CreateMeshObject($"Bush_{count:000}", Pick(rng, ctx.Props.Bushes),
                    new[] { rng.NextDouble() < 0.5 ? ctx.Mats.Bush : ctx.Mats.Leaves }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, h - 0.1f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 0.7f, 1.5f);
                count++;
            }

            ctx.Count("bụi cây", count);
        }

        static void BuildFlowers(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.flowersPer100m / 100f);
            int count = 0;

            for (int attempt = 0; attempt < target * 5 && count < target; attempt++)
            {
                float lateral = track.bankWidth + Range(rng, 1f, 28f);
                if (!TryBankPoint(ctx, lateral, lateral, track.bankWidth + 0.5f, out var p, out float h, out _) || ctx.IsExcluded(p, 1.5f))
                    continue;

                var color = ctx.Mats.Flowers[rng.Next(ctx.Mats.Flowers.Length)];
                var go = CreateMeshObject($"Flowers_{count:000}", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, color }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, h, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 0.8f, 1.3f);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                count++;
            }

            ctx.Count("khóm hoa", count);
        }

        static void BuildLogs(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.logsPer100m / 100f);
            int count = 0;

            for (int attempt = 0; attempt < target * 8 && count < target; attempt++)
            {
                float lateral = track.bankWidth * 0.6f + Range(rng, 0f, 10f);
                if (!TryBankPoint(ctx, lateral, lateral, track.HalfWidth + 1.5f, out var p, out float h, out float d) || ctx.IsExcluded(p, 3f))
                    continue;

                var go = CreateMeshObject($"Log_{count:00}", ctx.Props["Log"], new[] { ctx.Mats.Trunk, ctx.Mats.Wood }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, h + 0.15f, p.z),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), Range(rng, -6f, 6f)));
                go.transform.localScale = new Vector3(Range(rng, 0.7f, 1.2f), Range(rng, 0.8f, 1.3f), Range(rng, 0.8f, 1.3f));
                if (d < track.HalfWidth + track.bankWidth)
                    go.AddComponent<CapsuleCollider>().direction = 0;
                count++;
            }

            ctx.Count("khúc gỗ", count);
        }

        // ---------- Sát mép nước ----------

        static void BuildReeds(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.reedsPer100m / 100f);
            int count = 0;

            for (int attempt = 0; attempt < target * 5 && count < target; attempt++)
            {
                var s = track.EvaluateAtDistance(Range(rng, 0f, track.Length));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 p = s.center + s.right * side * (track.HalfWidth + Range(rng, -0.8f, 0.8f));
                if (ctx.IsExcluded(p, 1f))
                    continue;

                var go = CreateMeshObject($"Reeds_{count:000}", ctx.Props["Reeds"], new[] { ctx.Mats.Reed, ctx.Mats.Trunk }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, s.WaterHeight, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 0.8f, 1.3f);
                count++;
            }

            ctx.Count("bụi sậy", count);
        }

        static void BuildLilyPads(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.lilyPadsPer100m / 100f);
            int clusters = 0, pads = 0;

            for (int attempt = 0; attempt < target * 5 && clusters < target; attempt++)
            {
                var s = track.EvaluateAtDistance(Range(rng, 0f, track.Length));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 c = s.center + s.right * side * (track.HalfWidth - Range(rng, 1f, 2.5f));
                if (ctx.IsExcluded(c, 2f))
                    continue;

                int n = rng.Next(3, 7);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = c + new Vector3(Range(rng, -1.3f, 1.3f), 0f, Range(rng, -1.3f, 1.3f));
                    bool flower = rng.NextDouble() < 0.3;
                    var mats = flower ? new[] { ctx.Mats.LilyPad, ctx.Mats.Flowers[1] } : new[] { ctx.Mats.LilyPad };
                    var go = CreateMeshObject($"LilyPad_{pads:000}", ctx.Props[flower ? "LilyPadFlower" : "LilyPad"], mats, parent);
                    go.transform.SetPositionAndRotation(new Vector3(p.x, s.WaterHeight + 0.03f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                    go.transform.localScale = Vector3.one * Range(rng, 0.7f, 1.2f);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    pads++;
                }
                clusters++;
            }

            ctx.Count("lá súng", pads);
        }

        // ---------- Núi và mây ----------

        // Dãy núi là một tấm địa hình thứ hai, ô lưới thưa (8 m), bắt đầu từ mép địa hình chính và
        // nhô cao dần thành núi. Liền một khối nên không có khe hở như khi ghép nhiều ngọn núi rời.
        const float MountainGrid = 4f;
        const float MountainOverlap = 8f;

        /// <summary>Độ cao của dãy núi tại một điểm, đã biết khoảng cách tới tâm sông.</summary>
        /// <summary>Độ cao dãy núi, đã đào lòng các nhánh sông chạy lộ thiên qua chân núi.</summary>
        static float MountainHeight(RiverTrack track, Vector3 p, RiverSample s)
        {
            float h = MountainHeightRaw(track, p, s);
            float unused = float.MaxValue;
            return track.CarveBranches(p, h, ref unused);
        }

        /// <summary>Độ cao dãy núi khi chưa đào nhánh sông nào.</summary>
        static float MountainHeightRaw(RiverTrack track, Vector3 p, RiverSample s)
        {
            float baseH = track.UnmodifiedTerrainHeightAt(p, s.horizontalDistance, s.WaterHeight);
            float e = s.horizontalDistance - track.terrainMargin;
            // Dãy núi của hầm nối liền vào dãy núi bao quanh, không bị đứt ở mép địa hình
            float tunnelRidge = track.RidgeHeight(p, s) - 0.5f;
            if (e <= 0f)
                return ApplyWaterfallLandform(track,p,Mathf.Max(baseH - 0.5f, tunnelRidge));

            // Nhiễu dạng sống núi: lấy 1 - |2n - 1| nên chỗ nhiễu bằng 0,5 thành đỉnh nhọn, cộng ba tầng to nhỏ
            float seed = (track.seed % 613) * 1.13f;
            float ridge = 0f, amp = 1f, freq = 0.011f, total = 0f;
            for (int o = 0; o < 3; o++)
            {
                float n = Mathf.PerlinNoise(p.x * freq + seed, p.z * freq + seed * 0.7f);
                float r = 1f - Mathf.Abs(2f * n - 1f);
                ridge += r * r * amp;
                total += amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            ridge /= total;

            float rise = Mathf.SmoothStep(0f, 1f, e / 70f);
            return ApplyWaterfallLandform(track,p,Mathf.Max(tunnelRidge, baseH - 0.5f + rise * (track.mountainHeight * (0.35f + 0.65f * ridge)) + e * 0.12f));
        }

        static void BuildMountains(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            float inner = track.terrainMargin - MountainOverlap;
            float outer = track.terrainMargin + track.mountainDepth;
            float g = MountainGrid;
            Bounds b = track.GetCenterlineBounds();

            float minX = b.min.x - outer - g, minZ = b.min.z - outer - g;
            int nx = Mathf.CeilToInt((b.size.x + 2f * (outer + g)) / g) + 1;
            int nz = Mathf.CeilToInt((b.size.z + 2f * (outer + g)) / g) + 1;

            var pos = new Vector3[nx * nz];
            var dist = new float[nx * nz];
            var water = new float[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    float jx = (Hash(ix, iz, track.seed + 31) - 0.5f) * g * 0.45f;
                    float jz = (Hash(ix, iz, track.seed + 57) - 0.5f) * g * 0.45f;
                    var p = new Vector3(minX + ix * g + jx, 0f, minZ + iz * g + jz);
                    var s = track.GetNearest(p);
                    p.y = MountainHeight(track, p, s);
                    int i = iz * nx + ix;
                    pos[i] = p;
                    dist[i] = s.horizontalDistance;
                    water[i] = s.WaterHeight;
                }
            }

            var mats = ctx.Mats;
            // 0 cỏ, 1 rừng, 2 đá, 3 tuyết
            var materials = new[] { mats.Mountain, mats.ForestFloor, mats.MountainRock, mats.Snow };
            // Tính theo độ cao so với mặt nước: rừng ở chân núi, rồi cỏ, đá, tuyết chỉ trên đỉnh
            float snowLine = track.mountainHeight * 0.92f;
            float rockLine = track.mountainHeight * 0.6f;
            float forestLine = track.mountainHeight * 0.3f;
            int totalTris = 0;

            void AddTri(FlatMeshBuilder fb, int ia, int ib, int ic)
            {
                Vector3 a = pos[ia], bb = pos[ib], c = pos[ic];
                Vector3 m = (a + bb + c) / 3f;
                float rel = m.y - (water[ia] + water[ib] + water[ic]) / 3f;
                float up = Mathf.Abs(Vector3.Cross(bb - a, c - a).normalized.y);
                // Đường tuyết và đường đá lượn theo nhiễu cho tự nhiên
                float wobble = (Mathf.PerlinNoise(m.x * 0.05f, m.z * 0.05f) - 0.5f) * 10f;
                int sub;
                float hgt = rel + wobble;
                if (hgt > snowLine && up > 0.4f) sub = 3;
                else if (hgt > rockLine || up < 0.55f) sub = 2;
                else if (hgt < forestLine) sub = 1;
                else sub = 0;
                fb.TriUp(sub, a, bb, c);
            }

            const int chunk = 24;
            for (int cz = 0; cz * chunk < nz - 1; cz++)
            {
                for (int cx = 0; cx * chunk < nx - 1; cx++)
                {
                    var fb = new FlatMeshBuilder(4);
                    int zEnd = Mathf.Min((cz + 1) * chunk, nz - 1);
                    int xEnd = Mathf.Min((cx + 1) * chunk, nx - 1);
                    for (int iz = cz * chunk; iz < zEnd; iz++)
                    {
                        for (int ix = cx * chunk; ix < xEnd; ix++)
                        {
                            int i00 = iz * nx + ix, i10 = i00 + 1, i01 = i00 + nx, i11 = i01 + 1;
                            float dMin = Mathf.Min(Mathf.Min(dist[i00], dist[i10]), Mathf.Min(dist[i01], dist[i11]));
                            float dMax = Mathf.Max(Mathf.Max(dist[i00], dist[i10]), Mathf.Max(dist[i01], dist[i11]));
                            if (dMax < inner || dMin > outer)
                                continue;

                            if (((ix + iz) & 1) == 0)
                            {
                                AddTri(fb, i00, i01, i11);
                                AddTri(fb, i00, i11, i10);
                            }
                            else
                            {
                                AddTri(fb, i00, i01, i10);
                                AddTri(fb, i10, i01, i11);
                            }
                        }
                    }

                    if (fb.IsEmpty)
                        continue;

                    var mesh = fb.ToMesh($"{track.name}_Mountains_{cx}_{cz}");
                    ctx.Meshes.Add(mesh);
                    totalTris += fb.VertexCount / 3;
                    var go = CreateMeshObject($"Mountains_{cx}_{cz}", mesh, materials, parent);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            ctx.Count("tam giác núi", totalTris);
        }

        /// <summary>Rừng thông mọc trên phần thấp của sườn núi, dày ở chân núi và thưa dần lên cao.</summary>
        static void BuildMountainForest(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int count = 0;
            var leaves = new[] { ctx.Mats.Trunk, ctx.Mats.LeavesDark };
            var kinds = new[] { PropLibrary.TreeKind.Pine, PropLibrary.TreeKind.PineTall };

            for (int attempt = 0; attempt < track.mountainTrees * 6 && count < track.mountainTrees; attempt++)
            {
                var s = track.EvaluateAtDistance(Range(rng, 0f, track.Length));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float e = Mathf.Pow((float)rng.NextDouble(), 1.8f) * 70f;
                Vector3 p = s.center + s.right * side * (track.terrainMargin - 4f + e);

                var near = track.GetNearest(p);
                if (near.horizontalDistance < track.terrainMargin - 6f || ctx.IsExcluded(p,3f))
                    continue;
                float h = MountainHeight(track, p, near);
                if (h - near.WaterHeight > track.mountainHeight * 0.45f)
                    continue;

                var go = CreateMeshObject($"MountainTree_{count:000}", ctx.Props.Tree(Pick(rng, kinds)), leaves, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, h - 0.6f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 1.1f, 1.9f);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                count++;
            }

            ctx.Count("cây trên núi", count);
        }

        // ---------- Làng ----------

        static void BuildVillages(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            // Làng chính ở quãng cuối đường đua, nằm phía trong khúc cua để người chơi nhìn thấy lâu hơn
            float d = track.StartDistance + track.RaceLength * 0.76f;
            float turn = TurnAngle(track, d - 30f, d + 30f);
            float side = turn >= 0f ? 1f : -1f;
            BuildVillage(ctx, Group(parent, "Village"), d, side, 6, true);

            // Xóm nhỏ ở bờ bên kia, gần vạch đích
            BuildVillage(ctx, Group(parent, "Hamlet"), track.FinishDistance - 45f, -side, 2, false);
        }

        static void BuildVillage(BuildContext ctx, Transform parent, float d, float side, int houses, bool withMill)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            var mats = ctx.Mats;
            float edge = track.HalfWidth + track.bankWidth;
            string[] meshes = { "Cabin", "HouseTall", "Cabin", "HouseTall" };
            int built = 0;

            for (int i = 0; i < houses; i++)
            {
                float along = (i - (houses - 1) * 0.5f) * 12f + Range(rng, -2f, 2f);
                float lateral = edge + 9f + (i % 2) * 11f + Range(rng, -1.5f, 1.5f);
                var s = track.EvaluateAtDistance(d + along);
                Vector3 p = s.center + s.right * side * lateral;
                if (track.GetNearest(p).horizontalDistance < edge + 5f || ctx.IsExcluded(p, 3f))
                    continue;

                string meshName = meshes[(i + rng.Next(3)) % 3];
                Material wall = mats.Plaster;
                Material roof = meshName == "Hut" ? mats.Hay : Pick(rng, mats.Roofs);
                var go = CreateMeshObject($"House_{built:00}", ctx.Props[meshName], new[] { wall, mats.WoodDark, roof, mats.Rock }, parent);
                float floor = MaxTerrain(track, p, 3f);
                // Mặt trước nhà là -z, quay ra sông
                go.transform.SetPositionAndRotation(new Vector3(p.x, floor, p.z),
                    Quaternion.LookRotation(s.right * side, Vector3.up) * Quaternion.Euler(0f, Range(rng, -12f, 12f), 0f));
                go.AddComponent<BoxCollider>();
                ctx.Exclude(p, 7f);
                built++;
            }

            // Giếng nước giữa làng và đống rơm
            var mid = track.EvaluateAtDistance(d);
            Vector3 wellPos = mid.center + mid.right * side * (edge + 15f);
            PlaceOnGround(ctx, "Well", ctx.Props["Well"], new[] { mats.Wood, mats.WoodDark, Pick(rng, mats.Roofs), mats.Rock }, parent, wellPos, Range(rng, 0f, 360f), 1f);
            ctx.Exclude(wellPos, 3f);

            for (int i = 0; i < houses / 2 + 1; i++)
            {
                var s = track.EvaluateAtDistance(d + Range(rng, -houses * 6f, houses * 6f));
                Vector3 p = s.center + s.right * side * (edge + 27f + Range(rng, 0f, 4f));
                if (ctx.IsExcluded(p, 1.5f))
                    continue;
                PlaceOnGround(ctx, $"Haystack_{i}", ctx.Props["Haystack"], new[] { mats.Hay }, parent, p, Range(rng, 0f, 360f), Range(rng, 0.8f, 1.2f));
                ctx.Exclude(p, 2f);
            }

            // Hàng rào phía sau làng
            float fenceLength = houses * 12f + 10f;
            for (float a = -fenceLength * 0.5f; a < fenceLength * 0.5f; a += 2.5f)
            {
                var s = track.EvaluateAtDistance(d + a);
                Vector3 p = s.center + s.right * side * (edge + 32f);
                if (track.GetNearest(p).horizontalDistance < edge + 20f)
                    continue;
                // Trục x của đoạn rào phải chạy dọc theo sông
                var go = PlaceOnGround(ctx, "Fence", ctx.Props["Fence"], new[] { mats.Wood }, parent, p, 0f, 1f);
                go.transform.rotation = Quaternion.LookRotation(-s.right, Vector3.up);
                ctx.Exclude(p, 1.5f);
            }

            if (withMill)
                BuildMill(ctx, parent, d - houses * 6f - 10f, side);

            ctx.Count(withMill ? "nhà trong làng" : "nhà trong xóm", built);
        }

        static void BuildMill(BuildContext ctx, Transform parent, float d, float side)
        {
            var track = ctx.Track;
            var mats = ctx.Mats;
            var s = track.EvaluateAtDistance(d);

            // Nhà cối xay đứng sát mép nước
            Vector3 housePos = s.center + s.right * side * (track.HalfWidth + 5f);
            var house = CreateMeshObject("MillHouse", ctx.Props["Cabin"], new[] { mats.Plaster, mats.WoodDark, mats.Roofs[3], mats.Rock }, parent);
            house.transform.SetPositionAndRotation(new Vector3(housePos.x, MaxTerrain(track, housePos, 2.5f), housePos.z),
                Quaternion.LookRotation(-s.tangent, Vector3.up));
            house.transform.localScale = Vector3.one * 0.85f;
            house.AddComponent<BoxCollider>();
            ctx.Exclude(housePos, 6f);

            // Bánh xe nhúng xuống nước, trục quay vuông góc với dòng chảy nên guồng quay theo dòng nước
            Vector3 wheelPos = s.center + s.right * side * (track.HalfWidth - 0.6f);
            var wheel = CreateMeshObject("MillWheel", ctx.Props["MillWheel"], new[] { mats.Wood, mats.WoodDark }, parent);
            wheel.transform.SetPositionAndRotation(new Vector3(wheelPos.x, s.WaterHeight + 1.7f, wheelPos.z),
                Quaternion.LookRotation(s.tangent * side, Vector3.up));
            wheel.AddComponent<World.Rotator>().degreesPerSecond = new Vector3(-25f, 0f, 0f);
            var col = wheel.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(0.9f, 4.6f, 4.6f);
            ctx.Exclude(wheelPos, 4f);
            ctx.Count("cối xay nước", 1);
        }

        static GameObject PlaceOnGround(BuildContext ctx, string name, Mesh mesh, Material[] mats, Transform parent, Vector3 p, float yaw, float scale)
        {
            float h = ctx.Track.TerrainHeight(p, out _);
            var go = CreateMeshObject(name, mesh, mats, parent);
            go.transform.SetPositionAndRotation(new Vector3(p.x, h, p.z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ---------- Thú rừng và chim ----------

        class AnimalSpec
        {
            public string Name, Body, Leg;
            public Material[] BodyMats, LegMats;
            public Vector3[] Hips;
            public World.AnimalWander.Gait Gait;
            public float Walk, Flee, FleeDistance, Scale, MinFromEdge, MaxFromEdge;
            public int Count;
        }

        static void BuildAnimals(BuildContext ctx, Transform parent)
        {
            var m = ctx.Mats;
            Vector3[] Hips(float x, float y, float z) => new[]
            {
                new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(-x, y, -z), new Vector3(x, y, -z),
            };

            var specs = new[]
            {
                new AnimalSpec { Name = "Deer", Body = "Deer_Body", Leg = "Deer_Leg", Count = 20,
                    BodyMats = new[] { m.DeerBrown, m.AnimalCream, m.AnimalDark }, LegMats = new[] { m.DeerBrown, m.AnimalDark },
                    Hips = Hips(0.15f, 0.76f, 0.36f), Walk = 1.1f, Flee = 5.5f, FleeDistance = 16f, Scale = 1f, MinFromEdge = 6f, MaxFromEdge = 40f },
                new AnimalSpec { Name = "Fox", Body = "Fox_Body", Leg = "Fox_Leg", Count = 9,
                    BodyMats = new[] { m.FoxOrange, m.AnimalCream, m.AnimalDark }, LegMats = new[] { m.AnimalDark, m.AnimalDark },
                    Hips = Hips(0.08f, 0.29f, 0.2f), Walk = 1.3f, Flee = 5f, FleeDistance = 12f, Scale = 1.2f, MinFromEdge = 6f, MaxFromEdge = 35f },
                new AnimalSpec { Name = "Bear", Body = "Bear_Body", Leg = "Bear_Leg", Count = 3,
                    BodyMats = new[] { m.BearBrown, m.AnimalCream, m.AnimalDark }, LegMats = new[] { m.BearBrown, m.AnimalDark },
                    Hips = Hips(0.25f, 0.55f, 0.42f), Walk = 0.8f, Flee = 2.5f, FleeDistance = 8f, Scale = 1.1f, MinFromEdge = 15f, MaxFromEdge = 45f },
                new AnimalSpec { Name = "Rabbit", Body = "Rabbit", Count = 28, Gait = World.AnimalWander.Gait.Hop,
                    BodyMats = new[] { m.RabbitGray, m.AnimalCream, m.AnimalDark },
                    Walk = 1.4f, Flee = 4.5f, FleeDistance = 10f, Scale = 1.3f, MinFromEdge = 5f, MaxFromEdge = 30f },
            };

            // Vùng đất rộng hơn mức gốc 70 m thì thú đi xa hơn và đông hơn theo cùng tỉ lệ
            float landScale = Mathf.Max(1f, ctx.Track.terrainMargin / 70f);
            foreach (var spec in specs)
            {
                spec.MaxFromEdge *= landScale;
                spec.Count = Mathf.RoundToInt(spec.Count * landScale);
                SpawnAnimals(ctx, Group(parent, spec.Name), spec);
            }
            SpawnDucks(ctx, Group(parent, "Ducks"));
        }

        static void SpawnAnimals(BuildContext ctx, Transform parent, AnimalSpec spec)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            float edge = track.HalfWidth + track.bankWidth;
            int count = 0;

            for (int attempt = 0; attempt < spec.Count * 20 && count < spec.Count; attempt++)
            {
                float lateral = track.bankWidth + Range(rng, spec.MinFromEdge, spec.MaxFromEdge);
                if (!TryBankPoint(ctx, lateral, lateral, track.bankWidth + spec.MinFromEdge - 1f, out var p, out float h, out float d)
                    || ctx.IsExcluded(p, 2f))
                    continue;

                var root = new GameObject($"{spec.Name}_{count:00}").transform;
                root.SetParent(parent, false);
                root.SetPositionAndRotation(new Vector3(p.x, h, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                root.localScale = Vector3.one * spec.Scale * Range(rng, 0.9f, 1.1f);

                var body = CreateMeshObject("Body", ctx.Props[spec.Body], spec.BodyMats, root).transform;
                Transform[] legs = null;
                if (spec.Leg != null)
                {
                    legs = new Transform[spec.Hips.Length];
                    string[] names = { "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR" };
                    for (int i = 0; i < legs.Length; i++)
                    {
                        legs[i] = CreateMeshObject(names[i], ctx.Props[spec.Leg], spec.LegMats, root).transform;
                        legs[i].localPosition = spec.Hips[i];
                    }
                }

                var ai = root.gameObject.AddComponent<World.AnimalWander>();
                ai.gait = spec.Gait;
                ai.body = body;
                ai.legs = legs;
                ai.walkSpeed = spec.Walk;
                ai.fleeSpeed = spec.Flee;
                ai.fleeDistance = spec.FleeDistance;
                // Vòng đi dạo không được chạm tới bờ dốc, để thú không lội xuống sông
                ai.wanderRadius = Mathf.Clamp((d - edge - 2f) / 1.5f, 1.5f, 8f);
                count++;
            }

            ctx.Count(spec.Name, count);
        }

        static void SpawnDucks(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            var mats = new[] { ctx.Mats.DuckBrown, ctx.Mats.DuckGreen, ctx.Mats.Beak };
            int count = 0;

            for (int group = 0; group < 7; group++)
            {
                var s = track.EvaluateAtDistance(Range(rng, track.StartDistance, track.FinishDistance));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 c = s.center + s.right * side * (track.HalfWidth - 1.8f);
                if (ctx.IsExcluded(c, 3f))
                    continue;

                int n = rng.Next(2, 5);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = c + new Vector3(Range(rng, -1.5f, 1.5f), 0f, Range(rng, -1.5f, 1.5f));
                    var root = new GameObject($"Duck_{count:00}").transform;
                    root.SetParent(parent, false);
                    root.SetPositionAndRotation(new Vector3(p.x, s.WaterHeight - 0.03f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                    root.localScale = Vector3.one * 1.3f;
                    var body = CreateMeshObject("Body", ctx.Props["Duck"], mats, root).transform;

                    var ai = root.gameObject.AddComponent<World.AnimalWander>();
                    ai.gait = World.AnimalWander.Gait.Swim;
                    ai.body = body;
                    ai.waterHeight = s.WaterHeight - 0.03f;
                    ai.wanderRadius = 1.5f;
                    ai.walkSpeed = 0.4f;
                    ai.fleeSpeed = 1.8f;
                    ai.fleeDistance = 9f;
                    count++;
                }
            }

            ctx.Count("vịt", count);
        }

        static void BuildBirds(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            var mat = new[] { ctx.Mats.BirdGray };
            int birds = 0;

            for (int f = 0; f < 4; f++)
            {
                var s = track.EvaluateAtDistance(track.Length * (f + 0.5f) / 4f);
                var flock = new GameObject($"BirdFlock_{f}").transform;
                flock.SetParent(parent, false);
                flock.position = s.center + s.right * Range(rng, -30f, 30f) + Vector3.up * Range(rng, 22f, 38f);

                var ai = flock.gameObject.AddComponent<World.BirdFlock>();
                ai.radius = Range(rng, 18f, 32f);
                ai.angularSpeed = Range(rng, 0.18f, 0.3f) * (rng.NextDouble() < 0.5 ? -1f : 1f);

                int n = rng.Next(4, 8);
                for (int i = 0; i < n; i++)
                {
                    var bird = CreateMeshObject($"Bird_{i}", ctx.Props["Bird_Body"], mat, flock).transform;
                    bird.localScale = Vector3.one * 3f;
                    var wr = CreateMeshObject("WingR", ctx.Props["Bird_Wing"], mat, bird).transform;
                    var wl = CreateMeshObject("WingL", ctx.Props["Bird_Wing"], mat, bird).transform;
                    wl.localScale = new Vector3(-1f, 1f, 1f);
                    foreach (var r in bird.GetComponentsInChildren<MeshRenderer>())
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    birds++;
                }
            }

            ctx.Count("chim", birds);
        }

        static void BuildClouds(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            int target = Mathf.RoundToInt(track.Length * track.cloudsPer100m / 100f);
            for (int i = 0; i < target; i++)
            {
                var s = track.EvaluateAtDistance(Range(rng, 0f, track.Length));
                Vector3 p = s.center + s.right * Range(rng, -90f, 90f) + Vector3.up * Range(rng, 45f, 75f);
                var go = CreateMeshObject($"Cloud_{i:00}", Pick(rng, ctx.Props.Clouds), new[] { ctx.Mats.Cloud }, parent);
                float size = Range(rng, 2.5f, 5f);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = new Vector3(size, size * 0.8f, size);
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            ctx.Count("mây", target);
        }

        static void BuildMeadowGrass(BuildContext ctx, Transform parent)
        {
            // Independent seed: adding ground cover never changes houses, trees or wildlife.
            var rng = new System.Random(ctx.Track.seed + 8207);
            int target = Mathf.Clamp(Mathf.RoundToInt(ctx.Track.Length * 1.4f), 180, 1500);
            int count = 0, chunk = 0;
            var mesh = new FlatMeshBuilder(3);
            var materials = new[] { ctx.Mats.GrassDark, ctx.Mats.Grass, ctx.Mats.Reed };
            for (int attempt = 0; attempt < target * 4 && count < target; attempt++)
            {
                var sample = ctx.Track.EvaluateAtDistance(Range(rng, 0, ctx.Track.Length));
                float side = rng.NextDouble() < 0.5 ? -1 : 1;
                float offset = ctx.Track.HalfWidth + Range(rng, 1.1f, ctx.Track.bankWidth + 18f);
                Vector3 center = sample.center + sample.right * side * offset;
                float distance;
                float height = ctx.Track.TerrainHeight(center, out distance);
                if (distance < ctx.Track.HalfWidth + 0.9f || ctx.IsExcluded(center, 1.3f)) continue;
                float patchRadius = Range(rng, 0.65f, 1.65f);
                for (int blade = 0; blade < 26; blade++)
                {
                    float a = Range(rng, 0, Mathf.PI * 2);
                    float radius = Mathf.Sqrt((float)rng.NextDouble()) * patchRadius;
                    var p = center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                    p.y = ctx.Track.TerrainHeight(p, out distance) - 0.035f;
                    if (distance < ctx.Track.HalfWidth + 0.65f || ctx.IsExcluded(p, 0.5f)) continue;
                    float h = Range(rng, 0.22f, 0.64f);
                    float angle = Range(rng, 0, Mathf.PI * 2);
                    var across = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Range(rng, 0.025f, 0.065f);
                    var lean = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle)) * h * 0.28f;
                    var mid = p + Vector3.up * h * 0.58f + lean * 0.3f;
                    var tip = p + Vector3.up * h + lean;
                    int mat = blade % 11 == 0 ? 2 : blade % 2;
                    // Double-sided tapered blades remain visible from river and bank views.
                    GrassTriangle(mesh, mat, p - across, mid - across * 0.55f, mid + across * 0.55f);
                    GrassTriangle(mesh, mat, p - across, mid + across * 0.55f, p + across);
                    GrassTriangle(mesh, mat, mid - across * 0.55f, tip, mid + across * 0.55f);
                }
                if (count % 13 == 0 && distance > ctx.Track.HalfWidth + ctx.Track.bankWidth)
                {
                    var flowers = CreateMeshObject("MeadowWildflowers_" + count, ctx.Props["Flowers"],
                        new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[(count / 13) % ctx.Mats.Flowers.Length] }, parent);
                    flowers.transform.position = new Vector3(center.x, height - 0.02f, center.z);
                    flowers.transform.localScale = Vector3.one * 0.5f;
                    flowers.transform.rotation = Quaternion.Euler(0, Range(rng, 0, 360), 0);
                    flowers.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                if (count % 29 == 0)
                {
                    for (int pebble = 0; pebble < 3; pebble++)
                    {
                        var p = center + new Vector3(pebble * 0.3f, 0, pebble * 0.22f);
                        p.y = ctx.Track.TerrainHeight(p, out distance) - 0.05f;
                        if (distance < ctx.Track.HalfWidth + 1f) continue;
                        var stone = CreateMeshObject("MeadowPebble_" + count + "_" + pebble,
                            ctx.Props.Rocks[pebble], new[] { ctx.Mats.Rock }, parent);
                        stone.transform.position = p;
                        stone.transform.localScale = new Vector3(0.28f, 0.19f, 0.24f);
                        stone.transform.rotation = Quaternion.Euler(0, Range(rng, 0, 360), 0);
                    }
                }
                count++;
                if (count % 64 == 0 || count == target)
                {
                    SaveGrassChunk(ctx, parent, mesh, materials, chunk++);
                    mesh = new FlatMeshBuilder(3);
                }
            }
            if (!mesh.IsEmpty) SaveGrassChunk(ctx, parent, mesh, materials, chunk);
            ctx.Count("cụm cỏ", count);
        }

        static void GrassTriangle(FlatMeshBuilder mesh, int mat, Vector3 a, Vector3 b, Vector3 c)
        {
            mesh.Tri(mat, a, b, c);
            mesh.Tri(mat, c, b, a);
        }

        static void SaveGrassChunk(BuildContext ctx, Transform parent, FlatMeshBuilder builder, Material[] materials, int chunk)
        {
            if (builder.IsEmpty) return;
            var mesh = builder.ToMesh("MeadowGrass_" + chunk);
            ctx.Meshes.Add(mesh);
            var go = CreateMeshObject(mesh.name, mesh, materials, parent);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildRiversideHomes(BuildContext ctx, Transform parent)
        {
            var rng = new System.Random(ctx.Track.seed + 17531);
            int built = 0;
            for (int site = 0; site < 10; site++)
                for (int pair = 0; pair < 2; pair++)
                {
                    float along = ctx.Track.StartDistance + ctx.Track.RaceLength * (0.06f + site * 0.092f) + pair * 17f;
                    if (along > ctx.Track.FinishDistance - 15f) continue;
                    var sample = ctx.Track.EvaluateAtDistance(along);
                    float side = (site + pair) % 2 == 0 ? 1 : -1;
                    float lateral = ctx.Track.HalfWidth + ctx.Track.bankWidth + 11f + pair * 5f;
                    var p = sample.center + sample.right * side * lateral;
                    float d;
                    float low = ctx.Track.TerrainHeight(p, out d);
                    if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 7f || ctx.IsExcluded(p, 8f)) continue;
                    float floor = MaxTerrain(ctx.Track, p, 3.5f);
                    if (floor - low > 2.2f) continue;
                    string shape = (site + pair) % 3 == 0 ? "HouseTall" : "Cabin";
                    var house = CreateMeshObject("RiversideCottage_" + built.ToString("00"), ctx.Props[shape],
                        new[] { ctx.Mats.Plaster, ctx.Mats.WoodDark, ctx.Mats.Roofs[(site + pair) % ctx.Mats.Roofs.Length], ctx.Mats.Rock }, parent);
                    house.transform.SetPositionAndRotation(new Vector3(p.x, floor, p.z), Quaternion.LookRotation(sample.right * side, Vector3.up) * Quaternion.Euler(0, Range(rng, -8f, 8f), 0));
                    house.AddComponent<BoxCollider>();
                    ctx.Exclude(p, 8f);
                    built++;
                }
            ctx.Count("nhà dọc sông", built);
        }

        static void BuildVillageFinish(BuildContext ctx, Transform parent)
        {
            var glass = AccentMaterial("CottageGlass", new Color(0.20f, 0.39f, 0.43f), ctx.Mats.Wood, false);
            var glow = AccentMaterial("LanternAmber", new Color(1f, 0.72f, 0.30f), ctx.Mats.Wood, true);
            var paving = AccentMaterial("CourtyardStone", new Color(0.65f, 0.61f, 0.51f), ctx.Mats.Rock, false);
            var houses = ctx.Track.GetComponentsInChildren<MeshFilter>();
            int housesFinished = 0;
            foreach (var house in houses)
            {
                if (house.sharedMesh == null || (house.sharedMesh.name != "Cabin" && house.sharedMesh.name != "HouseTall")) continue;
                bool tall = house.sharedMesh.name == "HouseTall";
                float front = tall ? 2.25f : 2f;
                var details = Group(house.transform, "ArchitecturalDetails");
                float dormerY = (tall ? 4.5f : 2.8f) + 0.4f;
                float dormerZ = -(tall ? 2.65f : 2.4f) + 0.22f;
                DetailBox(details, "DormerGlass", glass, new Vector3(0, dormerY, dormerZ), new Vector3(0.7f, 0.55f, 0.05f));
                foreach (float x in new[] { -0.4f, 0.4f })
                    DetailBox(details, "DormerTrim", ctx.Mats.WoodDark, new Vector3(x, dormerY, dormerZ - 0.04f), new Vector3(0.065f, 0.7f, 0.06f));
                foreach (float y in new[] { -0.32f, 0.32f })
                    DetailBox(details, "DormerTrim", ctx.Mats.WoodDark, new Vector3(0, dormerY + y, dormerZ - 0.04f), new Vector3(0.85f, 0.06f, 0.06f));
                foreach (float side in new[] { -1f, 1f })
                    foreach (float y in tall ? new[] { 1.4f, 3.3f } : new[] { 1.45f })
                    {
                        DetailBox(details, "SideGlass", glass, new Vector3(side * 2.55f, y, 0), new Vector3(0.05f, 0.75f, 0.9f));
                        foreach (float z in new[] { -0.52f, 0.52f })
                            DetailBox(details, "SideWindowFrame", ctx.Mats.WoodDark, new Vector3(side * 2.59f, y, z), new Vector3(0.07f, 0.9f, 0.07f));
                        foreach (float dy in new[] { -0.43f, 0f, 0.43f })
                            DetailBox(details, "SideWindowFrame", ctx.Mats.WoodDark, new Vector3(side * 2.59f, y + dy, 0), new Vector3(0.07f, 0.06f, 1.1f));
                    }
                foreach (float y in tall ? new[] { 1.45f, 3.3f } : new[] { 1.5f })
                    foreach (float x in new[] { -1.25f, 1.1f })
                    {
                        if (y < 2 && x > 0) continue;
                        DetailBox(details, "GlassPane", glass, new Vector3(x, y, -front - 0.12f), new Vector3(0.72f, 0.60f, 0.035f));
                        var flowers = CreateMeshObject("WindowFlowers", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[housesFinished % 4] }, details);
                        flowers.transform.localPosition = new Vector3(x, y - 0.39f, -front - 0.27f);
                        flowers.transform.localScale = new Vector3(0.38f, 0.5f, 0.18f);
                    }
                foreach (float x in new[] { 0.1f, 1.55f })
                {
                    var lp = new Vector3(x, 1.8f, -front - 0.3f);
                    DetailBox(details, "LanternBracket", ctx.Mats.WoodDark, lp + Vector3.up * 0.22f, new Vector3(0.09f, 0.1f, 0.45f));
                    DetailBox(details, "LanternGlow", glow, lp, new Vector3(0.16f, 0.25f, 0.16f));
                    foreach (float y in new[] { -0.16f, 0.16f })
                        DetailBox(details, "LanternFrame", ctx.Mats.WoodDark, lp + Vector3.up * y, new Vector3(0.25f, 0.055f, 0.25f));
                }
                // Stone path follows terrain instead of floating above a hillside.
                var garden = Group(parent, "CottageCourtyard_" + housesFinished);
                for (int row = 0; row < 7; row++)
                    for (int col = -1; col <= 1; col++)
                    {
                        Vector3 p = house.transform.TransformPoint(new Vector3(0.8f + col * 0.52f, 0, -front - 2f - row * 0.55f));
                        float d;
                        p.y = ctx.Track.TerrainHeight(p, out d);
                        if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 0.4f) continue;
                        var tile = DetailBox(garden, "StonePath", (row + col) % 3 == 0 ? ctx.Mats.Rock : paving,
                            p + Vector3.up * 0.025f, new Vector3(0.48f, 0.10f, 0.50f));
                        tile.rotation = house.transform.rotation;
                        ctx.Exclude(p, 0.6f);
                    }
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 p = house.transform.TransformPoint(new Vector3(side * 3.7f, 0, -front - 2f));
                    float d; p.y = ctx.Track.TerrainHeight(p, out d);
                    if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 1f) continue;
                    var fence = CreateMeshObject("GardenFence", ctx.Props["Fence"], new[] { ctx.Mats.Wood }, garden);
                    fence.transform.SetPositionAndRotation(p, house.transform.rotation * Quaternion.Euler(0, 90, 0));
                    ctx.Exclude(p, 1f);
                    // A small seating area, with legs sunk into terrain.
                    if (side < 0)
                    {
                        var seat = Group(garden, "GardenBench");
                        seat.SetPositionAndRotation(p + house.transform.right * 0.7f, house.transform.rotation);
                        DetailBox(seat, "Seat", ctx.Mats.Wood, new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.12f, 0.5f));
                        DetailBox(seat, "Back", ctx.Mats.Wood, new Vector3(0, 0.82f, 0.22f), new Vector3(1.5f, 0.45f, 0.08f));
                        foreach (float x in new[] { -0.55f, 0.55f })
                            DetailBox(seat, "BenchLeg", ctx.Mats.WoodDark, new Vector3(x, 0.15f, 0), new Vector3(0.13f, 0.65f, 0.35f));
                    }
                }
                housesFinished++;
            }
            ctx.Count("nhà hoàn thiện", housesFinished);
            for (int i = 0; i < 3; i++)
            {
                var sample = ctx.Track.EvaluateAtDistance(ctx.Track.StartDistance + ctx.Track.RaceLength * (0.22f + i * 0.26f));
                float side = i % 2 == 0 ? 1f : -1f;
                var p = sample.center + sample.right * side * (ctx.Track.HalfWidth + ctx.Track.bankWidth + 7f);
                float d;
                p.y = ctx.Track.TerrainHeight(p, out d);
                if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 5f || ctx.IsExcluded(p, 5f)) continue;
                float floor = MaxTerrain(ctx.Track, p, 3f) + 0.1f;
                var deck = Group(parent, "RiversideLookout_" + i);
                deck.SetPositionAndRotation(new Vector3(p.x, floor, p.z), Quaternion.LookRotation(sample.right * side, Vector3.up));
                for (int plank = 0; plank < 13; plank++)
                    DetailBox(deck, "DeckPlank", ctx.Mats.Wood, new Vector3(-2.4f + plank * 0.4f, 0, 0), new Vector3(0.37f, 0.15f, 3.4f));
                foreach (float x in new[] { -2.3f, 2.3f })
                    foreach (float z in new[] { -1.4f, 1.4f })
                    {
                        var wp = deck.TransformPoint(new Vector3(x, 0, z));
                        float leg = Mathf.Max(0.3f, floor - ctx.Track.TerrainHeight(wp, out d) + 0.3f);
                        DetailBox(deck, "Support", ctx.Mats.WoodDark, new Vector3(x, -leg * 0.5f, z), new Vector3(0.22f, leg, 0.22f));
                        DetailBox(deck, "RailPost", ctx.Mats.WoodDark, new Vector3(x, 0.6f, z), new Vector3(0.14f, 1.2f, 0.14f));
                    }
                foreach (float x in new[] { -2.3f, 2.3f })
                    DetailBox(deck, "SideRail", ctx.Mats.Wood, new Vector3(x, 1.1f, 0), new Vector3(0.1f, 0.1f, 3f));
                DetailBox(deck, "ViewingRail", ctx.Mats.Wood, new Vector3(0, 1.1f, -1.4f), new Vector3(4.6f, 0.1f, 0.1f));
                for (int picket = 0; picket < 16; picket++)
                    DetailBox(deck, "Picket", ctx.Mats.WoodDark, new Vector3(-2.25f + picket * 0.3f, 0.55f, -1.4f), new Vector3(0.05f, 1f, 0.05f));
                DetailBox(deck, "BenchSeat", ctx.Mats.Wood, new Vector3(0, 0.5f, 0.7f), new Vector3(2.5f, 0.12f, 0.55f));
                DetailBox(deck, "BenchBack", ctx.Mats.Wood, new Vector3(0, 0.9f, 0.95f), new Vector3(2.5f, 0.5f, 0.09f));
                foreach (float x in new[] { -1f, 1f })
                    DetailBox(deck, "BenchSupport", ctx.Mats.WoodDark, new Vector3(x, 0.25f, 0.7f), new Vector3(0.12f, 0.5f, 0.4f));
                foreach (float x in new[] { -2f, 2f })
                {
                    DetailBox(deck, "Planter", ctx.Mats.Roof, new Vector3(x, 0.2f, 0.8f), new Vector3(0.5f, 0.4f, 0.5f));
                    var flowers = CreateMeshObject("LookoutFlowers", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[i] }, deck);
                    flowers.transform.localPosition = new Vector3(x, 0.4f, 0.8f);
                    flowers.transform.localScale = Vector3.one * 0.32f;
                }
                ctx.Exclude(p, 5f);
            }
        }

        static Transform DetailBox(Transform parent, string name, Material mat, Vector3 position, Vector3 size)
        {
            var t = Primitive(PrimitiveType.Cube, name, mat, parent);
            t.localPosition = position;
            t.localScale = size;
            return t;
        }

        static Material AccentMaterial(string name, Color color, Material source, bool emission)
        {
            string path = TrackMaterials.Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(source) { name = name };
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", emission ? 0.2f : 0.35f);
            if (emission) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 1.4f); }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void BuildGardenDetails(BuildContext ctx, Transform parent)
        {
            // Follow existing cottage positions, so gardens survive spline edits and rebuilds.
            var houses = ctx.Track.GetComponentsInChildren<MeshFilter>() ;
            foreach (var house in houses)
            {
                if (house.sharedMesh == null || (house.sharedMesh.name != "Cabin" && house.sharedMesh.name != "HouseTall")) continue;
                for (int i = 0; i < 7; i++)
                {
                    float x = i % 2 == 0 ? -3.4f : 3.4f;
                    var p = house.transform.TransformPoint(new Vector3(x, 0, -2f + i * 0.65f));
                    float d;
                    float h = ctx.Track.TerrainHeight(p, out d);
                    if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth) continue;
                    var flowers = CreateMeshObject("CottageFlowerBed", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[i % ctx.Mats.Flowers.Length] }, parent);
                    flowers.transform.position = new Vector3(p.x, h, p.z);
                    flowers.transform.localScale = Vector3.one * 0.65f;
                }
            }
            // Fern-like understory in small clusters rather than uniform isolated props.
            for (int i = 0; i < 140; i++)
            {
                Vector3 p; float h, d;
                if (!TryBankPoint(ctx, ctx.Track.bankWidth + 2f, ctx.Track.bankWidth + 22f, ctx.Track.bankWidth + 1f, out p, out h, out d) || ctx.IsExcluded(p, 2f)) continue;
                var bush = CreateMeshObject("Understory_" + i, ctx.Props.Bushes[i % 2], new[] { ctx.Mats.LeavesDark }, parent);
                bush.transform.position = new Vector3(p.x, h - 0.08f, p.z);
                bush.transform.localScale = new Vector3(0.65f, 0.28f, 0.65f);
                bush.transform.rotation = Quaternion.Euler(0, Range(ctx.Rng, 0, 360), 0);
            }
        }

        // ---------- Tiện ích ----------

        /// <summary>
        /// Chọn ngẫu nhiên một điểm trên bờ, cách mép nước từ <paramref name="minFromEdge"/> đến <paramref name="maxFromEdge"/> mét.
        /// Bỏ điểm nếu nó lại quá gần một đoạn sông khác (ở khúc cua) hoặc nằm dưới nước.
        /// </summary>
        static bool TryBankPoint(BuildContext ctx, float minFromEdge, float maxFromEdge, float minFromEdgeCheck,
            out Vector3 point, out float height, out float distanceFromCenter)
        {
            var track = ctx.Track;
            var rng = ctx.Rng;
            var s = track.EvaluateAtDistance(Range(rng, 0f, track.Length));
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            point = s.center + s.right * side * (track.HalfWidth + Range(rng, minFromEdge, maxFromEdge));
            height = track.TerrainHeight(point, out distanceFromCenter);
            return distanceFromCenter >= track.HalfWidth + minFromEdgeCheck;
        }

        static float MaxTerrain(RiverTrack track, Vector3 center, float radius)
        {
            float max = track.TerrainHeight(center, out _);
            foreach (var o in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                max = Mathf.Max(max, track.TerrainHeight(center + o * radius, out _));
            return max;
        }

        /// <summary>Góc dòng sông đổi hướng từ mét <paramref name="from"/> đến mét <paramref name="to"/>. Dương là rẽ phải.</summary>
        static float TurnAngle(RiverTrack track, float from, float to)
        {
            var a = track.EvaluateAtDistance(from).tangent;
            var b = track.EvaluateAtDistance(to).tangent;
            return Vector3.SignedAngle(a, b, Vector3.up);
        }

        static Dictionary<string, Mesh> SnapshotGround(RiverTrack track)
        {
            var copies = new Dictionary<string, Mesh>();
            foreach (string group in new[] { "Terrain", "Mountains" })
            {
                var g = track.transform.Find("Generated/" + group);
                if (g == null) continue;
                foreach (var f in g.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && !copies.ContainsKey(f.sharedMesh.name))
                    {
                        var copy = Object.Instantiate(f.sharedMesh);
                        copy.name = f.sharedMesh.name;
                        copies[copy.name] = copy;
                    }
            }
            return copies;
        }

        static Transform Group(Transform root, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        static Transform Primitive(PrimitiveType type, string name, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject CreateMeshObject(string name, Mesh mesh, Material[] materials, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            // Botanical flowers reserve a separate warm pollen material.
            if (mesh.name == "Duck" && materials.Length == 3)
                materials = new[] { materials[0], materials[1], materials[2], AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/AnimalDark.mat") };
            if (mesh.name == "Flowers" && materials.Length == 2)
                materials = new[] { AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/BotanicalLeaves.mat") ?? materials[0], materials[1], AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/FlowerPollen.mat") ?? materials[1] };
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            if (mesh.name == "Flowers")
            {
                var far = new GameObject("DistantFlowers");
                far.transform.SetParent(go.transform, false);
                far.AddComponent<MeshFilter>().sharedMesh = BotanicalFlowerMesh.DistantMesh();
                var farRenderer = far.AddComponent<MeshRenderer>();
                farRenderer.sharedMaterials = materials;
                farRenderer.shadowCastingMode = ShadowCastingMode.Off;
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.045f, new Renderer[] { renderer }), new LOD(0.006f, new Renderer[] { farRenderer }) });
                lod.RecalculateBounds();
            }
            return go;
        }

        static void SaveMeshes(RiverTrack track, List<Mesh> meshes)
        {
            if (meshes.Count == 0)
                return;

            string scene = string.IsNullOrEmpty(track.gameObject.scene.name) ? "Untitled" : track.gameObject.scene.name;
            string path = $"{GeneratedFolder}/{scene}_{track.name}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);

            // Asset chính bị Unity đổi tên theo tên file, nên dùng một mesh rỗng làm asset chính
            AssetDatabase.CreateAsset(new Mesh { name = Path.GetFileNameWithoutExtension(path) }, path);
            foreach (var m in meshes)
                AssetDatabase.AddObjectToAsset(m, path);
            AssetDatabase.SaveAssets();
        }

        static void SetStatic(GameObject root)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                // Vật chuyển động (thú, chim, bánh xe cối xay) không được đánh dấu static, nếu không sẽ đứng im
                bool moves = t.GetComponentInParent<World.FarmHen>(true) != null
                             || t.GetComponentInParent<World.RealisticVillager>(true) != null
                             || t.GetComponentInParent<World.RuralVillager>(true) != null
                             || t.GetComponentInParent<World.AnimalWander>(true) != null
                             || t.GetComponentInParent<World.BirdFlock>(true) != null
                             || t.GetComponentInParent<World.Rotator>(true) != null
                             || t.GetComponentInParent<ParticleSystem>(true) != null;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, moves ? 0 : flags);
            }
        }

        static bool TooClose(List<Vector3> placed, Vector3 p, float minDistance)
        {
            float sqr = minDistance * minDistance;
            foreach (var q in placed)
            {
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < sqr)
                    return true;
            }
            return false;
        }

        static T Pick<T>(System.Random rng, T[] items) => items[rng.Next(items.Length)];

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static float Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(seed * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}






