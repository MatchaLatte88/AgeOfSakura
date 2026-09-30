using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>Small, cheap particle effects created in code (few particles, one shared soft sprite).</summary>
    public sealed class VfxFactory
    {
        private readonly GameArt art;
        private readonly Material smokeMaterial;
        private readonly Material petalMaterial;
        private readonly Material dustMaterial;

        public VfxFactory(GameArt art)
        {
            this.art = art;
            var soft = art.Icons.Get("soft").texture;
            smokeMaterial = art.Particle(soft, Color.white);
            petalMaterial = art.Particle(soft, Color.white);
            dustMaterial = art.Particle(soft, Color.white);
        }

        public ParticleSystem CreateSmoke(Transform parent, Vector3 localPosition)
        {
            var ps = Create("Smoke", parent, localPosition, smokeMaterial, out var main);
            main.loop = true;
            main.startLifetime = 2.8f;
            main.startSpeed = 0.28f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
            main.startColor = new Color(0.92f, 0.92f, 0.9f, 0.5f);
            main.maxParticles = 20;
            main.gravityModifier = -0.02f;
            var emission = ps.emission;
            emission.rateOverTime = 3.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.025f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeAndGrow(ps, 0.5f, 0.6f, 1.9f);
            ps.gameObject.SetActive(true);
            return ps;
        }

        public ParticleSystem CreatePetals(Transform parent, Vector3 localPosition)
        {
            var ps = Create("Petals", parent, localPosition, petalMaterial, out var main);
            main.loop = true;
            main.startLifetime = 4.5f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = new Color(1f, 0.78f, 0.86f, 0.9f);
            main.maxParticles = 24;
            main.gravityModifier = 0.045f;
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.1f, 0.15f, 1.1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.5f;
            FadeAndGrow(ps, 0.9f, 1f, 1f);
            ps.gameObject.SetActive(true);
            return ps;
        }

        /// <summary>One-shot dust puff for building placement; destroys itself when finished.</summary>
        public void PlayDust(Vector3 worldPosition, float radius)
        {
            var ps = Create("Dust", null, worldPosition, dustMaterial, out var main);
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = 0.7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startColor = new Color(0.85f, 0.78f, 0.62f, 0.55f);
            main.maxParticles = 20;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeAndGrow(ps, 0.55f, 0.7f, 1.6f);
            ps.gameObject.SetActive(true);
        }

        private ParticleSystem Create(string name, Transform parent, Vector3 position, Material material, out ParticleSystem.MainModule main)
        {
            var go = new GameObject(name);
            go.SetActive(false); // configure before it starts playing
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
            }
            else
            {
                go.transform.position = position;
            }
            var ps = go.AddComponent<ParticleSystem>();
            main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void FadeAndGrow(ParticleSystem ps, float peakAlpha, float startScale, float endScale)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, startScale), new Keyframe(1f, endScale)));
        }
    }
}
