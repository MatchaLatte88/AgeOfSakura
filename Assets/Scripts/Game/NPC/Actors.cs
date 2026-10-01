using System;
using UnityEngine;

namespace AgeOfSakura.Game
{
    public enum NpcState
    {
        Idle,
        Walking
        // Future: Working, Carrying, Visiting
    }

    /// <summary>A character: its root plus a function that poses it for the current state.</summary>
    public sealed class ActorVisual
    {
        public GameObject Root;
        /// <summary>(state, seconds, walkPhase) - called every frame by the agent.</summary>
        public Action<NpcState, float, float> Animate;
    }

    /// <summary>
    /// Builds the villagers and animals of the world. Villagers are about 0.64f units tall with human proportions (six heads), jointed limbs
    /// (shoulder/elbow, hip/knee), period clothing (kimono, obi, hakama-style trousers, waraji sandals), hair styles and small props, so
    /// they read as people at the game's zoom and hold up when zoomed in. Agents only need an <see cref="ActorVisual"/>.
    /// </summary>
    public sealed class ActorFactory
    {
        private readonly GameArt art;
        private readonly Prim prim;

        public const int VillagerVariants = 6;

        public ActorFactory(GameArt art, Prim prim)
        {
            this.art = art;
            this.prim = prim;
        }

        private static Color Hex(string s) => Palette.Hex(s);

        private Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>Round tube along a path (capsule-like limbs, necks, tails), radii per point.</summary>
        private GameObject Limb(Transform parent, Material m, Vector3[] path, float[] radii, int sides = 8, float capReach = 0.9f) =>
            prim.MeshObject(parent, ProceduralMeshes.Tube(path, radii, sides, capReach), m, Vector3.zero, default);

        /// <summary>Smooth ellipsoid; parts whose middle extent is under 7 cm use the 80-triangle sphere (eyes, hands, ears, feathers), bigger ones the 320-triangle one.</summary>
        private GameObject Ball(Transform parent, Material m, Vector3 pos, Vector3 size, bool castShadows = true) =>
            prim.Add(parent, SecondLargest(size) < 0.07f ? art.SphereLow : art.Sphere, m, pos, size, default, "Ball", castShadows);

        /// <summary>The middle of the three extents: thin feathers and flat ears are small however long they are.</summary>
        private static float SecondLargest(Vector3 v) => Mathf.Max(Mathf.Min(v.x, v.y), Mathf.Min(Mathf.Max(v.x, v.y), v.z));

        private static Quaternion X(float degrees) => Quaternion.Euler(degrees, 0f, 0f);

        /// <summary>Merges the parts of every bone into a few meshes (a villager has about a hundred parts, but only ~15 bones that move).</summary>
        private static void MergeBones(Transform root)
        {
            var bones = new System.Collections.Generic.List<Transform>(root.GetComponentsInChildren<Transform>(true));
            foreach (var bone in bones)
            {
                if (bone == null || bone.GetComponent<MeshFilter>() != null) continue; // mesh parts are merged into their bone, then destroyed
                ModelBatcher.MergeDirect(bone);
            }
        }

        // ------------------------------------------------------------------ villagers (~0.64f units tall, forward = +Z)

