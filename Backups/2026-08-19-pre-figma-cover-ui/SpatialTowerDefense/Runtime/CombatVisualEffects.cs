using UnityEngine;

namespace PicoTowerDefense
{
    public static class CombatVisualEffects
    {
        private static Material s_lineMaterial;
        private static Material s_particleMaterial;

        public static void AddEnemySkillIndicator(GameObject enemyRoot, EnemyKind kind, float radius)
        {
            if (enemyRoot == null)
            {
                return;
            }

            EnemySkillVisual visual = enemyRoot.AddComponent<EnemySkillVisual>();
            visual.Initialize(kind, Mathf.Max(0.025f, radius));
        }

        public static void AddProjectileTrail(GameObject projectile, TowerKind kind, Color color)
        {
            if (projectile == null)
            {
                return;
            }

            var trail = projectile.AddComponent<TrailRenderer>();
            trail.sharedMaterial = LineMaterial;
            trail.time = kind == TowerKind.Cannon ? 0.16f : 0.10f;
            trail.minVertexDistance = 0.011f;
            trail.startWidth = kind == TowerKind.Cannon ? 0.016f : 0.010f;
            trail.endWidth = 0f;
            Color softTrailColor = Color.Lerp(color, Color.white, 0.18f);
            softTrailColor.a = 0.68f;
            trail.startColor = softTrailColor;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        public static void SpawnTowerImpact(TowerKind kind, Vector3 worldPosition, Transform parent)
        {
            Color color = kind switch
            {
                TowerKind.Arrow => new Color(0.64f, 0.57f, 0.31f),
                TowerKind.Cannon => new Color(0.72f, 0.43f, 0.18f),
                TowerKind.Frost => new Color(0.30f, 0.63f, 0.56f),
                _ => Color.white
            };
            float radius = kind == TowerKind.Cannon ? 0.16f : 0.12f;
            SpawnImpact($"{kind} Purification Impact", worldPosition, parent, color, radius, kind != TowerKind.Arrow);
        }

        public static void SpawnKeeperImpact(Vector3 worldPosition, Transform parent)
        {
            SpawnImpact(
                "Keeper Purification Impact",
                worldPosition,
                parent,
                new Color(0.78f, 0.59f, 0.26f),
                0.14f,
                true);
        }

        private static void SpawnImpact(
            string name,
            Vector3 worldPosition,
            Transform parent,
            Color color,
            float radius,
            bool crossedRing)
        {
            var root = new GameObject(name);
            root.layer = 2;
            root.transform.SetParent(parent, true);
            root.transform.position = worldPosition;
            TransientPurificationRing ring = root.AddComponent<TransientPurificationRing>();
            ring.Initialize(color, radius, crossedRing, LineMaterial);
        }

        internal static Material LineMaterial
        {
            get
            {
                if (s_lineMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                    {
                        shader = Shader.Find("Unlit/Color");
                    }
                    s_lineMaterial = new Material(shader)
                    {
                        name = "Purification Ring Shared Material"
                    };
                }
                return s_lineMaterial;
            }
        }

        internal static Material ParticleMaterial
        {
            get
            {
                if (s_particleMaterial == null)
                {
                    Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                    s_particleMaterial = new Material(shader)
                    {
                        name = "Enemy Skill Mist Shared Material"
                    };
                    if (s_particleMaterial.HasProperty("_Color"))
                    {
                        s_particleMaterial.SetColor("_Color", Color.white);
                    }
                    if (s_particleMaterial.HasProperty("_BaseColor"))
                    {
                        s_particleMaterial.SetColor("_BaseColor", Color.white);
                    }
                }
                return s_particleMaterial;
            }
        }
    }

    /// <summary>
    /// Small, restrained runtime cues make each imported enemy's skill readable
    /// without changing the supplied mesh or adding anything to the authored
    /// island layout.
    /// </summary>
    public sealed class EnemySkillVisual : MonoBehaviour
    {
        private const int RingSegments = 28;

        private EnemyKind _kind;
        private Transform _effectRoot;
        private Transform _orbitRoot;
        private LineRenderer _ring;
        private LineRenderer _secondaryRing;
        private ParticleSystem _mist;
        private Color _accent;
        private float _radius;
        private float _elapsed;
        private float _phase;
        private float _baseAlpha;

