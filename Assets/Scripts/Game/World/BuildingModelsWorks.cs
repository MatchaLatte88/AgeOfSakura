using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// The working buildings added with the river and the tools economy: the Blacksmith (2x2), the Fisher Dock (2x4, land at one end and a
    /// pier in the water at the other) and the Road cell (1x1). Same rules as the other models: everything stays inside the footprint,
    /// children are grouped Ground / Body / Roof / Trees / Props, thin parts use flat boxes.
    /// </summary>
    public sealed partial class ProceduralBuildingModels
    {
        private Material IronMat => art.Lit(Hex("#3a3f48"));
        private Material IronLightMat => art.Lit(Hex("#6d7782"));
        private Material EmberMat => art.Glow(Hex("#d9461a"), Hex("#ff8c3d") * 0.7f);
        private Material LeatherMat => art.Lit(Hex("#7a4a2a"));
        private Material RopeMat => art.Lit(Hex("#b79a6a"));

        /// <summary>A horseshoe lying in the XY plane (open end down), centred on <paramref name="pos"/>.</summary>
        private void Horseshoe(Transform t, Vector3 pos, float radius, Material m)
        {
            var path = new System.Collections.Generic.List<Vector3>();
            var radii = new System.Collections.Generic.List<float>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.18f, Mathf.PI * 1.18f, i / 8f);
                path.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
                radii.Add(radius * 0.2f);
            }
            prim.MeshObject(t, ProceduralMeshes.Tube(path, radii, 4), m, pos, default);
        }

        // ---------------------------------------------------------------- Blacksmith (2x2): stone forge with a slate roof, levels 1..3

        private GameObject Blacksmith(int level, int w, int d)
        {
            prim.ThinBoxes = true; // tongs, hooks, hoops and slats: flat boxes for everything hair-thin
            try { return BuildBlacksmith(level); }
            finally { prim.ThinBoxes = false; }
        }

        private GameObject BuildBlacksmith(int level)
        {
            var rng = Rng("blacksmith");
            var root = NewRoot("Blacksmith_Model", 1.95f + 0.1f * level, 1.35f + 0.1f * level);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var stone = StoneMat;
            var stoneDark = StoneDarkMat;
            var frame = FrameMat;
            var iron = IronMat;
            var ironLight = IronLightMat;
            var coal = art.Lit(Hex("#26221f"));
            var soot = art.NoOutline(Hex("#625b53"));
            var rope = RopeMat;

            // flagstone pad with dark soot under the forge and the anvil
            prim.BoxOnGround(ground, stoneDark, Vector3.zero, new Vector3(1.9f, 0.05f, 1.9f));
            for (int i = 0; i < 9; i++)
            {
                float x = -0.7f + (i % 3) * 0.7f + ((float)rng.NextDouble() - 0.5f) * 0.1f;
                float z = -0.7f + (i / 3) * 0.7f + ((float)rng.NextDouble() - 0.5f) * 0.1f;
                prim.BoxOnGround(ground, stone, new Vector3(x, 0.05f, z), new Vector3(0.52f, 0.02f, 0.5f), new Vector3(0f, ((float)rng.NextDouble() - 0.5f) * 8f, 0f));
            }
            prim.Sphere(ground, soot, new Vector3(0.42f, 0.075f, -0.42f), new Vector3(0.55f, 0.012f, 0.42f), false);
            prim.Sphere(ground, soot, new Vector3(-0.55f, 0.075f, 0.28f), new Vector3(0.7f, 0.012f, 0.34f), false);

            // the hut: stone back and left walls, timber corner posts, open front
            const float roofY = 0.95f;
            prim.BoxOnGround(body, stone, new Vector3(-0.325f, 0.07f, 0.76f), new Vector3(1.14f, 0.82f, 0.1f));
            prim.BoxOnGround(body, stone, new Vector3(-0.9f, 0.07f, 0.5f), new Vector3(0.1f, 0.82f, 0.62f));
            prim.BoxOnGround(body, stoneDark, new Vector3(-0.325f, 0.07f, 0.76f), new Vector3(1.2f, 0.07f, 0.14f));   // plinth
            prim.BoxOnGround(body, stoneDark, new Vector3(-0.9f, 0.07f, 0.5f), new Vector3(0.14f, 0.07f, 0.68f));
            foreach (var p in new[] { new Vector2(-0.9f, 0.2f), new Vector2(0.25f, 0.2f), new Vector2(0.25f, 0.76f), new Vector2(-0.9f, 0.76f) })
                prim.BoxOnGround(body, frame, new Vector3(p.x, 0.07f, p.y), new Vector3(0.12f, 0.88f, 0.12f));
            prim.Box(body, frame, new Vector3(-0.325f, roofY - 0.04f, 0.2f), new Vector3(1.2f, 0.08f, 0.08f), default, false);   // front lintel
            prim.Box(body, frame, new Vector3(0.25f, roofY - 0.04f, 0.5f), new Vector3(0.08f, 0.08f, 0.62f), default, false);
            for (int i = 0; i < 4; i++) // stone courses on the left wall
                prim.Box(body, stoneDark, new Vector3(-0.955f, 0.2f + i * 0.2f, 0.5f), new Vector3(0.02f, 0.025f, 0.56f), default, false);
            Window(body, new Vector3(-0.955f, 0.5f, 0.5f), 0.2f, 0.2f, true);

            // the forge: stone hearth with glowing coals, a hood into the chimney, bellows beside it
            prim.BoxOnGround(body, stone, new Vector3(-0.55f, 0.07f, 0.54f), new Vector3(0.7f, 0.42f, 0.34f));
            prim.BoxOnGround(body, stoneDark, new Vector3(-0.55f, 0.49f, 0.54f), new Vector3(0.78f, 0.05f, 0.42f));
            prim.Box(body, EmberMat, new Vector3(-0.55f, 0.535f, 0.52f), new Vector3(0.52f, 0.045f, 0.26f), default, false);
            for (int i = 0; i < 5; i++)
                prim.Sphere(body, i % 2 == 0 ? coal : EmberMat, new Vector3(-0.72f + i * 0.085f, 0.56f, 0.46f + (i % 2) * 0.1f), new Vector3(0.1f, 0.06f, 0.1f), false);
            prim.Box(body, stoneDark, new Vector3(-0.55f, 0.76f, 0.6f), new Vector3(0.44f, 0.36f, 0.24f));      // hood
            prim.Box(body, stone, new Vector3(-0.55f, 0.95f, 0.62f), new Vector3(0.3f, 0.2f, 0.24f));
            prim.Box(body, IronMat, new Vector3(-0.55f, 0.6f, 0.4f), new Vector3(0.5f, 0.03f, 0.03f), default, false);   // hood rim
            // bellows
            prim.Box(body, LeatherMat, new Vector3(-0.08f, 0.26f, 0.55f), new Vector3(0.36f, 0.12f, 0.2f), default, false);
            prim.Box(body, LeatherMat, new Vector3(-0.08f, 0.36f, 0.55f), new Vector3(0.32f, 0.07f, 0.17f), new Vector3(0f, 0f, 6f), false);
            prim.Cylinder(body, iron, new Vector3(-0.27f, 0.3f, 0.55f), 0.025f, 0.2f, new Vector3(0f, 0f, 90f), false);
            prim.Box(body, WoodMat, new Vector3(0.12f, 0.36f, 0.55f), new Vector3(0.22f, 0.04f, 0.04f), new Vector3(0f, 0f, 14f), false);
            prim.BoxOnGround(body, WoodMat, new Vector3(-0.08f, 0.07f, 0.55f), new Vector3(0.3f, 0.14f, 0.14f));

            // tool rack on the back wall: tongs, hammers, a file
            prim.Box(body, frame, new Vector3(0.0f, 0.62f, 0.69f), new Vector3(0.5f, 0.04f, 0.04f), default, false);
            foreach (var x in new[] { -0.15f, 0.0f, 0.13f, 0.22f })
            {
                prim.Box(body, iron, new Vector3(x, 0.5f, 0.68f), new Vector3(0.02f, 0.2f, 0.015f), default, false);
                prim.Box(body, ironLight, new Vector3(x, 0.37f, 0.675f), new Vector3(x < 0.1f ? 0.07f : 0.03f, 0.05f, 0.03f), default, false);
            }

            // slate gable roof with thick edge boards and a ridge
            float ridgeY = roofY + 0.5f;
            prim.MeshObject(roof, ProceduralMeshes.GableRoof(1.28f, 0.95f, 0.46f, 0.3f, 0.06f), SlateMat, new Vector3(-0.325f, roofY, 0.5f));
            foreach (var z in new[] { -1f, 1f })
                prim.Box(roof, SlateLightMat, new Vector3(-0.325f, roofY + 0.045f, 0.5f + z * (0.475f + 0.055f)), new Vector3(1.35f, 0.06f, 0.055f), default, false);
            foreach (var sx in new[] { -0.325f - 0.54f, -0.325f + 0.54f })
            {
                prim.Add(roof, ProceduralMeshes.GableWall(0.65f, 0.38f, 0.05f), stone, new Vector3(sx, roofY, 0.5f), Vector3.one, default, "GableWall");
                prim.BoxOnGround(roof, frame, new Vector3(sx + Mathf.Sign(sx + 0.325f) * 0.03f, roofY, 0.5f), new Vector3(0.04f, 0.36f, 0.04f));
            }
            prim.Cylinder(roof, SlateLightMat, new Vector3(-0.325f, ridgeY - 0.04f, 0.5f), 0.06f, 1.3f, new Vector3(0f, 0f, 90f));
            foreach (var x in new[] { -0.85f, -0.55f, -0.25f, 0.05f, 0.2f })   // rafter tips under the front eave
                prim.Box(roof, frame, new Vector3(x, roofY - 0.03f, 0.1f), new Vector3(0.04f, 0.045f, 0.1f), default, false);
            Chimney(root, roof, new Vector3(-0.55f, roofY + 0.05f, 0.62f), 0.72f);

            // front yard: anvil on a stump with a hammer and tongs, quench barrel, grindstone, charcoal basket, iron bars
            prim.Cylinder(props, art.Lit(Palette.TimberLight, PaintedTexture.Bark), new Vector3(0.45f, 0.17f, -0.45f), 0.19f, 0.3f);
            prim.Cylinder(props, LogEndMat, new Vector3(0.45f, 0.322f, -0.45f), 0.17f, 0.012f, default, false);
            prim.Box(props, iron, new Vector3(0.45f, 0.36f, -0.45f), new Vector3(0.26f, 0.06f, 0.16f));            // anvil foot
            prim.Box(props, iron, new Vector3(0.45f, 0.43f, -0.45f), new Vector3(0.14f, 0.09f, 0.11f));            // waist
            prim.Box(props, ironLight, new Vector3(0.45f, 0.5f, -0.45f), new Vector3(0.38f, 0.06f, 0.15f));        // face
            prim.Cylinder(props, ironLight, new Vector3(0.7f, 0.5f, -0.45f), 0.045f, 0.18f, new Vector3(0f, 0f, 90f));   // horn
            prim.Sphere(props, ironLight, new Vector3(0.79f, 0.5f, -0.45f), 0.03f);
            prim.Box(props, WoodMat, new Vector3(0.36f, 0.56f, -0.47f), new Vector3(0.2f, 0.03f, 0.03f), new Vector3(0f, 10f, 0f), false);   // hammer
            prim.Box(props, iron, new Vector3(0.27f, 0.575f, -0.47f), new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 10f, 0f), false);
            for (int i = 0; i < 3; i++)   // glowing offcuts and sparks on the flagstones
                prim.Sphere(props, EmberMat, new Vector3(0.1f + i * 0.17f, 0.065f, -0.62f + (i % 2) * 0.12f), new Vector3(0.05f, 0.025f, 0.05f), false);

            prim.Cylinder(props, WoodMat, new Vector3(0.8f, 0.2f, 0.0f), 0.17f, 0.34f);     // quench barrel
            foreach (var y in new[] { 0.09f, 0.31f }) prim.Cylinder(props, iron, new Vector3(0.8f, y, 0.0f), 0.176f, 0.025f, default, false);
            prim.Cylinder(props, art.NoOutline(Palette.WaterDeep), new Vector3(0.8f, 0.34f, 0.0f), 0.15f, 0.012f, default, false);
            prim.Box(props, iron, new Vector3(0.72f, 0.4f, 0.0f), new Vector3(0.025f, 0.28f, 0.025f), new Vector3(0f, 0f, 24f), false);   // tongs hooked on the rim

            // grindstone on a frame
            foreach (var z in new[] { -0.07f, 0.07f })
                prim.Box(props, WoodMat, new Vector3(-0.55f, 0.15f, -0.55f + z), new Vector3(0.05f, 0.3f, 0.05f), default, false);
            prim.Cylinder(props, stoneDark, new Vector3(-0.55f, 0.28f, -0.55f), 0.13f, 0.06f, new Vector3(90f, 0f, 0f));
            prim.Box(props, WoodMat, new Vector3(-0.55f, 0.15f, -0.55f), new Vector3(0.05f, 0.04f, 0.2f), default, false);

            // charcoal basket and iron bars
            prim.Cylinder(props, art.Lit(Hex("#b98a4a"), PaintedTexture.Thatch), new Vector3(0.78f, 0.12f, 0.55f), 0.13f, 0.18f);
            for (int i = 0; i < 4; i++)
                prim.Sphere(props, coal, new Vector3(0.74f + (i % 2) * 0.07f, 0.23f + (i / 2) * 0.03f, 0.52f + (i / 2) * 0.06f), 0.045f);
            for (int i = 0; i < 3; i++)
                prim.Box(props, iron, new Vector3(0.5f, 0.075f + i * 0.04f, 0.0f), new Vector3(0.36f, 0.035f, 0.035f), new Vector3(0f, 8f * (i - 1), 0f), false);

            // a horseshoe over the entrance and a coil of rope on the front post
            Horseshoe(body, new Vector3(-0.325f, 0.74f, 0.15f), 0.075f, level >= 3 ? GoldTrimMat : ironLight);
            prim.Cylinder(body, rope, new Vector3(0.3f, 0.5f, 0.18f), 0.045f, 0.03f, new Vector3(90f, 0f, 0f), false);
            PaperLantern(body, new Vector3(0.25f, 0.7f, 0.08f), 0.7f);

            if (level >= 2)
            {
                // a lean-to over the yard so the smith works in the rain, a second lantern and more tools on the wall
                foreach (var p in new[] { new Vector2(0.95f, -0.9f), new Vector2(0.95f, 0.1f) })
                    prim.BoxOnGround(body, frame, new Vector3(p.x - 0.05f, 0.07f, p.y), new Vector3(0.08f, 0.78f, 0.08f));
                prim.Box(roof, SlateMat, new Vector3(0.58f, 0.9f, -0.4f), new Vector3(0.8f, 0.05f, 1.2f), new Vector3(0f, 0f, -12f));
                prim.Box(roof, frame, new Vector3(0.58f, 0.84f, -0.88f), new Vector3(0.76f, 0.04f, 0.04f), new Vector3(0f, 0f, -12f), false);
                PaperLantern(body, new Vector3(0.6f, 0.66f, -0.78f), 0.65f);
                prim.Box(body, ironLight, new Vector3(-0.945f, 0.62f, 0.35f), new Vector3(0.012f, 0.07f, 0.3f), default, false);       // saw on the left wall
                foreach (var z in new[] { 0.22f, 0.5f }) prim.Box(body, iron, new Vector3(-0.945f, 0.62f, z), new Vector3(0.02f, 0.1f, 0.03f), default, false);
            }
            if (level >= 3)
            {
                // gilded sign, a gold cap on the chimney and a second horseshoe
                foreach (var x in new[] { -0.9f, -0.62f })
                    prim.Box(body, rope, new Vector3(x, 0.8f, 0.14f), new Vector3(0.012f, 0.1f, 0.012f), default, false);
                prim.Box(body, art.Lit(Palette.BannerRed), new Vector3(-0.76f, 0.66f, 0.14f), new Vector3(0.34f, 0.22f, 0.025f), default, false);
                Horseshoe(body, new Vector3(-0.76f, 0.66f, 0.115f), 0.065f, GoldTrimMat);
                prim.Sphere(roof, GoldTrimMat, new Vector3(-0.55f, roofY + 0.88f, 0.62f), 0.05f);
            }

            if (Props != null)
            {
                Bush(trees, new Vector3(-0.92f, 0f, -0.8f), 0.6f, rng);
                Bush(trees, new Vector3(0.9f, 0f, 0.9f), 0.65f, rng);
            }
            return root;
        }

        // ---------------------------------------------------------------- Fisher Dock (2x4): a hut and drying rack on land, a pier into the river

        private GameObject FisherDock(int level, int w, int d)
        {
            prim.ThinBoxes = true;
            try { return BuildFisherDock(level); }
            finally { prim.ThinBoxes = false; }
        }

        private GameObject BuildFisherDock(int level)
        {
            var rng = Rng("fisher_dock");
            var root = NewRoot("FisherDock_Model", 1.7f, 1.1f);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var plank = BeamMat;
            var plankLight = WoodMat;
            var dark = FrameMat;
            var rope = RopeMat;
            var net = art.Lit(Hex("#c9b98a"));
            var silver = art.Lit(Hex("#b9c7d1"));
            var cork = art.Lit(Hex("#d6a45a"));

            // land end (z -2 .. 0): trodden earth, flat stones from the hut to the pier
            prim.BoxOnGround(ground, art.Lit(Palette.DirtDark, PaintedTexture.Earth), new Vector3(0f, 0f, -1.0f), new Vector3(1.9f, 0.05f, 1.9f));
            for (int i = 0; i < 4; i++)
                prim.BoxOnGround(ground, StoneMat, new Vector3(0.05f + (i % 2) * 0.12f, 0.05f, -1.45f + i * 0.35f), new Vector3(0.34f, 0.025f, 0.26f), new Vector3(0f, i * 17f, 0f));
            StoneLine(ground, new Vector3(-0.92f, 0.06f, -1.9f), new Vector3(-0.92f, 0.06f, -0.15f), 7, 0.1f, rng, StoneMat);

            // the hut: plank walls on a stone footing, a thatched gable roof, door and window
            const float hx = -0.38f, hz = -1.2f, roofY = 0.85f;
            prim.BoxOnGround(body, StoneDarkMat, new Vector3(hx, 0.05f, hz), new Vector3(1.0f, 0.1f, 0.9f));
            prim.BoxOnGround(body, plank, new Vector3(hx, 0.1f, hz), new Vector3(0.92f, 0.72f, 0.82f));
            for (int i = 0; i < 6; i++)
                prim.Box(body, dark, new Vector3(hx - 0.4f + i * 0.16f, 0.46f, hz - 0.415f), new Vector3(0.025f, 0.7f, 0.02f), default, false);
            for (int i = 0; i < 5; i++)
                prim.Box(body, dark, new Vector3(hx - 0.465f, 0.46f, hz - 0.33f + i * 0.17f), new Vector3(0.02f, 0.7f, 0.025f), default, false);
            foreach (var p in new[] { new Vector2(hx - 0.47f, hz - 0.42f), new Vector2(hx + 0.47f, hz - 0.42f), new Vector2(hx + 0.47f, hz + 0.42f), new Vector2(hx - 0.47f, hz + 0.42f) })
                prim.BoxOnGround(body, dark, new Vector3(p.x, 0.1f, p.y), new Vector3(0.1f, 0.76f, 0.1f));
            prim.Box(body, DoorMat, new Vector3(hx + 0.15f, 0.34f, hz - 0.425f), new Vector3(0.28f, 0.48f, 0.04f));
            prim.Box(body, dark, new Vector3(hx + 0.15f, 0.6f, hz - 0.44f), new Vector3(0.34f, 0.04f, 0.04f), default, false);
            Window(body, new Vector3(hx - 0.475f, 0.5f, hz), 0.22f, 0.2f, true);
            prim.Box(body, plankLight, new Vector3(hx - 0.51f, 0.38f, hz), new Vector3(0.07f, 0.03f, 0.34f), default, false);   // sill

            prim.MeshObject(roof, ProceduralMeshes.GableRoof(1.14f, 1.12f, 0.46f, 0.32f, 0.05f), ThatchMat, new Vector3(hx, roofY, hz));
            foreach (var z in new[] { -1f, 1f })
                prim.Box(roof, ThatchDarkMat, new Vector3(hx, roofY + 0.04f, hz + z * 0.6f), new Vector3(1.22f, 0.055f, 0.05f), default, false);
            foreach (var sx in new[] { hx - 0.5f, hx + 0.5f })
                prim.Add(roof, ProceduralMeshes.GableWall(0.94f, 0.36f, 0.05f), PlasterMat, new Vector3(sx, roofY, hz), Vector3.one, default, "GableWall");
            prim.Cylinder(roof, ThatchDarkMat, new Vector3(hx, roofY + 0.48f, hz), 0.065f, 1.16f, new Vector3(0f, 0f, 90f));
            var bands = new GameObject("RidgeBands").transform;
            bands.SetParent(roof, false);
            bands.localPosition = new Vector3(hx, 0f, hz);
            RidgeBands(bands, roofY + 0.48f, 1.16f, 0.072f, art.Lit(Hex("#7a5a2a"), PaintedTexture.Thatch));
            for (int i = 0; i < 6; i++)   // rafter tips under the front eave
                prim.Box(roof, dark, new Vector3(hx - 0.5f + i * 0.2f, roofY - 0.03f, hz - 0.52f), new Vector3(0.04f, 0.045f, 0.1f), default, false);
            PaperLantern(body, new Vector3(hx + 0.55f, 0.6f, hz - 0.52f), 0.7f);

            // net drying rack with floats, and fish hung up to dry
            foreach (var z in new[] { -1.65f, -0.65f })
                prim.BoxOnGround(props, dark, new Vector3(0.62f, 0.05f, z), new Vector3(0.07f, 0.78f, 0.07f));
            prim.Box(props, dark, new Vector3(0.62f, 0.8f, -1.15f), new Vector3(0.06f, 0.06f, 1.1f), default, false);
            for (int i = 0; i < 9; i++)
                prim.Box(props, net, new Vector3(0.62f, 0.54f - (i % 3) * 0.0f, -1.6f + i * 0.12f), new Vector3(0.012f, 0.5f, 0.012f), default, false);
            for (int i = 0; i < 4; i++)
                prim.Box(props, net, new Vector3(0.62f, 0.8f - i * 0.13f, -1.15f), new Vector3(0.012f, 0.012f, 1.0f), default, false);
            for (int i = 0; i < 6; i++)
                prim.Sphere(props, cork, new Vector3(0.64f, 0.8f - 0.03f, -1.58f + i * 0.2f), 0.03f);
            prim.Box(props, dark, new Vector3(0.28f, 0.66f, -0.3f), new Vector3(0.55f, 0.04f, 0.04f), default, false);   // a short pole with fish
            foreach (var x in new[] { 0.1f, 0.28f, 0.46f })
            {
                prim.Box(props, rope, new Vector3(x, 0.58f, -0.3f), new Vector3(0.008f, 0.14f, 0.008f), default, false);
                prim.Sphere(props, silver, new Vector3(x, 0.45f, -0.3f), new Vector3(0.05f, 0.18f, 0.04f), false);
            }

            // baskets with the catch, a barrel, a crate, oars and rods against the hut
            for (int i = 0; i < 2; i++)
            {
                float bx = 0.85f, bz = -0.2f - i * 0.28f;
                prim.Cylinder(props, art.Lit(Hex("#b98a4a"), PaintedTexture.Thatch), new Vector3(bx, 0.1f, bz), 0.12f, 0.16f);
                for (int f = 0; f < 3; f++)
                    prim.Sphere(props, silver, new Vector3(bx - 0.04f + f * 0.04f, 0.2f, bz + (f % 2) * 0.03f), new Vector3(0.05f, 0.04f, 0.14f), false);
            }
            prim.Cylinder(props, WoodMat, new Vector3(-0.82f, 0.16f, -0.35f), 0.11f, 0.24f);
            foreach (var y in new[] { 0.09f, 0.22f }) prim.Cylinder(props, IronMat, new Vector3(-0.82f, y, -0.35f), 0.115f, 0.02f, default, false);
            prim.Box(props, plank, new Vector3(-0.55f, 0.1f, -0.2f), new Vector3(0.24f, 0.18f, 0.2f));
            foreach (var z in new[] { -0.3f, -0.18f })   // two oars leaning on the front-right corner of the hut
            {
                prim.Box(props, plankLight, new Vector3(hx + 0.52f, 0.45f, hz + z), new Vector3(0.025f, 0.8f, 0.025f), new Vector3(0f, 0f, -8f), false);
                prim.Box(props, plankLight, new Vector3(hx + 0.57f, 0.12f, hz + z), new Vector3(0.05f, 0.2f, 0.12f), new Vector3(0f, 0f, -8f), false);
            }
            foreach (var z in new[] { 0.1f, 0.2f })   // fishing rods against the left wall
                prim.Cylinder(props, art.Lit(Palette.Bamboo), new Vector3(hx - 0.54f, 0.5f, hz + z), 0.012f, 0.95f, new Vector3(12f, 0f, 14f), false);

            // the pier (z -0.3 .. 1.95): planks over two stringers, posts down into the water, bollards, a ladder and a lantern at the end
            const float deckY = 0.2f;
            int planks = 15;
            for (int i = 0; i < planks; i++)
                prim.Box(body, i % 2 == 0 ? plank : plankLight, new Vector3(0f, deckY, -0.25f + i * 0.152f), new Vector3(0.92f, 0.05f, 0.138f));
            foreach (var x in new[] { -0.44f, 0.44f })
            {
                prim.Box(body, dark, new Vector3(x, deckY - 0.06f, 0.85f), new Vector3(0.07f, 0.07f, 2.3f), default, false);
                foreach (var z in new[] { -0.15f, 0.6f, 1.35f, 1.9f })
                {
                    prim.Cylinder(body, art.Lit(Palette.Bark, PaintedTexture.Bark), new Vector3(x, 0.12f, z), 0.055f, 0.58f);
                    prim.Cylinder(body, LogEndMat, new Vector3(x, 0.412f, z), 0.045f, 0.012f, default, false);
                }
            }
            foreach (var z in new[] { 0.6f, 1.35f })
                prim.Box(body, dark, new Vector3(0f, deckY - 0.1f, z), new Vector3(0.9f, 0.05f, 0.05f), default, false);   // cross beams under the deck
            prim.Cylinder(body, art.Lit(Palette.Bark, PaintedTexture.Bark), new Vector3(0.44f, 0.5f, 1.9f), 0.06f, 0.78f);   // the tall mooring posts
            prim.Cylinder(body, art.Lit(Palette.Bark, PaintedTexture.Bark), new Vector3(-0.44f, 0.5f, 1.9f), 0.06f, 0.78f);
            prim.Sphere(body, art.Lit(Palette.Bark, PaintedTexture.Bark), new Vector3(0.44f, 0.9f, 1.9f), 0.06f);
            prim.Sphere(body, art.Lit(Palette.Bark, PaintedTexture.Bark), new Vector3(-0.44f, 0.9f, 1.9f), 0.06f);
            prim.Cylinder(body, rope, new Vector3(0.44f, 0.55f, 1.9f), 0.068f, 0.05f, default, false);
            prim.Cylinder(body, rope, new Vector3(-0.44f, 0.55f, 1.9f), 0.068f, 0.05f, default, false);
            // a rope sagging between the end posts
            var ropePath = new System.Collections.Generic.List<Vector3>();
            var ropeRadii = new System.Collections.Generic.List<float>();
            for (int i = 0; i <= 6; i++)
            {
                float u = i / 6f;
                ropePath.Add(new Vector3(Mathf.Lerp(-0.44f, 0.44f, u), 0.75f - Mathf.Sin(u * Mathf.PI) * 0.12f, 1.9f));
                ropeRadii.Add(0.012f);
            }
            prim.MeshObject(body, ProceduralMeshes.Tube(ropePath, ropeRadii, 4), rope, default);
            PaperLantern(body, new Vector3(0.44f, 0.98f, 1.9f), 0.8f);
            // bollards and a coil of rope on the deck, a stool and a crate of fish near the end
            foreach (var z in new[] { 0.35f, 1.1f })
                prim.Cylinder(body, dark, new Vector3(-0.38f, deckY + 0.1f, z), 0.045f, 0.16f);
            prim.Cylinder(body, rope, new Vector3(0.28f, deckY + 0.05f, 1.55f), 0.1f, 0.06f);
            prim.Cylinder(body, WoodMat, new Vector3(-0.12f, deckY + 0.14f, 0.35f), 0.09f, 0.04f);
            foreach (var p in new[] { new Vector2(-0.17f, 0.3f), new Vector2(-0.07f, 0.4f), new Vector2(-0.07f, 0.3f), new Vector2(-0.17f, 0.4f) })
                prim.Box(props, dark, new Vector3(p.x, deckY + 0.07f, p.y), new Vector3(0.02f, 0.14f, 0.02f), default, false);
            prim.Box(props, plankLight, new Vector3(0.0f, deckY + 0.075f, 1.0f), new Vector3(0.3f, 0.1f, 0.22f));
            for (int f = 0; f < 3; f++)
                prim.Sphere(props, silver, new Vector3(-0.07f + f * 0.07f, deckY + 0.14f, 1.0f), new Vector3(0.05f, 0.04f, 0.16f), false);
            for (int i = 0; i < 4; i++)   // ladder rungs on the side of the pier
                prim.Box(body, dark, new Vector3(0.465f, deckY - 0.04f - i * 0.07f, 0.9f), new Vector3(0.02f, 0.02f, 0.2f), default, false);

            if (Props != null)
            {
                Bush(trees, new Vector3(0.9f, 0f, -1.85f), 0.65f, rng);
                var reeds = Props.Reeds(trees, new Vector3(-0.9f, 0f, 0.15f), rng);
                reeds.transform.localScale = Vector3.one * 0.7f;
            }
            return root;
        }

        // ---------------------------------------------------------------- Mine (2x3): a timbered tunnel in the rock face at the far (+Z) end

        private GameObject Mine(int level, int w, int d)
        {
            prim.ThinBoxes = true;
            try { return BuildMine(level); }
            finally { prim.ThinBoxes = false; }
        }

        private GameObject BuildMine(int level)
        {
            var rng = Rng("mine");
            var root = NewRoot("Mine_Model", 1.9f, 1.3f);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var stone = StoneMat;
            var rockA = art.Lit(Palette.RockA, PaintedTexture.Stone);
            var rockB = art.Lit(Palette.RockB, PaintedTexture.Stone);
            var rockC = art.Lit(Palette.RockC, PaintedTexture.Stone);
            var dark = FrameMat;
            var wood = WoodMat;
            var iron = IronMat;
            var ore = art.Lit(Hex("#8e5a3c"));
            var oreLight = art.Lit(Hex("#b9825a"));
            var black = art.Lit(Hex("#120e0c"));

            // trodden earth in front of the tunnel, spoil heaps at its sides
            prim.BoxOnGround(ground, art.Lit(Palette.DirtDark, PaintedTexture.Earth), new Vector3(0f, 0f, -0.45f), new Vector3(1.9f, 0.05f, 2.1f));

            // the mountain: overlapping lumps that fill the last cell, a crest on top, and the arch of the tunnel mouth in its face
            var lumps = new[]
            {
                new Vector4(-0.55f, 0.95f, 0.95f, 1.2f), new Vector4(0.55f, 0.95f, 0.95f, 1.35f), new Vector4(0f, 1.1f, 1.0f, 1.55f),
                new Vector4(-0.62f, 0.62f, 0.7f, 0.85f), new Vector4(0.62f, 0.62f, 0.7f, 0.9f), new Vector4(0f, 0.9f, 0.85f, 1.1f)
            };
            for (int i = 0; i < lumps.Length; i++)
            {
                var l = lumps[i];
                var mat = i % 3 == 0 ? rockA : i % 3 == 1 ? rockB : rockC;
                var lump = prim.Sphere(body, mat, new Vector3(l.x, l.w * 0.32f, l.y), new Vector3(l.z * 1.05f, l.w, 0.8f));
                lump.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 40f, 0f);
            }
            prim.Sphere(body, rockC, new Vector3(0.1f, 1.45f, 1.2f), new Vector3(0.55f, 0.32f, 0.5f), false);

            // tunnel mouth: dark opening, timber frame with a lintel and planks, a lantern
            const float mouthZ = 0.62f;
            prim.Box(body, black, new Vector3(0f, 0.34f, mouthZ + 0.02f), new Vector3(0.62f, 0.68f, 0.1f), default, false);
            prim.Sphere(body, black, new Vector3(0f, 0.66f, mouthZ + 0.02f), new Vector3(0.62f, 0.3f, 0.1f), false);
            foreach (var x in new[] { -0.36f, 0.36f })
                prim.BoxOnGround(body, dark, new Vector3(x, 0f, mouthZ - 0.02f), new Vector3(0.1f, 0.74f, 0.1f));
            prim.Box(body, dark, new Vector3(0f, 0.78f, mouthZ - 0.02f), new Vector3(0.86f, 0.1f, 0.1f));
            foreach (var x in new[] { -0.3f, 0.3f })
                prim.Box(body, dark, new Vector3(x * 0.7f, 0.68f, mouthZ - 0.03f), new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0f, 0f, x > 0 ? 40f : -40f), false);
            for (int i = 0; i < 4; i++) prim.Box(body, wood, new Vector3(-0.3f + i * 0.2f, 0.9f, mouthZ - 0.04f), new Vector3(0.16f, 0.05f, 0.07f), default, false);
            PaperLantern(body, new Vector3(0.4f, 0.64f, mouthZ - 0.16f), 0.75f);

            // rails out of the tunnel with sleepers, an ore cart on them
            foreach (var x in new[] { -0.15f, 0.15f })
                prim.Box(ground, iron, new Vector3(x, 0.075f, -0.45f), new Vector3(0.035f, 0.03f, 2.0f), default, false);
            for (int i = 0; i < 11; i++)
                prim.Box(ground, dark, new Vector3(0f, 0.058f, -1.3f + i * 0.17f), new Vector3(0.46f, 0.03f, 0.07f), default, false);
            prim.Box(props, art.Lit(Palette.Plank, PaintedTexture.Planks), new Vector3(0f, 0.25f, -0.35f), new Vector3(0.36f, 0.2f, 0.5f));
            prim.Box(props, iron, new Vector3(0f, 0.36f, -0.35f), new Vector3(0.39f, 0.03f, 0.53f), default, false);
            foreach (var p in new[] { new Vector2(-0.18f, -0.52f), new Vector2(0.18f, -0.52f), new Vector2(-0.18f, -0.18f), new Vector2(0.18f, -0.18f) })
                prim.Cylinder(props, iron, new Vector3(p.x * 1.05f, 0.12f, p.y), 0.07f, 0.04f, new Vector3(0f, 0f, 90f), false);
            for (int i = 0; i < 5; i++)
                prim.Sphere(props, i % 2 == 0 ? ore : oreLight, new Vector3(-0.1f + (i % 3) * 0.1f, 0.4f + (i / 3) * 0.05f, -0.45f + (i % 2) * 0.16f), new Vector3(0.14f, 0.1f, 0.14f), false);

            // ore piles, a barrel, a crate and pickaxes against the left support
            for (int i = 0; i < 6; i++)
                prim.Sphere(props, i % 2 == 0 ? ore : oreLight, new Vector3(-0.75f + (i % 3) * 0.13f, 0.08f + (i / 3) * 0.07f, -0.2f + (i % 2) * 0.12f), new Vector3(0.18f, 0.12f, 0.16f), false);
            for (int i = 0; i < 4; i++)
                prim.Sphere(props, i % 2 == 0 ? ore : stone, new Vector3(0.7f + (i % 2) * 0.1f, 0.07f + (i / 2) * 0.06f, 0.1f + (i % 2) * 0.1f), new Vector3(0.18f, 0.11f, 0.16f), false);
            prim.Cylinder(props, wood, new Vector3(0.78f, 0.17f, -0.5f), 0.12f, 0.26f);
            foreach (var y in new[] { 0.09f, 0.24f }) prim.Cylinder(props, iron, new Vector3(0.78f, y, -0.5f), 0.126f, 0.02f, default, false);
            prim.Box(props, art.Lit(Palette.Plank, PaintedTexture.Planks), new Vector3(-0.72f, 0.1f, -0.75f), new Vector3(0.26f, 0.18f, 0.22f));
            foreach (var x in new[] { -0.62f, -0.54f })
            {
                prim.Box(props, wood, new Vector3(x, 0.4f, mouthZ - 0.2f), new Vector3(0.025f, 0.7f, 0.025f), new Vector3(0f, 0f, x < -0.58f ? -14f : -10f), false);
                prim.Box(props, iron, new Vector3(x + 0.07f, 0.73f, mouthZ - 0.2f), new Vector3(0.16f, 0.035f, 0.035f), new Vector3(0f, 0f, 10f), false);
            }
            prim.Box(props, wood, new Vector3(-0.6f, 0.55f, -1.2f), new Vector3(0.05f, 1.0f, 0.05f), default, false);   // signpost with a pick
            prim.Box(props, art.Lit(Palette.TimberLight, PaintedTexture.Planks), new Vector3(-0.6f, 0.9f, -1.2f), new Vector3(0.05f, 0.2f, 0.34f), default, false);
            prim.Box(props, iron, new Vector3(-0.6f, 0.9f, -1.2f), new Vector3(0.07f, 0.04f, 0.2f), default, false);

            if (Props != null)
            {
                Bush(trees, new Vector3(-0.9f, 0f, -1.3f), 0.6f, rng);
                Bush(trees, new Vector3(0.88f, 0f, -1.25f), 0.65f, rng);
            }
            return root;
        }

        // ---------------------------------------------------------------- Road (1x1): the surface is a mesh that follows the neighbouring roads

        private GameObject Road(int level, int w, int d)
        {
            var root = NewRoot("Road_Model", 0.35f, 0.18f);
            var surface = new GameObject("Surface");
            surface.transform.SetParent(root.transform, false);
            surface.AddComponent<MeshFilter>();
            var mr = surface.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var road = surface.AddComponent<RoadSurface>();
            road.Setup(art.NoOutline(Hex("#86796a"), PaintedTexture.Stone), art.NoOutline(Hex("#c4b9a4"), PaintedTexture.Stone));
            road.SetMask(0);
            return root;
        }
    }
}