        public ActorVisual Villager(int variant)
        {
            variant = ((variant % VillagerVariants) + VillagerVariants) % VillagerVariants;
            bool woman = variant == 1 || variant == 4;
            bool elder = variant == 2;
            bool monk = variant == 5;
            bool worker = variant == 3;
            bool farmer = variant == 0;

            Color clothColor, trimColor, obiColor, legColor, skinColor, hairColor;
            switch (variant)
            {
                case 0: clothColor = Palette.KimonoIndigo; trimColor = Hex("#e6dcc0"); obiColor = Hex("#b59b6a"); legColor = Hex("#3a3d4c"); skinColor = Hex("#d9aa7c"); hairColor = Palette.Hair; break;
                case 1: clothColor = Palette.KimonoRust; trimColor = Hex("#f0e6cc"); obiColor = Hex("#e0b84a"); legColor = Palette.KimonoRust; skinColor = Hex("#f0d3b1"); hairColor = Palette.Hair; break;
                case 2: clothColor = Palette.KimonoMoss; trimColor = Hex("#d8d2b8"); obiColor = Hex("#6b5a44"); legColor = Hex("#5a5346"); skinColor = Hex("#dcb48a"); hairColor = Hex("#e6e4dc"); break;
                case 3: clothColor = Hex("#b08a52"); trimColor = Hex("#efe6cf"); obiColor = Hex("#6a3f2a"); legColor = Hex("#4a4438"); skinColor = Hex("#d6a377"); hairColor = Palette.Hair; break;
                case 4: clothColor = Hex("#5b8fb0"); trimColor = Hex("#f4efe0"); obiColor = Palette.BannerRed; legColor = Hex("#5b8fb0"); skinColor = Hex("#f2d6b8"); hairColor = Palette.Hair; break;
                default: clothColor = Hex("#c9812f"); trimColor = Hex("#f0e3c0"); obiColor = Hex("#7b3f26"); legColor = Hex("#5a3b2a"); skinColor = Hex("#dcae82"); hairColor = Palette.Hair; break;
            }
            var cloth = art.Lit(clothColor);
            var trim = art.Lit(trimColor);
            var obi = art.Lit(obiColor);
            var legs = art.Lit(legColor);
            var skin = art.Lit(skinColor);
            var skinTiny = art.NoOutline(skinColor);
            var hair = art.Lit(hairColor);
            var dark = art.NoOutline(Hex("#2a2320"));
            var straw = art.Lit(Palette.Straw, PaintedTexture.Thatch);
            var strawPlain = art.Lit(Color.Lerp(Palette.Straw, Palette.StrawDark, 0.4f));
            var sandal = art.Lit(Hex("#a37f47"));
            var wood = art.Lit(Palette.TimberLight, PaintedTexture.Planks);
            var gold = art.NoOutline(Palette.Gold);
            var red = art.NoOutline(Palette.BannerRed);

            var root = new GameObject("Villager");
            var body = Pivot(root.transform, "Body", Vector3.zero);
            var torso = Pivot(body, "Torso", new Vector3(0f, 0.31f, 0f));

            // ---- torso in a kimono: elliptical tube, overlapping collar, obi with a knot or bow
            var chest = Limb(torso, cloth, new[] { new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.215f, 0f) },
                new[] { 0.062f, 0.055f, 0.061f, 0.064f }, 12, 0.5f);
            chest.transform.localScale = new Vector3(1.14f, 1f, 0.8f);
            foreach (var s in new[] { -1f, 1f })
            {
                var collar = prim.Box(torso, trim, new Vector3(s * 0.017f, 0.155f, 0.052f), new Vector3(0.011f, 0.1f, 0.007f), new Vector3(-6f, 0f, -s * 19f), false);
                collar.name = "Collar";
            }
            prim.Box(torso, trim, new Vector3(0f, 0.208f, 0.0f), new Vector3(0.05f, 0.012f, 0.05f), default, false);   // collar band round the neck
            var sash = prim.Cylinder(torso, obi, new Vector3(0f, 0.07f, 0f), 0.066f, woman ? 0.075f : 0.05f);
            sash.transform.localScale = new Vector3(1.14f, 1f, 0.8f);
            if (woman)
            {
                // musubi: a big flat bow on the back
                prim.Box(torso, obi, new Vector3(0f, 0.075f, -0.062f), new Vector3(0.11f, 0.07f, 0.03f));
                prim.Box(torso, obi, new Vector3(-0.04f, 0.07f, -0.06f), new Vector3(0.05f, 0.06f, 0.026f), new Vector3(0f, 0f, 12f));
                prim.Box(torso, obi, new Vector3(0.04f, 0.07f, -0.06f), new Vector3(0.05f, 0.06f, 0.026f), new Vector3(0f, 0f, -12f));
                prim.Box(torso, trim, new Vector3(0f, 0.075f, -0.078f), new Vector3(0.02f, 0.075f, 0.014f), default, false);
            }
            else
            {
                prim.Box(torso, obi, new Vector3(0.02f, 0.068f, -0.058f), new Vector3(0.04f, 0.032f, 0.026f));   // knot
                prim.Box(torso, obi, new Vector3(0.03f, 0.03f, -0.06f), new Vector3(0.012f, 0.05f, 0.012f), new Vector3(0f, 0f, 12f), false);
            }

            // ---- lower garment: short kimono over trousers, or a long narrow kimono
            var hips = Pivot(body, "Hips", new Vector3(0f, 0.31f, 0f));
            Transform skirt = null;
            float hem = woman ? -0.245f : (monk ? -0.2f : -0.115f);
            {
                skirt = Pivot(hips, "Skirt", Vector3.zero);
                var flare = woman ? 0.078f : 0.076f;
                var s = Limb(skirt, cloth, new[] { new Vector3(0f, 0.01f, 0f), new Vector3(0f, hem * 0.45f, 0f), new Vector3(0f, hem, 0f) },
                    new[] { 0.064f, 0.068f, flare }, 12, 0.05f);
                s.transform.localScale = new Vector3(1.12f, 1f, 0.86f);
                var trimBand = prim.Cylinder(skirt, trim, new Vector3(0f, hem + 0.008f, 0f), flare + 0.002f, 0.016f);
                trimBand.transform.localScale = new Vector3(1.12f, 1f, 0.86f);
            }