        public void Initialize(EnemyKind kind, float radius)
        {
            _kind = kind;
            _radius = radius;
            _phase = ((int)kind + 1) * 1.71f;
            _effectRoot = new GameObject("Enemy Skill Cue").transform;
            _effectRoot.gameObject.layer = 2;
            _effectRoot.SetParent(transform, false);

            switch (kind)
            {
                case EnemyKind.Grunt:
                    _accent = new Color(0.56f, 0.64f, 0.72f);
                    _baseAlpha = 0.28f;
                    _ring = CreateRing("Restless Dust Pulse", _radius * 0.78f, _accent, 0.0026f);
                    _mist = CreateMist("Restless Dust", new Color(0.50f, 0.56f, 0.62f, 0.40f), 4f, 0.26f);
                    break;
                case EnemyKind.Runner:
                    _accent = new Color(0.98f, 0.61f, 0.26f);
                    _baseAlpha = 0.36f;
                    CreateRunnerTrail();
                    _ring = CreateRing("Grasping Burden Speed Mark", _radius * 0.72f, _accent, 0.0028f);
                    break;
                case EnemyKind.Tank:
                    _accent = new Color(0.67f, 0.45f, 0.84f);
                    _baseAlpha = 0.40f;
                    _ring = CreateRing("Ignorance Beast Aura", _radius * 1.10f, _accent, 0.0042f);
                    _secondaryRing = CreateRing("Ignorance Beast Aura Inner", _radius * 0.82f, Color.Lerp(_accent, Color.white, 0.22f), 0.0022f, 0.012f);
                    break;
                case EnemyKind.Shield:
                    _accent = new Color(0.24f, 0.90f, 0.88f);
                    _baseAlpha = 0.46f;
                    _ring = CreateRing("Doubt Carapace Shield Pulse", _radius * 1.03f, _accent, 0.0038f);
                    break;
                case EnemyKind.Splitter:
                    _accent = new Color(0.96f, 0.30f, 0.18f);
                    _baseAlpha = 0.42f;
                    _ring = CreateRing("Anger Flame Split Pulse", _radius * 0.96f, _accent, 0.0038f);
                    CreateOrbitEmbers();
                    break;
                case EnemyKind.Support:
                    _accent = new Color(0.48f, 0.92f, 0.70f);
                    _baseAlpha = 0.42f;
                    _ring = CreateRing("Delusion Fog Concealment", _radius * 1.13f, _accent, 0.0035f);
                    _mist = CreateMist("Delusion Fog Mist", new Color(0.58f, 0.90f, 0.73f, 0.52f), 7f, 0.62f);
                    break;
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_elapsed * (_kind == EnemyKind.Tank ? 2.1f : 3.2f) + _phase);
            float scalePulse = 1f + Mathf.Lerp(0f, 0.10f, pulse);

            if (_ring != null)
            {
                SetRingColor(_ring, _accent, _baseAlpha * Mathf.Lerp(0.62f, 1f, pulse));
                _ring.transform.localScale = Vector3.one * scalePulse;
            }
            if (_secondaryRing != null)
            {
                Color secondary = Color.Lerp(_accent, Color.white, 0.22f);
                SetRingColor(_secondaryRing, secondary, _baseAlpha * 0.62f * Mathf.Lerp(0.58f, 1f, 1f - pulse));
                _secondaryRing.transform.localScale = Vector3.one * (1f + (1f - pulse) * 0.08f);
            }
            if (_orbitRoot != null)
            {
                _orbitRoot.Rotate(Vector3.up, (_kind == EnemyKind.Splitter ? 82f : 34f) * Time.deltaTime, Space.Self);
            }
            if (_kind == EnemyKind.Support && _effectRoot != null)
            {
                _effectRoot.localPosition = Vector3.up * (Mathf.Sin(_elapsed * 1.35f + _phase) * _radius * 0.035f);
            }
        }

