using System.Collections.Generic;
using UnityEngine;

namespace PicoTowerDefense
{
    public sealed class EnemyAgent
    {
        private static int s_motionSeed;

        private readonly IReadOnlyList<Vector3> _path;
        private readonly Transform _healthFill;
        private readonly GameObject _shieldShell;
        private readonly Transform _motionVisual;
        private readonly Vector3 _motionVisualBasePosition;
        private readonly Quaternion _motionVisualBaseRotation;
        private readonly Vector3 _motionVisualBaseScale;
        private readonly MotionProfile _motionProfile;
        private readonly float _motionPhase;
        private float _slowTimer;
        private float _slowFactor = 1f;
        private float _hasteTimer;
        private float _hasteFactor = 1f;
        private float _motionTime;
        private int _segment;

        public EnemyKind Kind { get; }
        public EnemyDefinition Definition { get; }
        public GameObject Root { get; }
        public float HitPoints { get; private set; }
        public float ShieldPoints { get; private set; }
        public bool IsDead { get; private set; }
        public bool ReachedEnd { get; private set; }
        public int PathSegment => _segment;
        public Vector3 LocalPosition => Root.transform.localPosition;
        public Vector3 Position => Root.transform.position;
        public bool HasReachedWeaponGate => Progress >= GameDefinitions.WeaponActivationProgress;

        public float Progress
        {
            get
            {
                if (_segment >= _path.Count - 1)
                {
                    return _path.Count;
                }

                float length = Vector3.Distance(_path[_segment], _path[_segment + 1]);
                float remaining = Vector3.Distance(Root.transform.localPosition, _path[_segment + 1]);
                return _segment + (length <= 0.0001f ? 0f : 1f - remaining / length);
            }
        }

