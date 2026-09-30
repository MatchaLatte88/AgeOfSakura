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

    /// <summary>A placeholder character: its root plus a function that poses it for the current state.</summary>
    public sealed class ActorVisual
    {
        public GameObject Root;
        /// <summary>(state, seconds, walkPhase) - called every frame by the agent.</summary>
        public Action<NpcState, float, float> Animate;
    }

    /// <summary>Builds simple villagers and animals from primitives. Replaceable later; agents only need an <see cref="ActorVisual"/>.</summary>
    public sealed class ActorFactory
    {
        private readonly GameArt art;
        private readonly Prim prim;

        public ActorFactory(GameArt art, Prim prim)
        {
            this.art = art;
            this.prim = prim;
        }

        private Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        // ------------------------------------------------------------------ villagers (~0.6 units tall, forward = +Z)

        public ActorVisual Villager(int variant)
        {
            var root = new GameObject("Villager");
            var kimonoColors = new[] { Palette.KimonoIndigo, Palette.KimonoRust, Palette.KimonoMoss };
            var kimono = art.Lit(kimonoColors[variant % kimonoColors.Length]);
            var sash = art.Lit(Palette.Sash);
            var skin = art.Lit(Palette.Skin);
            var hair = art.Lit(Palette.Hair);
            var dark = art.Lit(Palette.Hex("#2E2A26"));

            var body = Pivot(root.transform, "Body", Vector3.zero);
            prim.Cylinder(body, kimono, new Vector3(0f, 0.31f, 0f), 0.105f, 0.3f);
            prim.Cylinder(body, sash, new Vector3(0f, 0.28f, 0f), 0.112f, 0.05f);
            prim.Sphere(body, skin, new Vector3(0f, 0.54f, 0f), 0.085f);
            prim.Sphere(body, hair, new Vector3(0f, 0.575f, -0.012f), new Vector3(0.18f, 0.11f, 0.18f));
            if (variant == 0) prim.Cone(body, art.Lit(Palette.Straw), new Vector3(0f, 0.585f, 0f), 0.17f, 0.09f);
            if (variant == 1) prim.Sphere(body, hair, new Vector3(0f, 0.64f, -0.03f), 0.035f); // top-knot

            var armL = Pivot(body, "ArmL", new Vector3(-0.125f, 0.43f, 0f));
            var armR = Pivot(body, "ArmR", new Vector3(0.125f, 0.43f, 0f));
            prim.Box(armL, kimono, new Vector3(0f, -0.09f, 0f), new Vector3(0.045f, 0.2f, 0.045f));
            prim.Box(armR, kimono, new Vector3(0f, -0.09f, 0f), new Vector3(0.045f, 0.2f, 0.045f));

            var legL = Pivot(root.transform, "LegL", new Vector3(-0.05f, 0.17f, 0f));
            var legR = Pivot(root.transform, "LegR", new Vector3(0.05f, 0.17f, 0f));
            prim.Box(legL, dark, new Vector3(0f, -0.08f, 0.01f), new Vector3(0.06f, 0.17f, 0.08f));
            prim.Box(legR, dark, new Vector3(0f, -0.08f, 0.01f), new Vector3(0.06f, 0.17f, 0.08f));

            float seed = variant * 1.7f;
            return new ActorVisual
            {
                Root = root,
                Animate = (state, time, phase) =>
                {
                    if (state == NpcState.Walking)
                    {
                        float swing = Mathf.Sin(phase * 9f + seed) * 34f;
                        legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
                        legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                        armL.localRotation = Quaternion.Euler(-swing * 0.7f, 0f, 0f);
                        armR.localRotation = Quaternion.Euler(swing * 0.7f, 0f, 0f);
                        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 9f + seed)) * 0.018f, 0f);
                        body.localScale = Vector3.one;
                    }
                    else
                    {
                        legL.localRotation = Quaternion.identity;
                        legR.localRotation = Quaternion.identity;
                        armL.localRotation = Quaternion.identity;
                        armR.localRotation = Quaternion.identity;
                        body.localPosition = Vector3.zero;
                        body.localScale = new Vector3(1f, 1f + Mathf.Sin(time * 2f + seed) * 0.014f, 1f);
                    }
                }
            };
        }

        // ------------------------------------------------------------------ dog

        public ActorVisual Dog()
        {
            var root = new GameObject("Dog");
            var fur = art.Lit(Palette.Hex("#A9743F"));
            var light = art.Lit(Palette.Hex("#E7D3B0"));
            var dark = art.Lit(Palette.Hex("#3B2A1E"));

            var body = Pivot(root.transform, "Body", Vector3.zero);
            prim.Box(body, fur, new Vector3(0f, 0.21f, 0f), new Vector3(0.14f, 0.15f, 0.32f));
            prim.Box(body, light, new Vector3(0f, 0.15f, 0.05f), new Vector3(0.12f, 0.06f, 0.18f));
            var head = Pivot(body, "Head", new Vector3(0f, 0.3f, 0.19f));
            prim.Box(head, fur, Vector3.zero, new Vector3(0.12f, 0.11f, 0.12f));
            prim.Box(head, light, new Vector3(0f, -0.02f, 0.085f), new Vector3(0.07f, 0.055f, 0.08f));
            prim.Box(head, dark, new Vector3(0f, 0f, 0.13f), new Vector3(0.03f, 0.03f, 0.02f));
            prim.Box(head, dark, new Vector3(-0.07f, 0.02f, -0.01f), new Vector3(0.03f, 0.09f, 0.06f));
            prim.Box(head, dark, new Vector3(0.07f, 0.02f, -0.01f), new Vector3(0.03f, 0.09f, 0.06f));
            var tail = Pivot(body, "Tail", new Vector3(0f, 0.27f, -0.16f));
            prim.Box(tail, fur, new Vector3(0f, 0.05f, -0.03f), new Vector3(0.04f, 0.14f, 0.04f), new Vector3(-25f, 0f, 0f));

            var legs = new Transform[4];
            var offsets = new[] { new Vector3(-0.05f, 0.14f, 0.11f), new Vector3(0.05f, 0.14f, 0.11f), new Vector3(-0.05f, 0.14f, -0.11f), new Vector3(0.05f, 0.14f, -0.11f) };
            for (int i = 0; i < 4; i++)
            {
                legs[i] = Pivot(root.transform, "Leg" + i, offsets[i]);
                prim.Box(legs[i], fur, new Vector3(0f, -0.07f, 0f), new Vector3(0.045f, 0.15f, 0.045f));
            }

            return new ActorVisual
            {
                Root = root,
                Animate = (state, time, phase) =>
                {
                    if (state == NpcState.Walking)
                    {
                        float s = Mathf.Sin(phase * 12f) * 32f;
                        legs[0].localRotation = Quaternion.Euler(s, 0f, 0f);
                        legs[3].localRotation = Quaternion.Euler(s, 0f, 0f);
                        legs[1].localRotation = Quaternion.Euler(-s, 0f, 0f);
                        legs[2].localRotation = Quaternion.Euler(-s, 0f, 0f);
                        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 12f)) * 0.02f, 0f);
                        tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 9f) * 25f, 0f);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++) legs[i].localRotation = Quaternion.identity;
                        body.localPosition = Vector3.zero;
                        tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 5f) * 35f, 0f);
                        head.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.9f) * 6f, Mathf.Sin(time * 0.6f) * 18f, 0f);
                    }
                }
            };
        }

        // ------------------------------------------------------------------ chicken

        public ActorVisual Chicken(bool brown)
        {
            var root = new GameObject("Chicken");
            var feathers = art.Lit(brown ? Palette.Hex("#B5773C") : Palette.Hex("#F4F1E8"));
            var red = art.Lit(Palette.Hex("#C7392E"));
            var orange = art.Lit(Palette.Hex("#E8A33A"));

            var body = Pivot(root.transform, "Body", Vector3.zero);
            prim.Sphere(body, feathers, new Vector3(0f, 0.15f, 0f), new Vector3(0.14f, 0.13f, 0.19f));
            prim.Box(body, feathers, new Vector3(0f, 0.2f, -0.1f), new Vector3(0.03f, 0.09f, 0.06f), new Vector3(30f, 0f, 0f));
            var head = Pivot(body, "Head", new Vector3(0f, 0.22f, 0.08f));
            prim.Sphere(head, feathers, new Vector3(0f, 0.03f, 0.01f), 0.036f);
            prim.Box(head, red, new Vector3(0f, 0.075f, 0.01f), new Vector3(0.012f, 0.03f, 0.03f));
            prim.Cone(head, orange, new Vector3(0f, 0.03f, 0.04f), 0.014f, 0.045f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var legL = Pivot(root.transform, "LegL", new Vector3(-0.03f, 0.09f, 0f));
            var legR = Pivot(root.transform, "LegR", new Vector3(0.03f, 0.09f, 0f));
            prim.Cylinder(legL, orange, new Vector3(0f, -0.04f, 0f), 0.006f, 0.09f);
            prim.Cylinder(legR, orange, new Vector3(0f, -0.04f, 0f), 0.006f, 0.09f);

            float seed = brown ? 2.1f : 0.4f;
            return new ActorVisual
            {
                Root = root,
                Animate = (state, time, phase) =>
                {
                    if (state == NpcState.Walking)
                    {
                        float s = Mathf.Sin(phase * 14f + seed) * 35f;
                        legL.localRotation = Quaternion.Euler(s, 0f, 0f);
                        legR.localRotation = Quaternion.Euler(-s, 0f, 0f);
                        head.localPosition = new Vector3(0f, 0.22f, 0.08f + Mathf.Sin(phase * 14f + seed) * 0.012f);
                        head.localRotation = Quaternion.identity;
                        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 14f + seed)) * 0.01f, 0f);
                    }
                    else
                    {
                        legL.localRotation = Quaternion.identity;
                        legR.localRotation = Quaternion.identity;
                        body.localPosition = Vector3.zero;
                        head.localPosition = new Vector3(0f, 0.22f, 0.08f);
                        // occasional peck
                        float peck = Mathf.Max(0f, Mathf.Sin(time * 3.2f + seed * 3f) - 0.55f) * 2.2f;
                        head.localRotation = Quaternion.Euler(peck * 55f, 0f, 0f);
                    }
                }
            };
        }
    }
}
