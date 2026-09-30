using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// One central Update for all cheap ambient motion (sway, scrolling water, butterflies) so the scene
    /// does not carry hundreds of tiny per-object Update loops.
    /// </summary>
    public sealed class WorldAnimator : MonoBehaviour
    {
        private struct Sway
        {
            public Transform Target;
            public Quaternion BaseRotation;
            public float Amplitude;
            public float Speed;
            public float Phase;
        }

        private struct Scroller
        {
            public Material Material;
            public Vector2 Velocity;
        }

        private sealed class Butterfly
        {
            public Transform Root;
            public Transform WingA;
            public Transform WingB;
            public Vector3 Center;
            public float RadiusX;
            public float RadiusZ;
            public float Speed;
            public float Phase;
            public float Height;
        }

        private readonly List<Sway> sways = new List<Sway>(160);
        private readonly List<Scroller> scrollers = new List<Scroller>(4);
        private readonly List<Butterfly> butterflies = new List<Butterfly>(6);

        public void AddSway(Transform target, float amplitudeDegrees, float speed, float phase)
        {
            sways.Add(new Sway { Target = target, BaseRotation = target.localRotation, Amplitude = amplitudeDegrees, Speed = speed, Phase = phase });
        }

        public void AddScroller(Material material, Vector2 velocityPerSecond)
        {
            scrollers.Add(new Scroller { Material = material, Velocity = velocityPerSecond });
        }

        public void AddButterfly(Transform root, Transform wingA, Transform wingB, Vector3 center, float radiusX, float radiusZ, float speed, float phase, float height)
        {
            butterflies.Add(new Butterfly { Root = root, WingA = wingA, WingB = wingB, Center = center, RadiusX = radiusX, RadiusZ = radiusZ, Speed = speed, Phase = phase, Height = height });
        }

        private void Update()
        {
            float t = Time.time;

            for (int i = 0; i < sways.Count; i++)
            {
                var s = sways[i];
                if (s.Target == null) continue;
                float a = Mathf.Sin(t * s.Speed + s.Phase) * s.Amplitude;
                float b = Mathf.Cos(t * s.Speed * 0.83f + s.Phase) * s.Amplitude * 0.7f;
                s.Target.localRotation = s.BaseRotation * Quaternion.Euler(a, 0f, b);
            }

            for (int i = 0; i < scrollers.Count; i++)
            {
                var sc = scrollers[i];
                var offset = new Vector2(Mathf.Repeat(sc.Velocity.x * t, 1f), Mathf.Repeat(sc.Velocity.y * t, 1f));
                if (sc.Material.HasProperty("_BaseMap")) sc.Material.SetTextureOffset("_BaseMap", offset);
                if (sc.Material.HasProperty("_MainTex")) sc.Material.SetTextureOffset("_MainTex", offset);
            }

            for (int i = 0; i < butterflies.Count; i++)
            {
                var b = butterflies[i];
                float a = t * b.Speed + b.Phase;
                var pos = b.Center + new Vector3(Mathf.Sin(a) * b.RadiusX, b.Height + Mathf.Sin(a * 2.3f) * 0.12f, Mathf.Sin(a * 0.7f + 1f) * b.RadiusZ);
                var vel = pos - b.Root.position;
                b.Root.position = pos;
                if (vel.sqrMagnitude > 1e-6f) b.Root.rotation = Quaternion.LookRotation(new Vector3(vel.x, 0f, vel.z));
                float flap = Mathf.Sin(t * 22f + b.Phase) * 55f;
                b.WingA.localRotation = Quaternion.Euler(0f, 0f, flap);
                b.WingB.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }
    }
}