        public EnemyAgent(
            EnemyKind kind,
            IReadOnlyList<Vector3> localPath,
            Transform parent,
            int startSegment = 0,
            Vector3? startLocalPosition = null)
        {
            Kind = kind;
            Definition = GameDefinitions.Enemy(kind);
            _path = localPath;
            _segment = Mathf.Clamp(startSegment, 0, localPath.Count - 1);
            HitPoints = Definition.HitPoints;
            ShieldPoints = Definition.ShieldPoints;
            Root = ProceduralFactory.BuildEnemyVisual(kind, parent);
            Root.transform.localPosition = startLocalPosition ?? _path[_segment];
            _motionVisual = CreateMotionVisualPivot(Root.transform);
            _motionVisualBasePosition = _motionVisual.localPosition;
            _motionVisualBaseRotation = _motionVisual.localRotation;
            _motionVisualBaseScale = _motionVisual.localScale;
            _motionProfile = MotionProfileFor(kind);
            _motionPhase = Mathf.Repeat(
                ++s_motionSeed * 1.173f + ((int)kind + 1) * 1.618f,
                Mathf.PI * 2f);

            Transform shell = FindDescendant(_motionVisual, "Broken Mirror Barrier");
            _shieldShell = shell != null ? shell.gameObject : null;

            UpdateHeading();

            Material barBack = ProceduralFactory.CreateMaterial(new Color(0.06f, 0.07f, 0.10f));
            Material barFill = ProceduralFactory.CreateMaterial(new Color(0.95f, 0.70f, 0.24f));
            float width = Definition.Radius * 2.4f;
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                "Health Back",
                Root.transform,
                new Vector3(0f, Definition.Radius * 2.65f, 0f),
                new Vector3(width, Definition.Radius * 0.24f, 0.008f),
                barBack);
            _healthFill = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                "Health Fill",
                Root.transform,
                new Vector3(0f, Definition.Radius * 2.65f, -0.006f),
                new Vector3(width * 0.95f, Definition.Radius * 0.13f, 0.009f),
                barFill).transform;
        }

        public void Update(float deltaTime)
        {
            if (IsDead || ReachedEnd)
            {
                return;
            }

            if (_slowTimer > 0f)
            {
                _slowTimer -= deltaTime;
                if (_slowTimer <= 0f)
                {
                    _slowFactor = 1f;
                }
            }

            if (_hasteTimer > 0f)
            {
                _hasteTimer -= deltaTime;
                if (_hasteTimer <= 0f)
                {
                    _hasteFactor = 1f;
                }
            }

            float speedFactor = _slowFactor * _hasteFactor;
            float remainingMove = Definition.Speed * speedFactor * deltaTime;
            while (remainingMove > 0f && _segment < _path.Count - 1)
            {
                Vector3 current = Root.transform.localPosition;
                Vector3 target = _path[_segment + 1];
                Vector3 toTarget = target - current;
                float distance = toTarget.magnitude;
                if (distance <= remainingMove)
                {
                    Root.transform.localPosition = target;
                    remainingMove -= distance;
                    _segment++;
                }
                else
                {
                    Vector3 direction = toTarget / distance;
                    Root.transform.localPosition = current + direction * remainingMove;
                    remainingMove = 0f;
                }
            }

            // Movement still follows the exact path; only the visible mesh receives the gait motion.
            UpdateHeading();
            UpdateVisualMotion(deltaTime, speedFactor);

            if (_segment >= _path.Count - 1)
            {
                ReachedEnd = true;
            }
        }

        public void HealNearby(float deltaTime, IReadOnlyList<EnemyAgent> enemies)
        {
            if (IsDead || ReachedEnd)
            {
                return;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent ally = enemies[i];
                if (ally != this && !ally.IsDead && !ally.ReachedEnd &&
                    (ally.LocalPosition - LocalPosition).sqrMagnitude <=
                    (0.30f * GameDefinitions.SpatialScale) * (0.30f * GameDefinitions.SpatialScale))
                {
                    if (Definition.HealPerSecond > 0f)
                    {
                        ally.Heal(Definition.HealPerSecond * deltaTime);
                    }
                    if (Kind == EnemyKind.Splitter)
                    {
                        ally.ApplyHaste(1.22f, 0.20f);
                    }
                }
            }
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead || ReachedEnd)
            {
                return;
            }

            if (ShieldPoints > 0f)
            {
                float absorbed = Mathf.Min(ShieldPoints, amount);
                ShieldPoints -= absorbed;
                amount -= absorbed;
                if (ShieldPoints <= 0f && _shieldShell != null)
                {
                    _shieldShell.SetActive(false);
                }
            }

            if (amount > 0f)
            {
                HitPoints -= amount * (1f - Mathf.Clamp01(Definition.Armor));
                RefreshHealthBar();
            }

            if (HitPoints <= 0f)
            {
                IsDead = true;
            }
        }

        public void ApplySlow(float factor, float duration)
        {
            _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        private void ApplyHaste(float factor, float duration)
        {
            _hasteFactor = Mathf.Max(_hasteFactor, factor);
            _hasteTimer = Mathf.Max(_hasteTimer, duration);
        }

        public void Dispose()
        {
            DestroyObject(Root);
        }

        private void Heal(float amount)
        {
            if (HitPoints >= Definition.HitPoints)
            {
                return;
            }

            HitPoints = Mathf.Min(Definition.HitPoints, HitPoints + amount);
            RefreshHealthBar();
        }

        private void RefreshHealthBar()
        {
            float ratio = Mathf.Clamp01(HitPoints / Definition.HitPoints);
            Vector3 scale = _healthFill.localScale;
            scale.x = Definition.Radius * 2.4f * 0.95f * ratio;
            _healthFill.localScale = scale;
        }

        private void UpdateHeading()
        {
            if (_segment >= _path.Count - 1)
            {
                return;
            }

            Vector3 direction = _path[_segment + 1] - Root.transform.localPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            // The gameplay root follows the authored route, never the camera.
            // Snapping to the next path segment also prevents a slow turn from
            // briefly exposing the creature's face toward the viewer at bends.
            Root.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void UpdateVisualMotion(float deltaTime, float speedFactor)
        {
            float motionRate = Kind == EnemyKind.Support
                ? 1f
                : Mathf.Clamp(speedFactor, 0.30f, 1.45f);
            _motionTime += deltaTime * motionRate;

            float phase = _motionTime * _motionProfile.StepFrequency * Mathf.PI * 2f + _motionPhase;
            float step = 0.5f + 0.5f * Mathf.Sin(phase);
            if (_motionProfile.StepSharpness != 1f)
            {
                step = Mathf.Pow(step, _motionProfile.StepSharpness);
            }

            float sway = Mathf.Sin(phase + Mathf.PI * 0.5f);
            float drift = Mathf.Sin(phase * 0.5f + _motionPhase * 0.37f);
            _motionVisual.localPosition = _motionVisualBasePosition + new Vector3(
                drift * _motionProfile.SidewaysAmplitude,
                _motionProfile.BaseLift + step * _motionProfile.VerticalAmplitude,
                0f);
            _motionVisual.localRotation = _motionVisualBaseRotation * Quaternion.Euler(
                _motionProfile.ForwardLeanDegrees + sway * _motionProfile.PitchWobbleDegrees,
                drift * _motionProfile.YawWobbleDegrees,
                sway * _motionProfile.RollWobbleDegrees);

            float scalePulse = Mathf.Sin(phase) * _motionProfile.ScalePulse;
            _motionVisual.localScale = Vector3.Scale(
                _motionVisualBaseScale,
                new Vector3(1f - scalePulse * 0.45f, 1f + scalePulse, 1f - scalePulse * 0.45f));
        }

        private static Transform CreateMotionVisualPivot(Transform root)
        {
            var pivotObject = new GameObject("Motion Visual");
            pivotObject.layer = root.gameObject.layer;
            Transform pivot = pivotObject.transform;
            pivot.SetParent(root, false);

            // The gameplay root remains on the path while all artwork can bob independently.
            int visualChildCount = root.childCount - 1;
            for (int index = 0; index < visualChildCount; index++)
            {
                root.GetChild(0).SetParent(pivot, false);
            }

            return pivot;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == childName)
                {
                    return descendants[index];
                }
            }

            return null;
        }

        private static MotionProfile MotionProfileFor(EnemyKind kind)
        {
            float scale = GameDefinitions.SpatialScale;
            return kind switch
            {
                // Small dust shadows shiver and hop irregularly.
                EnemyKind.Grunt => new MotionProfile(4.25f, 0.0045f * scale, 0f, 0.0015f * scale, 1f, 0f, 2.0f, 0f, 4.0f, 0.018f, 420f),
                // The burden carrier makes quick, light leaps without affecting its actual speed.
                EnemyKind.Runner => new MotionProfile(7.80f, 0.0120f * scale, 0f, 0.0020f * scale, 0.72f, -5.5f, 4.0f, 1.5f, 5.5f, 0.040f, 600f),
                // The large beast spends more of each cycle planted before a slow, heavy step.
                EnemyKind.Tank => new MotionProfile(1.85f, 0.0060f * scale, 0f, 0.0010f * scale, 2.35f, -1.5f, 1.2f, 0.4f, 1.3f, 0.012f, 210f),
                // The carapace walks low with a restrained side-to-side shell motion.
                EnemyKind.Shield => new MotionProfile(3.00f, 0.0034f * scale, 0f, 0.0030f * scale, 1.35f, 0f, 1.5f, 2.4f, 3.0f, 0.010f, 300f),
                // The angular anger crag rocks forward between uneven steps.
                EnemyKind.Splitter => new MotionProfile(3.75f, 0.0065f * scale, 0f, 0.0022f * scale, 0.88f, -1.0f, 3.2f, 3.8f, 4.4f, 0.020f, 380f),
                // Delusion cloud is the only enemy that clearly hovers above its route.
                EnemyKind.Support => new MotionProfile(1.45f, 0.0120f * scale, 0.0070f * scale, 0.0050f * scale, 1f, 0f, 1.2f, 6.0f, 2.0f, 0.014f, 270f),
                _ => new MotionProfile(3.5f, 0.004f * scale, 0f, 0.001f * scale, 1f, 0f, 2f, 0f, 3f, 0.01f, 360f)
            };
        }

        private readonly struct MotionProfile
        {
            public readonly float StepFrequency;
            public readonly float VerticalAmplitude;
            public readonly float BaseLift;
            public readonly float SidewaysAmplitude;
            public readonly float StepSharpness;
            public readonly float ForwardLeanDegrees;
            public readonly float PitchWobbleDegrees;
            public readonly float YawWobbleDegrees;
            public readonly float RollWobbleDegrees;
            public readonly float ScalePulse;
            public readonly float TurnDegreesPerSecond;

            public MotionProfile(
                float stepFrequency,
                float verticalAmplitude,
                float baseLift,
                float sidewaysAmplitude,
                float stepSharpness,
                float forwardLeanDegrees,
                float pitchWobbleDegrees,
                float yawWobbleDegrees,
                float rollWobbleDegrees,
                float scalePulse,
                float turnDegreesPerSecond)
            {
                StepFrequency = stepFrequency;
                VerticalAmplitude = verticalAmplitude;
                BaseLift = baseLift;
                SidewaysAmplitude = sidewaysAmplitude;
                StepSharpness = stepSharpness;
                ForwardLeanDegrees = forwardLeanDegrees;
                PitchWobbleDegrees = pitchWobbleDegrees;
                YawWobbleDegrees = yawWobbleDegrees;
                RollWobbleDegrees = rollWobbleDegrees;
                ScalePulse = scalePulse;
                TurnDegreesPerSecond = turnDegreesPerSecond;
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }

    public sealed class TowerAgent
    {
        private Transform _turret;
        private Renderer _mergeIndicator;
        private TowerMergeTarget _mergeTarget;
        private Vector3 _homeLocalPosition;
        private float _cooldown;
        private readonly IReadOnlyList<Vector3> _route;

        public TowerKind Kind { get; }
        public TowerDefinition Definition { get; private set; }
        public GameObject Root { get; }
        public Vector2Int Coordinates { get; private set; }
        public int Level { get; private set; } = 1;

        public TowerAgent(
            TowerKind kind,
            Vector2Int coordinates,
            Vector3 localPosition,
            Transform parent,
            IReadOnlyList<Vector3> route = null)
        {
            Kind = kind;
            Coordinates = coordinates;
            _route = route;
            Root = new GameObject($"{GameDefinitions.TowerTierName(kind, 1)} L1");
            Root.layer = 0;
            Root.transform.SetParent(parent, false);
            Root.transform.localPosition = localPosition;
            _homeLocalPosition = localPosition;
            var collider = Root.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.10f * GameDefinitions.SpatialScale, 0f);
            collider.radius = 0.10f * GameDefinitions.SpatialScale;
            _mergeTarget = Root.AddComponent<TowerMergeTarget>();
            _mergeTarget.Initialize(this);
            RebuildVisual();
            FaceNearestRoute(route);
        }

        public bool Upgrade()
        {
            if (Level >= GameDefinitions.MaxTowerLevel)
            {
                return false;
            }

            Level++;
            _cooldown = 0f;
            RebuildVisual();
            FaceNearestRoute(_route);
            return true;
        }

        public void BeginDrag()
        {
            _mergeTarget.SetInteractionEnabled(false);
            Root.transform.localScale = Vector3.one * 1.10f;
            SetMergeHighlight(false, false);
        }

        public void DragToWorld(Vector3 worldPosition)
        {
            Vector3 local = Root.transform.parent.InverseTransformPoint(worldPosition);
            local.y = _homeLocalPosition.y + 0.13f;
            Root.transform.localPosition = local;
        }

        public void SnapDragPreviewTo(Vector3 worldPosition)
        {
            Vector3 local = Root.transform.parent.InverseTransformPoint(worldPosition);
            local.y += 0.13f;
            Root.transform.localPosition = local;
        }

        public void EndDrag(bool returnHome)
        {
            if (returnHome)
            {
                Root.transform.localPosition = _homeLocalPosition;
            }
            Root.transform.localScale = Vector3.one;
            _mergeTarget.SetInteractionEnabled(true);
        }

        public void MoveTo(Vector2Int coordinates, Vector3 localPosition)
        {
            Coordinates = coordinates;
            _homeLocalPosition = localPosition;
            Root.transform.localPosition = localPosition;
            EndDrag(false);
            FaceNearestRoute(_route);
        }

        public void SetMergeHighlight(bool visible, bool valid)
        {
            if (_mergeIndicator == null)
            {
                return;
            }

            _mergeIndicator.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            Color color = valid ? new Color(0.25f, 1f, 0.48f) : new Color(1f, 0.20f, 0.24f);
            _mergeIndicator.material.color = color;
            if (_mergeIndicator.material.HasProperty("_EmissionColor"))
            {
                _mergeIndicator.material.SetColor("_EmissionColor", color * 0.8f);
            }
        }

        public ProjectileAgent TryFire(float deltaTime, IReadOnlyList<EnemyAgent> enemies, Transform projectileParent)
        {
            _cooldown -= deltaTime;
            EnemyAgent target = PickTarget(enemies);
            if (target == null)
            {
                return null;
            }

            Vector3 flatDirection = target.Position - _turret.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                _turret.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }

            if (_cooldown > 0f)
            {
                return null;
            }

            _cooldown = 1f / Definition.FireRate;
            Transform muzzlePoint = _turret.Find("Muzzle Point");
            Vector3 muzzle = muzzlePoint != null
                ? muzzlePoint.position
                : _turret.position +
                  _turret.forward * ((0.08f + Level * 0.008f) * Root.transform.lossyScale.x) +
                  Vector3.up * (0.035f * Root.transform.lossyScale.y);
            return new ProjectileAgent(Kind, Definition, muzzle, target, projectileParent);
        }

        private void FaceNearestRoute(IReadOnlyList<Vector3> route)
        {
            if (_turret == null || route == null || route.Count < 2)
            {
                return;
            }

            Vector3 towerPosition = Root.transform.localPosition;
            towerPosition.y = 0f;
            Vector3 nearestPoint = route[0];
            nearestPoint.y = 0f;
            Vector3 fallbackDirection = route[1] - route[0];
            fallbackDirection.y = 0f;
            float nearestDistance = float.PositiveInfinity;

            for (int index = 0; index < route.Count - 1; index++)
            {
                Vector3 start = route[index];
                Vector3 end = route[index + 1];
                start.y = 0f;
                end.y = 0f;
                Vector3 segment = end - start;
                if (segment.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector3.Dot(towerPosition - start, segment) / segment.sqrMagnitude);
                Vector3 candidate = start + segment * t;
                float distance = (candidate - towerPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = candidate;
                    fallbackDirection = segment;
                }
            }

            Vector3 towardRoute = nearestPoint - towerPosition;
            towardRoute.y = 0f;
            if (towardRoute.sqrMagnitude <= 0.000001f)
            {
                towardRoute = fallbackDirection;
            }

            if (towardRoute.sqrMagnitude > 0.000001f)
            {
                // Route coordinates are authored in the arena's local space;
                // convert the chosen direction before assigning a world
                // rotation so this remains correct when XR places/rotates the
                // tabletop in front of the player's head.
                Vector3 worldDirection = Root.transform.parent != null
                    ? Root.transform.parent.TransformDirection(towardRoute.normalized)
                    : towardRoute.normalized;
                worldDirection.y = 0f;
                if (worldDirection.sqrMagnitude > 0.000001f)
                {
                    _turret.rotation = Quaternion.LookRotation(worldDirection, Vector3.up);
                }
            }
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(Root);
            }
            else
            {
                Object.DestroyImmediate(Root);
            }
        }

        private void RebuildVisual()
        {
            for (int i = Root.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = Root.transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }

            Definition = GameDefinitions.Tower(Kind, Level);
            Root.name = $"{GameDefinitions.TowerTierName(Kind, Level)} L{Level}";
            _turret = ProceduralFactory.BuildTowerVisual(Kind, Root.transform, Level);
            GameObject indicator = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Merge Target Highlight",
                Root.transform,
                new Vector3(0f, 0.014f, 0f),
                new Vector3(0.082f, 0.006f, 0.082f),
                ProceduralFactory.CreateMaterial(new Color(0.25f, 1f, 0.48f), 0.05f, 0.65f));
            _mergeIndicator = indicator.GetComponent<Renderer>();
            _mergeIndicator.gameObject.SetActive(false);
        }

        private EnemyAgent PickTarget(IReadOnlyList<EnemyAgent> enemies)
        {
            // Delusion Fog is a deliberate enemy skill: towers inside its aura
            // cannot acquire a target. The pre-wave briefing explains this
            // before any support enemy enters the route.
            float fogRadiusSquared = Mathf.Pow(0.28f * GameDefinitions.SpatialScale, 2f);
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent fog = enemies[i];
                if (fog.Kind == EnemyKind.Support && fog.HasReachedWeaponGate && !fog.IsDead && !fog.ReachedEnd &&
                    (fog.LocalPosition - Root.transform.localPosition).sqrMagnitude <= fogRadiusSquared)
                {
                    return null;
                }
            }

            EnemyAgent best = null;
            float bestProgress = float.NegativeInfinity;
            float rangeSquared = Definition.Range * Definition.Range;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent candidate = enemies[i];
                if (candidate.IsDead || candidate.ReachedEnd || !candidate.HasReachedWeaponGate)
                {
                    continue;
                }

                if ((candidate.LocalPosition - Root.transform.localPosition).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                if (candidate.Progress > bestProgress)
                {
                    bestProgress = candidate.Progress;
                    best = candidate;
                }
            }

            return best;
        }
    }

    public sealed class ProjectileAgent
    {
        private readonly TowerKind _kind;
        private readonly TowerDefinition _definition;
        private readonly EnemyAgent _target;
        private readonly float _worldScale;

        public GameObject Root { get; }
        public bool IsFinished { get; private set; }
        public TowerKind Kind => _kind;

        public ProjectileAgent(TowerKind kind, TowerDefinition definition, Vector3 worldPosition, EnemyAgent target, Transform parent)
        {
            _kind = kind;
            _definition = definition;
            _target = target;
            _worldScale = Mathf.Max(0.0001f, parent.lossyScale.x);
            Material material = ProceduralFactory.CreateMaterial(definition.Color * 1.25f, 0.1f, 0.65f);
            Root = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Sphere,
                $"{definition.Name} Projectile",
                parent,
                Vector3.zero,
                Vector3.one * (_kind == TowerKind.Cannon ? 0.036f : 0.026f),
                material);
            Root.transform.position = worldPosition;
            CombatVisualEffects.AddProjectileTrail(Root, kind, definition.Color);
        }

        public void Update(float deltaTime, IReadOnlyList<EnemyAgent> enemies)
        {
            if (IsFinished)
            {
                return;
            }

            if (_target == null || _target.IsDead || _target.ReachedEnd)
            {
                Finish();
                return;
            }

            Vector3 direction = _target.Position - Root.transform.position;
            float distance = direction.magnitude;
            float step = _definition.ProjectileSpeed * _worldScale * deltaTime;
            if (distance <= step)
            {
                ResolveHit(enemies);
                Finish();
                return;
            }

            Root.transform.position += direction / distance * step;
        }

        private void ResolveHit(IReadOnlyList<EnemyAgent> enemies)
        {
            Vector3 impactPosition = _target.Position + Vector3.up * (_target.Definition.Radius * 0.65f * _worldScale);
            if (_definition.SplashRadius > 0f)
            {
                Vector3 center = _target.Position;
                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyAgent enemy = enemies[i];
                    if (!enemy.IsDead && !enemy.ReachedEnd &&
                        Vector3.Distance(enemy.Position, center) <= _definition.SplashRadius * _worldScale)
                    {
                        enemy.ApplyDamage(_definition.Damage);
                        if (_definition.SlowDuration > 0f)
                        {
                            enemy.ApplySlow(_definition.SlowFactor, _definition.SlowDuration);
                        }
                    }
                }
            }
            else
            {
                _target.ApplyDamage(_definition.Damage);
                if (_definition.SlowDuration > 0f)
                {
                    _target.ApplySlow(_definition.SlowFactor, _definition.SlowDuration);
                }
            }
            CombatVisualEffects.SpawnTowerImpact(_kind, impactPosition, Root.transform.parent);
        }

        private void Finish()
        {
            IsFinished = true;
            if (Application.isPlaying)
            {
                Object.Destroy(Root);
            }
            else
            {
                Object.DestroyImmediate(Root);
            }
        }
    }
}