        private void CreateRunnerTrail()
        {
            var trailObject = new GameObject("Grasping Burden Speed Trail");
            trailObject.layer = 2;
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = Vector3.up * _radius * 0.52f;
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = CombatVisualEffects.LineMaterial;
            trail.time = 0.26f;
            trail.minVertexDistance = _radius * 0.045f;
            trail.startWidth = _radius * 0.14f;
            trail.endWidth = 0f;
            trail.startColor = new Color(_accent.r, _accent.g, _accent.b, 0.46f);
            trail.endColor = new Color(_accent.r, _accent.g, _accent.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private void CreateOrbitEmbers()
        {
            _orbitRoot = new GameObject("Anger Flame Embers").transform;
            _orbitRoot.gameObject.layer = 2;
            _orbitRoot.SetParent(_effectRoot, false);
            Material material = ProceduralFactory.CreateUnlitMaterial(new Color(_accent.r, _accent.g, _accent.b, 0.82f));
            for (int index = 0; index < 2; index++)
            {
                float angle = index * Mathf.PI;
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Sphere,
                    $"Anger Ember {index + 1}",
                    _orbitRoot,
                    new Vector3(Mathf.Cos(angle) * _radius * 0.98f, _radius * 0.54f, Mathf.Sin(angle) * _radius * 0.98f),
                    Vector3.one * (_radius * 0.095f),
                    material);
            }
        }

        private ParticleSystem CreateMist(string name, Color color, float emissionRate, float sizeMultiplier)
        {
            var mistObject = new GameObject(name);
            mistObject.layer = 2;
            mistObject.transform.SetParent(_effectRoot, false);
            mistObject.transform.localPosition = Vector3.up * _radius * 0.62f;
            ParticleSystem system = mistObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.duration = 1.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 32;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_radius * 0.06f, _radius * 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(_radius * 0.18f * sizeMultiplier, _radius * 0.36f * sizeMultiplier);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(color.r, color.g, color.b, color.a * 0.55f));
            main.gravityModifier = -0.04f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _radius * 0.34f;
            ParticleSystemRenderer renderer = mistObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CombatVisualEffects.ParticleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            return system;
        }

        private LineRenderer CreateRing(string name, float radius, Color color, float width, float height = 0.004f)
        {
            var ringObject = new GameObject(name);
            ringObject.layer = 2;
            ringObject.transform.SetParent(_effectRoot, false);
            ringObject.transform.localPosition = Vector3.up * height;
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.sharedMaterial = CombatVisualEffects.LineMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = RingSegments;
            line.startWidth = width;
            line.endWidth = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index / (float)RingSegments * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            SetRingColor(line, color, _baseAlpha);
            return line;
        }

        private static void SetRingColor(LineRenderer line, Color color, float alpha)
        {
            Color start = new(color.r, color.g, color.b, alpha);
            line.startColor = start;
            line.endColor = new Color(start.r, start.g, start.b, alpha * 0.30f);
        }
    }

    public sealed class TransientPurificationRing : MonoBehaviour
    {
        private const int SegmentCount = 32;
        private readonly LineRenderer[] _rings = new LineRenderer[2];
        private Color _color;
        private float _maximumRadius;
        private float _elapsed;
        private float _duration;

        public void Initialize(Color color, float maximumRadius, bool crossedRing, Material material)
        {
            _color = color;
            _maximumRadius = maximumRadius;
            _duration = 0.30f;
            _rings[0] = CreateRing("Ground Wave", Quaternion.identity, material);
            if (crossedRing)
            {
                _rings[1] = CreateRing("Rising Wave", Quaternion.Euler(90f, 0f, 0f), material);
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.10f, _maximumRadius, eased);
            transform.localRotation = Quaternion.Euler(0f, t * 65f, 0f);

            Color fade = new(_color.r, _color.g, _color.b, (1f - t) * 0.48f);
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] == null)
                {
                    continue;
                }
                _rings[i].startColor = fade;
                _rings[i].endColor = new Color(fade.r, fade.g, fade.b, fade.a * 0.30f);
                float width = Mathf.Lerp(0.026f, 0.006f, t);
                _rings[i].startWidth = width;
                _rings[i].endWidth = width;
            }

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private LineRenderer CreateRing(string name, Quaternion localRotation, Material material)
        {
            var ringObject = new GameObject(name);
            ringObject.layer = 2;
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localRotation = localRotation;
            var line = ringObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = SegmentCount;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = i / (float)SegmentCount * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
            return line;
        }
    }
}