            // ---- head, neck, face, hair, hats
            Limb(torso, skin, new[] { new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.262f, 0.003f) }, new[] { 0.018f, 0.016f }, 8);
            var head = Pivot(torso, "Head", new Vector3(0f, 0.252f, 0f));
            var headBall = Ball(head, skin, new Vector3(0f, 0.05f, 0.004f), new Vector3(0.082f, 0.096f, 0.09f));
            headBall.name = "Head";
            Ball(head, skin, new Vector3(0f, 0.022f, 0.025f), new Vector3(0.062f, 0.05f, 0.06f), false);                         // jaw / cheeks
            Ball(head, skinTiny, new Vector3(0f, 0.043f, 0.047f), new Vector3(0.014f, 0.016f, 0.014f), false);                    // nose
            prim.Box(head, art.NoOutline(Color.Lerp(skinColor, Hex("#8a3f30"), 0.55f)), new Vector3(0f, 0.014f, 0.0455f), new Vector3(0.017f, 0.004f, 0.005f), default, false);   // mouth
            foreach (var s in new[] { -1f, 1f })
            {
                Ball(head, dark, new Vector3(s * 0.02f, 0.056f, 0.04f), new Vector3(0.0105f, 0.013f, 0.008f), false);              // eye
                prim.Box(head, dark, new Vector3(s * 0.02f, 0.071f, 0.041f), new Vector3(0.022f, 0.005f, 0.006f), new Vector3(0f, 0f, s * -6f), false); // brow
                Ball(head, skin, new Vector3(s * 0.041f, 0.048f, 0.0f), new Vector3(0.012f, 0.026f, 0.018f), false);                // ear
            }
            if (!monk)
            {
                // hair: cap over the back and top of the skull
                Ball(head, hair, new Vector3(0f, 0.062f, -0.012f), new Vector3(0.09f, 0.088f, 0.092f));
                if (woman)
                {
                    Ball(head, hair, new Vector3(0f, 0.105f, -0.03f), new Vector3(0.056f, 0.05f, 0.05f));                        // bun
                    prim.Cylinder(head, gold, new Vector3(0.02f, 0.108f, -0.036f), 0.004f, 0.075f, new Vector3(0f, 0f, 70f), false);  // kanzashi pin
                    Ball(head, red, new Vector3(0.056f, 0.108f, -0.036f), Vector3.one * 0.018f, false);
                    foreach (var s in new[] { -1f, 1f })
                        prim.Box(head, hair, new Vector3(s * 0.043f, 0.05f, 0.004f), new Vector3(0.01f, 0.05f, 0.026f), default, false);   // side locks
                }
                else
                {
                    prim.Cylinder(head, hair, new Vector3(0f, 0.11f, 0.016f), 0.014f, 0.036f, new Vector3(28f, 0f, 0f), false);   // top-knot (chonmage)
                    prim.Cylinder(head, gold, new Vector3(0f, 0.101f, 0.008f), 0.0155f, 0.008f, new Vector3(28f, 0f, 0f), false);
                    if (elder)
                    {
                        Ball(head, hair, new Vector3(0f, 0.006f, 0.048f), new Vector3(0.026f, 0.034f, 0.02f), false);            // wispy beard
                    }
                }
            }
            if (worker)
            {
                // tenugui headband with a knot at the back
                prim.Cylinder(head, art.Lit(Hex("#f2ece0")), new Vector3(0f, 0.076f, 0f), 0.0455f, 0.02f);
                prim.Box(head, art.Lit(Hex("#f2ece0")), new Vector3(0f, 0.076f, -0.05f), new Vector3(0.03f, 0.03f, 0.014f), new Vector3(0f, 0f, 20f), false);
            }
            if (farmer)
            {
                // kasa: wide woven straw hat with a chin strap
                var kasa = prim.Cone(head, straw, new Vector3(0f, 0.085f, 0f), 0.135f, 0.075f, true);
                kasa.transform.localPosition = new Vector3(0f, 0.076f, 0.004f);
                prim.Cylinder(head, strawPlain, new Vector3(0f, 0.078f, 0.004f), 0.05f, 0.02f);
                foreach (var s in new[] { -1f, 1f })
                    prim.Box(head, red, new Vector3(s * 0.04f, 0.028f, 0.02f), new Vector3(0.006f, 0.07f, 0.006f), new Vector3(0f, 0f, s * -10f), false);
            }

