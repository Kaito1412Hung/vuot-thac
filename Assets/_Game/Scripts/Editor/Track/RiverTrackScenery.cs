using UnityEngine;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        static void BuildScenicDetails(BuildContext ctx, Transform parent)
        {
            var rng = new System.Random(ctx.Track.seed + 45399);
            var canvas = AccentMaterial("TentCanvas", new Color(0.69f, 0.43f, 0.21f), ctx.Mats.Wood, false);
            var cream = AccentMaterial("PavilionCream", new Color(0.93f, 0.88f, 0.70f), ctx.Mats.Plaster, false);
            var roofMesh = PavilionRoof();
            var tentMesh = CampTent();
            ctx.Meshes.Add(roofMesh);
            ctx.Meshes.Add(tentMesh);
            int sites = 0;
            for (int slot = 0; slot < 9; slot++)
            {
                bool found = false;
                Vector3 p = Vector3.zero;
                Quaternion facing = Quaternion.identity;
                float floor = 0;
                for (int attempt = 0; attempt < 18 && !found; attempt++)
                {
                    float along = ctx.Track.StartDistance + ctx.Track.RaceLength * (0.09f + slot * 0.10f) + Range(rng, -25, 25);
                    var sample = ctx.Track.EvaluateAtDistance(along);
                    float side = attempt % 2 == 0 ? 1 : -1;
                    p = sample.center + sample.right * side * (ctx.Track.HalfWidth + ctx.Track.bankWidth + Range(rng, 11, 25));
                    float distance;
                    float ground = ctx.Track.TerrainHeight(p, out distance);
                    if (distance < ctx.Track.HalfWidth + ctx.Track.bankWidth + 9 || ctx.IsExcluded(p, 8)) continue;
                    floor = MaxTerrain(ctx.Track, p, 4);
                    if (floor - ground > 0.65f) continue;
                    facing = Quaternion.LookRotation(sample.right * side, Vector3.up);
                    found = true;
                }
                if (!found) continue;
                var site = Group(parent, (slot % 3 == 0 ? "Campsite_" : slot % 3 == 1 ? "Pavilion_" : "FlowerGarden_") + slot);
                site.SetPositionAndRotation(new Vector3(p.x, floor, p.z), facing);
                if (slot % 3 == 0) DecorateCamp(ctx, site, tentMesh, canvas);
                else if (slot % 3 == 1) DecoratePavilion(ctx, site, roofMesh, cream);
                else DecorateFlowerGarden(ctx, site);
                ctx.Exclude(p, 7);
                sites++;
            }
            ctx.Count("Ä‘iá»ƒm cáº£nh quan", sites);
        }

        static void DecorateCamp(BuildContext ctx, Transform site, Mesh tent, Material canvas)
        {
            foreach (float x in new[] { -2.4f, 2.4f })
            {
                var go = CreateMeshObject("CanvasTent", tent, new[] { canvas, ctx.Mats.WoodDark }, site);
                go.transform.localPosition = new Vector3(x, 0, 1.2f);
                go.transform.localRotation = Quaternion.Euler(0, x < 0 ? 22 : -22, 0);
                DetailBox(go.transform, "TentPlatform", ctx.Mats.Wood, new Vector3(0, -0.06f, 0), new Vector3(2.35f, 0.14f, 3.2f));
                SinkSupports(ctx, go.transform, 1.1f, 1.5f);
                foreach (float side in new[] { -1f, 1f })
                    foreach (float end in new[] { -1f, 1f })
                    {
                        var anchor = go.transform.TransformPoint(new Vector3(side * 1.8f, 0, end * 1.9f));
                        float distance;
                        anchor.y = ctx.Track.TerrainHeight(anchor, out distance) + 0.04f;
                        var local = go.transform.InverseTransformPoint(anchor);
                        DetailBox(go.transform, "TentStake", ctx.Mats.Rock, local, new Vector3(0.05f, 0.25f, 0.05f));
                        SceneryBeam(go.transform, "GuyRope", ctx.Mats.Hay, new Vector3(side * 0.8f, 1.15f, end * 1.45f), local, 0.012f);
                    }
                DetailBox(go.transform, "SleepingMat", ctx.Mats.Roofs[1], new Vector3(0, 0.06f, 0.15f), new Vector3(1.35f, 0.06f, 2.2f));
            }
            // An unlit stone fire ring and split logs, with seating around it.
            for (int i = 0; i < 11; i++)
            {
                float a = i * Mathf.PI * 2 / 11;
                var stone = CreateMeshObject("FireRingStone", ctx.Props.Rocks[i % 4], new[] { ctx.Mats.Rock }, site);
                stone.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 0.03f, -1.1f + Mathf.Sin(a) * 0.75f);
                stone.transform.localScale = new Vector3(0.22f, 0.18f, 0.23f);
            }
            foreach (float angle in new[] { -35f, 35f })
            {
                var log = CreateMeshObject("Firewood", ctx.Props["Log"], new[] { ctx.Mats.WoodDark, ctx.Mats.Wood }, site);
                log.transform.localPosition = new Vector3(0, 0.08f, -1.1f);
                log.transform.localRotation = Quaternion.Euler(0, angle, 0);
                log.transform.localScale = Vector3.one * 0.28f;
            }
            ScenicBench(ctx, site, new Vector3(-2.4f, 0, -2.4f), 55);
            ScenicBench(ctx, site, new Vector3(2.4f, 0, -2.4f), -55);
            DetailBox(site, "SupplyCrate", ctx.Mats.Wood, new Vector3(3.2f, 0.3f, 2.3f), new Vector3(0.65f, 0.6f, 0.65f));
        }

        static void DecoratePavilion(BuildContext ctx, Transform site, Mesh roof, Material cream)
        {
            for (int i = 0; i < 12; i++)
                DetailBox(site, "DeckBoard", ctx.Mats.Wood, new Vector3(-2.2f + i * 0.4f, 0.12f, 0), new Vector3(0.37f, 0.15f, 4.5f));
            SinkSupports(ctx, site, 2.1f, 2.1f);
            foreach (float x in new[] { -2.05f, 2.05f })
                foreach (float z in new[] { -2.05f, 2.05f })
                {
                    DetailBox(site, "PavilionColumn", cream, new Vector3(x, 1.7f, z), new Vector3(0.20f, 3.2f, 0.20f));
                    DetailBox(site, "ColumnBase", ctx.Mats.Rock, new Vector3(x, 0.3f, z), new Vector3(0.38f, 0.3f, 0.38f));
                }
            var canopy = CreateMeshObject("PavilionRoof", roof, new[] { ctx.Mats.Roofs[2], ctx.Mats.WoodDark }, site);
            canopy.transform.localPosition = Vector3.up * 3.25f;
            foreach (float x in new[] { -2.05f, 2.05f })
                DetailBox(site, "RoofBeam", ctx.Mats.WoodDark, new Vector3(x, 3.1f, 0), new Vector3(0.16f, 0.18f, 4.4f));
            ScenicBench(ctx, site, new Vector3(0, 0.2f, 1.35f), 0);
            DetailBox(site, "PicnicTable", ctx.Mats.Wood, new Vector3(0, 0.95f, -0.2f), new Vector3(2.2f, 0.12f, 1f));
            foreach (float x in new[] { -0.8f, 0.8f })
                DetailBox(site, "TableLeg", ctx.Mats.WoodDark, new Vector3(x, 0.55f, -0.2f), new Vector3(0.14f, 0.8f, 0.7f));
            foreach (float x in new[] { -1.8f, 1.8f }) FlowerPot(ctx, site, new Vector3(x, 0.2f, -1.7f), cream);
        }

        static void DecorateFlowerGarden(BuildContext ctx, Transform site)
        {
            // Low stone borders and a timber entrance arch.
            foreach (float side in new[] { -1f, 1f })
            {
                var bed = Group(site, "RaisedFlowerBed");
                bed.localPosition = new Vector3(side * 2.1f, 0, 0);
                DetailBox(bed, "Soil", ctx.Mats.WoodDark, Vector3.zero, new Vector3(1.4f, 0.18f, 4f));
                foreach (float x in new[] { -0.75f, 0.75f })
                    DetailBox(bed, "StoneBorder", ctx.Mats.Rock, new Vector3(x, 0.07f, 0), new Vector3(0.12f, 0.3f, 4.2f));
                foreach (float z in new[] { -2f, 2f })
                    DetailBox(bed, "StoneBorder", ctx.Mats.Rock, new Vector3(0, 0.07f, z), new Vector3(1.6f, 0.3f, 0.12f));
                for (int j = 0; j < 8; j++)
                {
                    var flower = CreateMeshObject("GardenBlooms", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[j % 4] }, bed);
                    flower.transform.localPosition = new Vector3(j % 2 == 0 ? -0.3f : 0.3f, 0.1f, -1.6f + j * 0.45f);
                    flower.transform.localScale = Vector3.one * 0.55f;
                }
            }
            foreach (float x in new[] { -1f, 1f })
                DetailBox(site, "GardenArchPost", ctx.Mats.Wood, new Vector3(x, 1.3f, -2.6f), new Vector3(0.13f, 3f, 0.13f));
            for (int j = 0; j < 6; j++)
                DetailBox(site, "PergolaCrosspiece", ctx.Mats.WoodDark, new Vector3(0, 2.7f, -3f + j * 0.16f), new Vector3(2.6f, 0.12f, 0.08f));
            ScenicBench(ctx, site, new Vector3(0, 0, 2.6f), 0);
        }

        static void ScenicBench(BuildContext ctx, Transform parent, Vector3 p, float yaw)
        {
            var bench = Group(parent, "RestBench");
            bench.localPosition = p;
            bench.localRotation = Quaternion.Euler(0, yaw, 0);
            DetailBox(bench, "Seat", ctx.Mats.Wood, new Vector3(0, 0.5f, 0), new Vector3(2, 0.12f, 0.55f));
            DetailBox(bench, "Backrest", ctx.Mats.Wood, new Vector3(0, 0.9f, 0.22f), new Vector3(2, 0.5f, 0.09f));
            foreach (float x in new[] { -0.7f, 0.7f })
                DetailBox(bench, "Leg", ctx.Mats.WoodDark, new Vector3(x, 0.1f, 0), new Vector3(0.15f, 0.8f, 0.4f));
        }

        static void FlowerPot(BuildContext ctx, Transform parent, Vector3 p, Material potMaterial)
        {
            DetailBox(parent, "Planter", potMaterial, p + Vector3.up * 0.22f, new Vector3(0.6f, 0.45f, 0.6f));
            var flowers = CreateMeshObject("PlanterFlowers", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[1] }, parent);
            flowers.transform.localPosition = p + Vector3.up * 0.45f;
            flowers.transform.localScale = Vector3.one * 0.4f;
        }

        static void SinkSupports(BuildContext ctx, Transform parent, float x, float z)
        {
            foreach (float sx in new[] { -x, x })
                foreach (float sz in new[] { -z, z })
                {
                    Vector3 wp = parent.TransformPoint(new Vector3(sx, 0, sz));
                    float distance;
                    float depth = Mathf.Max(0.25f, wp.y - ctx.Track.TerrainHeight(wp, out distance) + 0.2f);
                    DetailBox(parent, "GroundSupport", ctx.Mats.WoodDark, new Vector3(sx, -depth * 0.5f, sz), new Vector3(0.15f, depth, 0.15f));
                }
        }

        static Mesh PavilionRoof()
        {
            var b = new FlatMeshBuilder(2);
            var corners = new[] { new Vector3(-2.65f, 0, -2.65f), new Vector3(2.65f, 0, -2.65f), new Vector3(2.65f, 0, 2.65f), new Vector3(-2.65f, 0, 2.65f) };
            for (int j = 0; j < 4; j++)
            {
                b.TriOutward(0, corners[j], Vector3.up * 1.45f, corners[(j + 1) % 4], Vector3.up * 0.2f);
                b.TriOutward(1, corners[j], corners[(j + 1) % 4], Vector3.zero, Vector3.up * 0.2f);
            }
            return b.ToMesh("ScenicPavilionRoof");
        }

        static Mesh CampTent() => NaturalTentMesh.Build();
    }
}