            // ---- arms: shoulder and elbow joints, sleeves, hands
            var shoulderL = Pivot(torso, "ShoulderL", new Vector3(-0.08f, 0.203f, 0f));
            var shoulderR = Pivot(torso, "ShoulderR", new Vector3(0.08f, 0.203f, 0f));
            var elbowL = Pivot(shoulderL, "ElbowL", new Vector3(0f, -0.105f, 0f));
            var elbowR = Pivot(shoulderR, "ElbowR", new Vector3(0f, -0.105f, 0f));
            bool bareForearms = worker || farmer;
            foreach (var (shoulder, elbow, side) in new[] { (shoulderL, elbowL, -1f), (shoulderR, elbowR, 1f) })
            {
                Ball(shoulder, cloth, Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f));
                Limb(shoulder, cloth, new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.105f, 0f) }, new[] { 0.024f, 0.021f }, 8);
                Limb(elbow, bareForearms ? skin : cloth, new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.098f, 0f) }, new[] { 0.02f, bareForearms ? 0.014f : 0.016f }, 8);
                if (!bareForearms)
                {
                    // wide kimono sleeve cuff
                    var cuff = Limb(elbow, cloth, new[] { new Vector3(0f, -0.03f, 0f), new Vector3(0f, -0.09f, 0f) }, new[] { 0.022f, 0.03f }, 8);
                    cuff.transform.localScale = new Vector3(0.9f, 1f, 1.25f);
                    prim.Cylinder(elbow, trim, new Vector3(0f, -0.09f, 0f), 0.029f, 0.005f, default, false).transform.localScale = new Vector3(0.9f, 1f, 1.25f);
                }
                Ball(elbow, skin, new Vector3(0f, -0.108f, 0.004f), new Vector3(0.03f, 0.034f, 0.032f));
            }
            if (bareForearms)
            {
                // rolled-up sleeves
                foreach (var e in new[] { elbowL, elbowR }) prim.Cylinder(e, cloth, new Vector3(0f, 0.008f, 0f), 0.026f, 0.03f);
            }

            // ---- legs: hip, knee, ankle; trousers, tabi socks, straw sandals
            var hipL = Pivot(body, "HipL", new Vector3(-0.038f, 0.31f, 0f));
            var hipR = Pivot(body, "HipR", new Vector3(0.038f, 0.31f, 0f));
            var kneeL = Pivot(hipL, "KneeL", new Vector3(0f, -0.135f, 0f));
            var kneeR = Pivot(hipR, "KneeR", new Vector3(0f, -0.135f, 0f));
            foreach (var (hip, knee) in new[] { (hipL, kneeL), (hipR, kneeR) })
            {
                Limb(hip, legs, new[] { new Vector3(0f, 0.005f, 0f), new Vector3(0f, -0.135f, 0f) }, new[] { 0.035f, 0.029f }, 8);
                Limb(knee, legs, new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.14f, 0f) }, new[] { 0.029f, woman ? 0.017f : 0.02f }, 8);
                Ball(knee, legs, Vector3.zero, new Vector3(0.056f, 0.056f, 0.056f));
                var foot = Pivot(knee, "Foot", new Vector3(0f, -0.14f, 0f));
                Ball(foot, art.Lit(Hex("#e6dfcf")), new Vector3(0f, -0.01f, 0.02f), new Vector3(0.036f, 0.028f, 0.076f));            // tabi sock
                prim.Box(foot, sandal, new Vector3(0f, -0.026f, 0.024f), new Vector3(0.044f, 0.012f, 0.095f));                         // waraji sole
                prim.Box(foot, red, new Vector3(0f, -0.014f, 0.038f), new Vector3(0.05f, 0.006f, 0.012f), default, false);              // strap
            }

            // ---- things they carry
            Transform stickPivot = null;
            if (farmer)
            {
                // hoe over the right shoulder
                var hoe = Pivot(torso, "Hoe", new Vector3(0.09f, 0.16f, -0.02f));
                hoe.localRotation = Quaternion.Euler(-24f, 0f, 8f);
                prim.Cylinder(hoe, wood, new Vector3(0f, 0.08f, 0f), 0.009f, 0.62f);
                prim.Box(hoe, art.Lit(Hex("#8a929c")), new Vector3(0f, 0.39f, 0.026f), new Vector3(0.05f, 0.07f, 0.008f), new Vector3(35f, 0f, 0f));
            }
            if (variant == 1)
            {
                // basket on the left forearm: woven body, green vegetables, handle
                var basket = Pivot(elbowL, "Basket", new Vector3(-0.005f, -0.108f, 0.008f));
                prim.Cylinder(basket, art.Lit(Hex("#b98a4a"), PaintedTexture.Thatch), new Vector3(0f, -0.03f, 0f), 0.05f, 0.06f);
                Ball(basket, art.Lit(Palette.LeafGreen), new Vector3(0f, 0.005f, 0f), new Vector3(0.085f, 0.05f, 0.085f));
                Ball(basket, art.Lit(Hex("#e58a3a")), new Vector3(0.02f, 0.012f, 0.012f), new Vector3(0.04f, 0.04f, 0.04f));
                prim.Cylinder(basket, art.Lit(Hex("#b98a4a")), new Vector3(0f, 0.02f, 0f), 0.006f, 0.1f, new Vector3(0f, 0f, 90f), false);
            }
            if (elder || monk)
            {
                // walking staff, planted at the right hand
                stickPivot = Pivot(body, "Staff", new Vector3(0.115f, 0.24f, 0.07f));
                prim.Cylinder(stickPivot, wood, new Vector3(0f, -0.12f, 0f), 0.0095f, 0.5f);
                if (monk)
                    for (int i = 0; i < 3; i++) prim.Cylinder(stickPivot, art.NoOutline(Palette.Gold), new Vector3(0f, 0.15f + i * 0.02f, 0f), 0.03f - i * 0.006f, 0.008f);
                else
                    Ball(stickPivot, wood, new Vector3(0f, 0.135f, 0f), new Vector3(0.03f, 0.028f, 0.03f));
            }
            if (worker)
            {
                // axe tucked into the sash
                var axe = Pivot(torso, "Axe", new Vector3(-0.07f, 0.06f, 0.02f));
                axe.localRotation = Quaternion.Euler(0f, 0f, 55f);
                prim.Cylinder(axe, wood, new Vector3(0f, -0.06f, 0f), 0.009f, 0.24f);
                prim.Box(axe, art.Lit(Hex("#8a929c")), new Vector3(0.012f, 0.05f, 0f), new Vector3(0.04f, 0.05f, 0.01f));
            }
            if (monk)
            {
                // prayer beads round the neck and a rope sash
                Limb(torso, art.Lit(Hex("#5a3a22")), new[] { new Vector3(-0.03f, 0.2f, 0.045f), new Vector3(0f, 0.15f, 0.058f), new Vector3(0.03f, 0.2f, 0.045f) }, new[] { 0.007f, 0.007f, 0.007f }, 5);
            }

            MergeBones(root.transform);

            float lean = elder ? 6f : 0f;
            float seed = variant * 1.7f + 0.3f;
            float stride = woman ? 0.5f : (elder ? 0.62f : 1f);
            float armAmp = woman ? 0.45f : (elder ? 0.35f : 1f);
            float speedK = elder ? 0.8f : 1f;
            bool foldedHands = variant == 4;
            var breathe = torso;
            var headBase = head;

            return new ActorVisual
            {
                Root = root,
                Animate = (state, time, phase) =>
                {
                    if (state == NpcState.Walking)
                    {
                        float p = phase * 9f * speedK + seed;
                        float sw = Mathf.Sin(p);
                        float legAmp = 30f * stride;
                        // legs: thigh swing, knee bends while the leg swings forward
                        hipL.localRotation = X(sw * legAmp);
                        hipR.localRotation = X(-sw * legAmp);
                        kneeL.localRotation = X(Mathf.Max(0f, -Mathf.Cos(p)) * 34f * stride + 3f);
                        kneeR.localRotation = X(Mathf.Max(0f, Mathf.Cos(p)) * 34f * stride + 3f);
                        // arms swing against the legs, elbows bend on the forward swing
                        float armSw = 26f * armAmp;
                        if (foldedHands)
                        {
                            shoulderL.localRotation = Quaternion.Euler(-12f, 38f, 0f);
                            shoulderR.localRotation = Quaternion.Euler(-12f, -38f, 0f);
                            elbowL.localRotation = X(-78f);
                            elbowR.localRotation = X(-78f);
                        }
                        else
                        {
                            shoulderL.localRotation = X(-sw * armSw);
                            shoulderR.localRotation = X(sw * armSw);
                            elbowL.localRotation = X(-8f - Mathf.Max(0f, sw) * 22f * armAmp);
                            elbowR.localRotation = X(-8f - Mathf.Max(0f, -sw) * 22f * armAmp);
                        }
                        if (stickPivot != null) stickPivot.localRotation = X(Mathf.Cos(p) * 16f);
                        // body: counter-rotating shoulders, two bounces per stride, a little sway; skirt follows the hips
                        body.localPosition = new Vector3(Mathf.Sin(p) * 0.004f, (1f - Mathf.Abs(sw)) * 0.014f * (woman ? 0.7f : 1f), 0f);
                        body.localRotation = Quaternion.Euler(lean + 2f, 0f, sw * 1.6f);
                        torso.localRotation = Quaternion.Euler(0f, -sw * 5f, 0f);
                        hips.localRotation = Quaternion.Euler(0f, sw * 4f, 0f);
                        skirt.localRotation = Quaternion.Euler(Mathf.Cos(p) * 4f, 0f, 0f);
                        headBase.localRotation = Quaternion.Euler(-lean * 0.6f, sw * 3f, -sw * 1.2f);
                        breathe.localScale = Vector3.one;
                    }
                    else
                    {
                        hipL.localRotation = Quaternion.identity;
                        hipR.localRotation = Quaternion.identity;
                        kneeL.localRotation = Quaternion.identity;
                        kneeR.localRotation = Quaternion.identity;
                        float t = time + seed * 3f;
                        float sway = Mathf.Sin(t * 0.7f);
                        if (foldedHands)
                        {
                            shoulderL.localRotation = Quaternion.Euler(-12f, 38f, 0f);
                            shoulderR.localRotation = Quaternion.Euler(-12f, -38f, 0f);
                            elbowL.localRotation = X(-78f);
                            elbowR.localRotation = X(-78f);
                        }
                        else
                        {
                            shoulderL.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2f, 0f, 3f);
                            shoulderR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 0.9f) * 2f, 0f, -3f);
                            elbowL.localRotation = X(-6f);
                            elbowR.localRotation = X(-6f);
                        }
                        if (stickPivot != null) stickPivot.localRotation = Quaternion.identity;
                        body.localPosition = new Vector3(sway * 0.003f, 0f, 0f);
                        body.localRotation = Quaternion.Euler(lean, 0f, sway * 1.2f);
                        torso.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.45f) * 4f, 0f);
                        hips.localRotation = Quaternion.identity;
                        skirt.localRotation = Quaternion.identity;
                        // looks around now and then
                        float look = Mathf.Sin(t * 0.5f) * Mathf.Clamp01(Mathf.Sin(t * 0.23f) * 2.5f + 0.4f);
                        headBase.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 3f - lean * 0.6f, look * 32f, 0f);
                        breathe.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2.1f) * 0.012f, 1f + Mathf.Sin(t * 2.1f) * 0.01f);
                    }
                }
            };
        }

        // ------------------------------------------------------------------ dog

        public ActorVisual Dog()
        {
            var root = new GameObject("Dog");
            var fur = art.Lit(Hex("#A9743F"));
            var furDark = art.Lit(Hex("#7d5228"));
            var light = art.Lit(Hex("#E7D3B0"));
            var dark = art.Lit(Hex("#3B2A1E"));
            var black = art.NoOutline(Hex("#1d1512"));
            var nose = art.NoOutline(Hex("#1d1512"));
            var tongue = art.NoOutline(Hex("#d9727a"));

            var body = Pivot(root.transform, "Body", Vector3.zero);
            // barrel chest tapering to the haunches, lighter belly and chest
            Limb(body, fur, new[] { new Vector3(0f, 0.215f, 0.115f), new Vector3(0f, 0.22f, 0.04f), new Vector3(0f, 0.215f, -0.05f), new Vector3(0f, 0.21f, -0.135f) },
                new[] { 0.068f, 0.075f, 0.068f, 0.062f }, 10);
            Ball(body, light, new Vector3(0f, 0.19f, 0.09f), new Vector3(0.09f, 0.1f, 0.12f));      // chest
            Ball(body, light, new Vector3(0f, 0.165f, 0.0f), new Vector3(0.095f, 0.05f, 0.2f), false); // belly
            Ball(body, furDark, new Vector3(0f, 0.262f, 0.0f), new Vector3(0.1f, 0.05f, 0.24f), false); // saddle
            var neck = Limb(body, fur, new[] { new Vector3(0f, 0.235f, 0.12f), new Vector3(0f, 0.285f, 0.16f), new Vector3(0f, 0.31f, 0.19f) }, new[] { 0.048f, 0.04f, 0.038f }, 8);
            neck.name = "Neck";
            var head = Pivot(body, "Head", new Vector3(0f, 0.315f, 0.205f));
            Ball(head, fur, Vector3.zero, new Vector3(0.105f, 0.095f, 0.115f));                                   // skull
            Ball(head, light, new Vector3(0f, -0.022f, 0.062f), new Vector3(0.062f, 0.048f, 0.085f));              // muzzle
            Ball(head, nose, new Vector3(0f, -0.008f, 0.108f), new Vector3(0.026f, 0.02f, 0.02f), false);          // nose
            Ball(head, tongue, new Vector3(0f, -0.04f, 0.085f), new Vector3(0.022f, 0.008f, 0.03f), false);
            foreach (var s in new[] { -1f, 1f })
            {
                Ball(head, black, new Vector3(s * 0.03f, 0.018f, 0.045f), new Vector3(0.016f, 0.016f, 0.012f), false);   // eyes
                var ear = Ball(head, dark, new Vector3(s * 0.058f, 0.005f, -0.012f), new Vector3(0.028f, 0.075f, 0.052f));
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, s * -18f);
            }
            var tail = Pivot(body, "Tail", new Vector3(0f, 0.245f, -0.135f));
            Limb(tail, fur, new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.05f, -0.035f), new Vector3(0f, 0.11f, -0.045f), new Vector3(0f, 0.16f, -0.02f) }, new[] { 0.022f, 0.02f, 0.016f, 0.011f }, 6);
            Ball(tail, light, new Vector3(0f, 0.165f, -0.018f), new Vector3(0.026f, 0.036f, 0.026f), false);

            var hipPivots = new Transform[4];
            var kneePivots = new Transform[4];
            var offsets = new[] { new Vector3(-0.052f, 0.205f, 0.1f), new Vector3(0.052f, 0.205f, 0.1f), new Vector3(-0.052f, 0.205f, -0.11f), new Vector3(0.052f, 0.205f, -0.11f) };
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                hipPivots[i] = Pivot(body, "Leg" + i, offsets[i]);
                Limb(hipPivots[i], fur, new[] { new Vector3(0f, 0.005f, 0f), new Vector3(0f, -0.085f, front ? 0.004f : -0.01f) }, new[] { front ? 0.03f : 0.036f, 0.022f }, 7);
                kneePivots[i] = Pivot(hipPivots[i], "Knee", new Vector3(0f, -0.085f, front ? 0.004f : -0.01f));
                Limb(kneePivots[i], fur, new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.09f, front ? 0.0f : 0.012f) }, new[] { 0.021f, 0.016f }, 7);
                Ball(kneePivots[i], light, new Vector3(0f, -0.098f, 0.014f), new Vector3(0.04f, 0.028f, 0.056f));   // paw with a light sock
            }

            MergeBones(root.transform);

            return new ActorVisual
            {
                Root = root,
                Animate = (state, time, phase) =>
                {
                    if (state == NpcState.Walking)
                    {
                        float p = phase * 12f;
                        float s = Mathf.Sin(p) * 30f;
                        hipPivots[0].localRotation = X(s);
                        hipPivots[3].localRotation = X(s);
                        hipPivots[1].localRotation = X(-s);
                        hipPivots[2].localRotation = X(-s);
                        for (int i = 0; i < 4; i++)
                        {
                            float sign = (i == 0 || i == 3) ? 1f : -1f;
                            kneePivots[i].localRotation = X(Mathf.Max(0f, -Mathf.Cos(p) * sign) * 40f);
                        }
                        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(p)) * 0.018f, 0f);
                        body.localRotation = Quaternion.Euler(Mathf.Sin(p * 2f) * 2f, 0f, Mathf.Sin(p) * 1.5f);
                        tail.localRotation = Quaternion.Euler(-10f, Mathf.Sin(time * 9f) * 28f, 0f);
                        head.localRotation = Quaternion.Euler(Mathf.Sin(p * 2f) * 4f - 6f, 0f, 0f);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            hipPivots[i].localRotation = Quaternion.identity;
                            kneePivots[i].localRotation = Quaternion.identity;
                        }
                        body.localPosition = new Vector3(0f, 0f, 0f);
                        body.localRotation = Quaternion.Euler(Mathf.Sin(time * 2.2f) * 0.8f, 0f, 0f);
                        tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 5f) * 35f, 0f);
                        head.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.9f) * 6f, Mathf.Sin(time * 0.6f) * 22f, Mathf.Sin(time * 0.45f) * 5f);
                    }
                }
            };
        }

        // ------------------------------------------------------------------ chicken

        public ActorVisual Chicken(bool brown)
        {
            var root = new GameObject("Chicken");
            var feathers = art.Lit(brown ? Hex("#B5773C") : Hex("#F4F1E8"));
            var feathersDark = art.Lit(brown ? Hex("#7c4a22") : Hex("#d9d3c2"));
            var tailColor = art.Lit(brown ? Hex("#2f3a2e") : Hex("#ece6d6"));
            var red = art.Lit(Hex("#C7392E"));
            var orange = art.Lit(Hex("#E8A33A"));
            var black = art.NoOutline(Hex("#1d1512"));
            var eyeRing = art.NoOutline(Hex("#e8b23a"));

            var body = Pivot(root.transform, "Body", Vector3.zero);
            var chest = Ball(body, feathers, new Vector3(0f, 0.15f, 0.015f), new Vector3(0.135f, 0.14f, 0.19f));
            chest.name = "Chest";
            Ball(body, feathersDark, new Vector3(0f, 0.165f, -0.05f), new Vector3(0.12f, 0.11f, 0.12f), false);   // back
            foreach (var s in new[] { -1f, 1f })
            {
                var wing = Ball(body, feathersDark, new Vector3(s * 0.066f, 0.16f, -0.005f), new Vector3(0.03f, 0.09f, 0.14f));
                wing.transform.localRotation = Quaternion.Euler(-6f, 0f, s * 8f);
            }
            // tail: a fan of curved feathers
            for (int i = -2; i <= 2; i++)
            {
                var feather = Ball(body, i % 2 == 0 ? tailColor : feathersDark, new Vector3(i * 0.014f, 0.215f + (2 - Mathf.Abs(i)) * 0.012f, -0.1f), new Vector3(0.022f, 0.09f, 0.03f), false);
                feather.transform.localRotation = Quaternion.Euler(-28f, i * 9f, i * -7f);
            }
            var neck = Limb(body, feathers, new[] { new Vector3(0f, 0.19f, 0.06f), new Vector3(0f, 0.235f, 0.085f), new Vector3(0f, 0.27f, 0.09f) }, new[] { 0.036f, 0.026f, 0.022f }, 7);
            neck.name = "Neck";
            var head = Pivot(body, "Head", new Vector3(0f, 0.28f, 0.095f));
            Ball(head, feathers, new Vector3(0f, 0.008f, 0.006f), new Vector3(0.046f, 0.05f, 0.056f));
            for (int i = 0; i < 3; i++)
                Ball(head, red, new Vector3(0f, 0.04f + (i == 1 ? 0.004f : 0f), 0.02f - i * 0.014f), new Vector3(0.01f, 0.024f - i * 0.003f, 0.02f), false);   // comb
            Ball(head, red, new Vector3(0f, -0.022f, 0.03f), new Vector3(0.014f, 0.026f, 0.012f), false);                                          // wattle
            prim.Cone(head, orange, new Vector3(0f, 0.004f, 0.03f), 0.014f, 0.036f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // beak
            foreach (var s in new[] { -1f, 1f })
            {
                Ball(head, eyeRing, new Vector3(s * 0.021f, 0.014f, 0.016f), new Vector3(0.01f, 0.012f, 0.008f), false);
                Ball(head, black, new Vector3(s * 0.0225f, 0.014f, 0.017f), new Vector3(0.006f, 0.007f, 0.006f), false);
            }

            var hipL = Pivot(body, "LegL", new Vector3(-0.03f, 0.1f, 0.01f));
            var hipR = Pivot(body, "LegR", new Vector3(0.03f, 0.1f, 0.01f));
            foreach (var hip in new[] { hipL, hipR })
            {
                Limb(hip, orange, new[] { new Vector3(0f, 0.01f, 0f), new Vector3(0f, -0.098f, 0f) }, new[] { 0.009f, 0.007f }, 5);
                for (int t = -1; t <= 1; t++)
                {
                    var toe = prim.Cone(hip, orange, new Vector3(0f, -0.098f, 0.004f), 0.006f, 0.04f);
                    toe.transform.localRotation = Quaternion.Euler(90f, t * 26f, 0f);
                }
            }

            MergeBones(root.transform);

            float seed = brown ? 2.1f : 0.4f;
            return new ActorVisual
            {
                Root = root,
                Animate = (state, time, phase) =>
                {
                    if (state == NpcState.Walking)
                    {
                        float p = phase * 14f + seed;
                        float s = Mathf.Sin(p) * 38f;
                        hipL.localRotation = X(s);
                        hipR.localRotation = X(-s);
                        head.localPosition = new Vector3(0f, 0.28f, 0.095f + Mathf.Sin(p) * 0.012f);
                        head.localRotation = X(Mathf.Sin(p) * 4f);
                        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(p)) * 0.012f, 0f);
                        body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(p) * 4f);
                    }
                    else
                    {
                        hipL.localRotation = Quaternion.identity;
                        hipR.localRotation = Quaternion.identity;
                        body.localPosition = Vector3.zero;
                        body.localRotation = Quaternion.identity;
                        head.localPosition = new Vector3(0f, 0.28f, 0.095f);
                        // occasional peck, otherwise a jerky look around like a real hen
                        float peck = Mathf.Max(0f, Mathf.Sin(time * 3.2f + seed * 3f) - 0.55f) * 2.2f;
                        float look = Mathf.Round(Mathf.Sin(time * 0.7f + seed) * 2f) * 14f;
                        head.localRotation = Quaternion.Euler(peck * 60f, look, 0f);
                        body.localRotation = Quaternion.Euler(peck * 14f, 0f, 0f);
                    }
                }
            };
        }
    }
}
