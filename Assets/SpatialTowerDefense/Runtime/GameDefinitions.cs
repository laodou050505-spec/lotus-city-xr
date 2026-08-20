using System;
using System.Collections.Generic;
using UnityEngine;

namespace PicoTowerDefense
{
    public enum TowerKind
    {
        Arrow,
        Cannon,
        Frost
    }

    public enum EnemyKind
    {
        Grunt,
        Runner,
        Tank,
        Shield,
        Splitter,
        Support
    }

    [Serializable]
    public struct TowerDefinition
    {
        public string Name;
        public int Cost;
        public float Range;
        public float Damage;
        public float FireRate;
        public float ProjectileSpeed;
        public float SplashRadius;
        public float SlowFactor;
        public float SlowDuration;
        public Color Color;

        public TowerDefinition(
            string name,
            int cost,
            float range,
            float damage,
            float fireRate,
            float projectileSpeed,
            Color color,
            float splashRadius = 0f,
            float slowFactor = 1f,
            float slowDuration = 0f)
        {
            Name = name;
            Cost = cost;
            Range = range;
            Damage = damage;
            FireRate = fireRate;
            ProjectileSpeed = projectileSpeed;
            SplashRadius = splashRadius;
            SlowFactor = slowFactor;
            SlowDuration = slowDuration;
            Color = color;
        }
    }

    [Serializable]
    public struct EnemyDefinition
    {
        public string Name;
        public float HitPoints;
        public float Speed;
        public int Reward;
        public float Radius;
        public Color Color;
        public float ShieldPoints;
        public float Armor;
        public int CoreDamage;
        public float HealPerSecond;

        public EnemyDefinition(
            string name,
            float hitPoints,
            float speed,
            int reward,
            float radius,
            Color color,
            float shieldPoints = 0f,
            float armor = 0f,
            int coreDamage = 1,
            float healPerSecond = 0f)
        {
            Name = name;
            HitPoints = hitPoints;
            Speed = speed;
            Reward = reward;
            Radius = radius;
            Color = color;
            ShieldPoints = shieldPoints;
            Armor = armor;
            CoreDamage = coreDamage;
            HealPerSecond = healPerSecond;
        }
    }

    public readonly struct SpawnBatch
    {
        public readonly EnemyKind Kind;
        public readonly int Count;
        public readonly float Gap;

        public SpawnBatch(EnemyKind kind, int count, float gap)
        {
            Kind = kind;
            Count = count;
            Gap = gap;
        }
    }

    public static class GameDefinitions
    {
        public const int GridColumns = 10;
        public const int GridRows = 8;
        public const float SpatialScale = 1.22f;
        public const float CellSize = 0.18f * SpatialScale;
        public const float TableHeight = 0.92f;
        public const float TableMargin = 0.15f;
        // Single authored player start shared by the desktop fallback and XR
        // recentering.  The landmark is the centre of the playable tabletop,
        // not a camera-attached screen. Its local coordinates are resolved
        // through the authored arena transform at runtime. The eye position
        // keeps the established Game View composition; the floor-origin rig
        // is then derived from it with DesignPlayerEyeHeight.
        public static readonly Vector3 DesignPlayerViewTargetLocal = new(0f, TableHeight + 0.08f, 0f);
        public const float DesignPlayerYaw = 0f;
        public const float DesignPlayerPitch = 25f;
        public const float DesignPlayerTableDistance = 2.65f;
        public const float DesignPlayerEyeHeight = 1.62f;

        public static Vector3 DesignPlayerEyeLocal
        {
            get
            {
                Quaternion orbit = Quaternion.Euler(DesignPlayerPitch, DesignPlayerYaw, 0f);
                return DesignPlayerViewTargetLocal + orbit * new Vector3(0f, 0f, -DesignPlayerTableDistance);
            }
        }

        // The title is a separate world-space entry stage, not a card placed
        // over an already visible board. Its floor-origin player start faces
        // an opaque Figma title shrine at this landmark. Gameplay has its own
        // tabletop landmark above, so each stage can recenter consistently.
        public static readonly Vector3 TitlePlayerViewTargetWorld = new(0f, DesignPlayerEyeHeight, 3.80f);
        // The authored Figma shrine sits on the +Z side of the title origin.
        // Desktop orbit looks toward +Z from the origin, so the XR floor rig
        // must use the same +Z-facing yaw.  Without this 180-degree heading
        // the title was physically behind the player in PICO: only the
        // passthrough room was visible and the START collider could not be
        // reached by a controller ray.
        public const float TitlePlayerYaw = 180f;
        public const float TitlePlayerPitch = 0f;
        public const float TitlePlayerViewingDistance = 3.80f;
        public static readonly Vector3 TitlePlayerRigWorldPosition = Vector3.zero;

        // The final scene was uniformly enlarged and moved together with the
        // authored terrain. Runtime-only content uses this same parent pose so
        // enemies, towers, board cells, and controls stay aligned without
        // rewriting any saved model transforms.
        public const float AuthoredSceneScale = 291.8798f;
        public static readonly Vector3 AuthoredScenePosition = new(29.526f, -523.945f, 336.538f);
        public const int StartingGold = 200;
        public const int StartingLives = 20;
        public const int MaxTowerLevel = 3;
        public const float PlayerWeaponDamage = 16f;
        public const float PlayerWeaponFireRate = 5f;
        public const float PlayerWeaponRange = 6f;

        // Enemies first cross a short display-only approach from the new outer
        // gate to the existing lower gate. Normal combat starts at that existing
        // gate, before any of the original board route is traversed.
        public const float WeaponActivationProgress = 1f;
        public const float EnemyApproachDistance = 0.48f;

        public static readonly Vector2Int[] PathCorners =
        {
            new(0, 1), new(4, 1), new(4, 3), new(1, 3),
            new(1, 5), new(6, 5), new(6, 6), new(9, 6)
        };

        // This is the saved tabletop layout, rather than a proximity rule.
        // Keeping the placement cells explicit is important: when a designer
        // removes a white weapon tile it must stay removed on every rebuild.
        // The green route cells are added separately by BuildVisibleBoardCellSet.
        private static readonly Vector2Int[] AuthoredPlacementCells =
        {
            new(0, 0), new(0, 2), new(0, 3), new(0, 4), new(0, 5), new(0, 6),
            new(1, 0), new(1, 2), new(1, 6), new(1, 7),
            new(2, 0), new(2, 2), new(2, 4), new(2, 6), new(2, 7),
            new(3, 0), new(3, 2), new(3, 4), new(3, 6), new(3, 7),
            new(4, 0), new(4, 4), new(4, 6), new(4, 7),
            new(5, 0), new(5, 1), new(5, 2), new(5, 3), new(5, 4), new(5, 6), new(5, 7),
            new(6, 1), new(6, 2), new(6, 3), new(6, 4), new(6, 7),
            new(7, 4), new(7, 5), new(7, 7),
            new(8, 4), new(8, 5), new(8, 7),
            new(9, 4), new(9, 5), new(9, 7)
        };

        public static readonly SpawnBatch[][] Waves =
        {
            new[] { new SpawnBatch(EnemyKind.Grunt, 6, 0.82f) },
            new[] { new SpawnBatch(EnemyKind.Grunt, 8, 0.72f) },
            new[] { new SpawnBatch(EnemyKind.Grunt, 8, 0.66f), new SpawnBatch(EnemyKind.Runner, 2, 0.58f) },
            new[] { new SpawnBatch(EnemyKind.Runner, 7, 0.52f), new SpawnBatch(EnemyKind.Splitter, 2, 0.90f) },
            new[] { new SpawnBatch(EnemyKind.Grunt, 7, 0.64f), new SpawnBatch(EnemyKind.Tank, 1, 1.25f), new SpawnBatch(EnemyKind.Runner, 4, 0.50f) },

            new[] { new SpawnBatch(EnemyKind.Grunt, 10, 0.58f), new SpawnBatch(EnemyKind.Support, 2, 1.00f) },
            new[] { new SpawnBatch(EnemyKind.Runner, 10, 0.43f), new SpawnBatch(EnemyKind.Shield, 4, 0.82f) },
            new[] { new SpawnBatch(EnemyKind.Grunt, 11, 0.54f), new SpawnBatch(EnemyKind.Splitter, 6, 0.70f) },
            new[] { new SpawnBatch(EnemyKind.Support, 3, 0.88f), new SpawnBatch(EnemyKind.Shield, 5, 0.76f), new SpawnBatch(EnemyKind.Runner, 6, 0.40f) },
            new[] { new SpawnBatch(EnemyKind.Runner, 8, 0.42f), new SpawnBatch(EnemyKind.Support, 3, 0.80f), new SpawnBatch(EnemyKind.Tank, 1, 1.20f), new SpawnBatch(EnemyKind.Shield, 5, 0.72f) },

            new[] { new SpawnBatch(EnemyKind.Grunt, 14, 0.50f), new SpawnBatch(EnemyKind.Splitter, 5, 0.68f) },
            new[] { new SpawnBatch(EnemyKind.Shield, 7, 0.70f), new SpawnBatch(EnemyKind.Runner, 10, 0.38f) },
            new[] { new SpawnBatch(EnemyKind.Grunt, 17, 0.47f), new SpawnBatch(EnemyKind.Support, 5, 0.76f) },
            new[] { new SpawnBatch(EnemyKind.Splitter, 8, 0.62f), new SpawnBatch(EnemyKind.Shield, 6, 0.68f) },
            new[] { new SpawnBatch(EnemyKind.Splitter, 8, 0.60f), new SpawnBatch(EnemyKind.Grunt, 6, 0.45f), new SpawnBatch(EnemyKind.Tank, 1, 1.15f), new SpawnBatch(EnemyKind.Shield, 4, 0.64f) },

            new[] { new SpawnBatch(EnemyKind.Runner, 16, 0.34f), new SpawnBatch(EnemyKind.Support, 4, 0.74f) },
            new[] { new SpawnBatch(EnemyKind.Shield, 9, 0.62f), new SpawnBatch(EnemyKind.Grunt, 16, 0.43f) },
            new[] { new SpawnBatch(EnemyKind.Runner, 15, 0.32f), new SpawnBatch(EnemyKind.Splitter, 13, 0.54f) },
            new[] { new SpawnBatch(EnemyKind.Support, 6, 0.66f), new SpawnBatch(EnemyKind.Shield, 8, 0.58f), new SpawnBatch(EnemyKind.Grunt, 10, 0.42f) },
            new[] { new SpawnBatch(EnemyKind.Support, 6, 0.62f), new SpawnBatch(EnemyKind.Shield, 8, 0.55f), new SpawnBatch(EnemyKind.Tank, 1, 1.10f), new SpawnBatch(EnemyKind.Runner, 12, 0.32f) },

            new[] { new SpawnBatch(EnemyKind.Grunt, 22, 0.38f), new SpawnBatch(EnemyKind.Runner, 16, 0.30f) },
            new[] { new SpawnBatch(EnemyKind.Splitter, 13, 0.50f), new SpawnBatch(EnemyKind.Support, 8, 0.60f) },
            new[] { new SpawnBatch(EnemyKind.Shield, 12, 0.54f), new SpawnBatch(EnemyKind.Runner, 18, 0.28f) },
            new[] { new SpawnBatch(EnemyKind.Support, 8, 0.56f), new SpawnBatch(EnemyKind.Splitter, 12, 0.48f), new SpawnBatch(EnemyKind.Grunt, 14, 0.36f) },
            new[] { new SpawnBatch(EnemyKind.Shield, 10, 0.50f), new SpawnBatch(EnemyKind.Support, 6, 0.54f), new SpawnBatch(EnemyKind.Splitter, 8, 0.46f), new SpawnBatch(EnemyKind.Tank, 1, 1.05f), new SpawnBatch(EnemyKind.Runner, 11, 0.30f) }
        };

        private static readonly Dictionary<TowerKind, TowerDefinition> TowerDefinitions = new()
        {
            [TowerKind.Arrow] = new TowerDefinition(
                "Sound School", 50, 0.42f * SpatialScale, 7f, 1.9f, 4.2f * SpatialScale, Hex(0x4FA99A),
                splashRadius: 0.10f * SpatialScale, slowFactor: 0.78f, slowDuration: 0.70f),
            [TowerKind.Cannon] = new TowerDefinition(
                "Guardian School", 120, 0.35f * SpatialScale, 22f, 0.8f, 3.0f * SpatialScale, Hex(0xD9A441), splashRadius: 0.16f * SpatialScale),
            [TowerKind.Frost] = new TowerDefinition(
                "Incense School", 90, 0.38f * SpatialScale, 4f, 1.45f, 4.6f * SpatialScale, Hex(0x91C8B0),
                splashRadius: 0.075f * SpatialScale, slowFactor: 0.50f, slowDuration: 1.65f)
        };

        private static readonly Dictionary<EnemyKind, EnemyDefinition> EnemyDefinitions = new()
        {
            [EnemyKind.Grunt] = new EnemyDefinition("Restless Dust", 64f, 0.62f * SpatialScale, 8, 0.045f * SpatialScale, Hex(0x6B7180)),
            [EnemyKind.Runner] = new EnemyDefinition("Grasping Burden", 55f, 0.92f * SpatialScale, 8, 0.043f * SpatialScale, Hex(0x8C755D)),
            [EnemyKind.Tank] = new EnemyDefinition("Ignorance Beast Boss", 800f, 0.28f * SpatialScale, 50, 0.078f * SpatialScale, Hex(0x665B72), armor: 0.38f, coreDamage: 4),
            [EnemyKind.Shield] = new EnemyDefinition("Doubt Carapace", 115f, 0.48f * SpatialScale, 13, 0.052f * SpatialScale, Hex(0x87969B), shieldPoints: 110f),
            [EnemyKind.Splitter] = new EnemyDefinition("Anger Flame", 90f, 0.68f * SpatialScale, 10, 0.049f * SpatialScale, Hex(0xA64E42)),
            [EnemyKind.Support] = new EnemyDefinition("Delusion Fog", 125f, 0.42f * SpatialScale, 16, 0.052f * SpatialScale, Hex(0x7B8591))
        };

        public static TowerDefinition Tower(TowerKind kind) => TowerDefinitions[kind];

        public static TowerDefinition Tower(TowerKind kind, int level)
        {
            TowerDefinition result = TowerDefinitions[kind];
            int clampedLevel = Mathf.Clamp(level, 1, MaxTowerLevel);
            float damageMultiplier = clampedLevel switch { 2 => 1.85f, 3 => 3.25f, _ => 1f };
            float fireRateMultiplier = clampedLevel switch { 2 => 1.18f, 3 => 1.42f, _ => 1f };
            float rangeMultiplier = clampedLevel switch { 2 => 1.10f, 3 => 1.22f, _ => 1f };
            result.Name = $"{result.Name} L{clampedLevel}";
            result.Damage *= damageMultiplier;
            result.FireRate *= fireRateMultiplier;
            result.Range *= rangeMultiplier;
            result.ProjectileSpeed *= 1f + (clampedLevel - 1) * 0.12f;
            result.SplashRadius *= 1f + (clampedLevel - 1) * 0.18f;
            result.SlowFactor = Mathf.Max(0.25f, result.SlowFactor - (clampedLevel - 1) * 0.07f);
            result.SlowDuration *= 1f + (clampedLevel - 1) * 0.22f;
            result.Color = Color.Lerp(result.Color, Color.white, (clampedLevel - 1) * 0.16f);
            return result;
        }

        public static EnemyDefinition Enemy(EnemyKind kind) => EnemyDefinitions[kind];

        /// <summary>
        /// Each wave now sends a denser normal-enemy formation. Bosses remain
        /// one per boss wave so their arrival is still easy for players to read.
        /// </summary>
        public static int SpawnCount(SpawnBatch batch)
        {
            if (batch.Kind == EnemyKind.Tank)
            {
                return batch.Count;
            }

            return batch.Count + Mathf.Max(1, Mathf.CeilToInt(batch.Count * 0.30f));
        }

        public static string TowerTierName(TowerKind kind, int level)
        {
            int clampedLevel = Mathf.Clamp(level, 1, MaxTowerLevel);
            return kind switch
            {
                TowerKind.Arrow => clampedLevel switch
                {
                    1 => "Morning Bell Purification Platform",
                    2 => "Morning Bell Purification Tower",
                    _ => "Grand Morning Bell Tower"
                },
                TowerKind.Cannon => clampedLevel switch
                {
                    1 => "Sutra Guardian Pillar",
                    2 => "Sutra Guardian Wheel Platform",
                    _ => "Sutra Guardian Gate"
                },
                _ => clampedLevel switch
                {
                    1 => "Lotus Dedication Lamp",
                    2 => "Lotus Dedication Pavilion",
                    _ => "Grand Lotus Dedication Altar"
                }
            };
        }

        public static float TerraceOffsetForRow(int row)
        {
            if (row <= 1)
            {
                return 0f;
            }

            return row <= 4 ? 0.095f : 0.19f;
        }

        public static float CellSurfaceHeight(int row) => TableHeight + TerraceOffsetForRow(row);

        public static Vector3 CellLocalPosition(int column, int row)
        {
            float x = (column - (GridColumns - 1) * 0.5f) * CellSize;
            float z = (row - (GridRows - 1) * 0.5f) * CellSize;
            return new Vector3(x, 0f, z);
        }

        public static void ApplyAuthoredSceneTransform(Transform target)
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = AuthoredScenePosition;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one * AuthoredSceneScale;
        }

        public static HashSet<Vector2Int> BuildPathCellSet()
        {
            var result = new HashSet<Vector2Int>();
            for (int i = 0; i < PathCorners.Length; i++)
            {
                Vector2Int current = PathCorners[i];
                result.Add(current);
                if (i == PathCorners.Length - 1)
                {
                    continue;
                }

                Vector2Int next = PathCorners[i + 1];
                Vector2Int direction = new(Math.Sign(next.x - current.x), Math.Sign(next.y - current.y));
                while (current != next)
                {
                    current += direction;
                    result.Add(current);
                }
            }

            return result;
        }

        public static HashSet<Vector2Int> BuildPlacementCellSet()
        {
            return new HashSet<Vector2Int>(AuthoredPlacementCells);
        }

        public static HashSet<Vector2Int> BuildVisibleBoardCellSet()
        {
            HashSet<Vector2Int> result = BuildPathCellSet();
            result.UnionWith(AuthoredPlacementCells);
            return result;
        }

        public static bool Validate(out string error)
        {
            if (GridColumns != 10 || GridRows != 8 || Mathf.Abs(CellSize - 0.18f * SpatialScale) > 0.0001f ||
                AuthoredSceneScale <= 0f)
            {
                error = "Board dimensions no longer match the source Three.js game.";
                return false;
            }

            for (int i = 0; i < PathCorners.Length; i++)
            {
                Vector2Int point = PathCorners[i];
                if (point.x < 0 || point.x >= GridColumns || point.y < 0 || point.y >= GridRows)
                {
                    error = $"Path point {point} is outside the board.";
                    return false;
                }

                if (i > 0)
                {
                    Vector2Int previous = PathCorners[i - 1];
                    if (previous.x != point.x && previous.y != point.y)
                    {
                        error = $"Path segment {previous} -> {point} is diagonal.";
                        return false;
                    }
                }
            }

            if (Waves.Length != 25 || StartingGold != 200 || StartingLives != 20 || MaxTowerLevel != 3)
            {
                error = "Upgraded economy, wave count, or tower level rules are invalid.";
                return false;
            }

            for (int waveIndex = 0; waveIndex < Waves.Length; waveIndex++)
            {
                int bossCount = 0;
                int normalBeforeBoss = 0;
                int normalAfterBoss = 0;
                bool bossSeen = false;
                for (int batchIndex = 0; batchIndex < Waves[waveIndex].Length; batchIndex++)
                {
                    if (Waves[waveIndex][batchIndex].Kind == EnemyKind.Tank)
                    {
                        bossCount += Waves[waveIndex][batchIndex].Count;
                        bossSeen = true;
                    }
                    else if (bossSeen)
                    {
                        normalAfterBoss += Waves[waveIndex][batchIndex].Count;
                    }
                    else
                    {
                        normalBeforeBoss += Waves[waveIndex][batchIndex].Count;
                    }
                }

                int expectedBossCount = (waveIndex + 1) % 5 == 0 ? 1 : 0;
                if (bossCount != expectedBossCount)
                {
                    error = $"Wave {waveIndex + 1} must contain exactly {expectedBossCount} boss enemies.";
                    return false;
                }

                if (expectedBossCount == 1 && normalBeforeBoss < normalAfterBoss)
                {
                    error = $"Boss wave {waveIndex + 1} must place the boss at the midpoint or later.";
                    return false;
                }

                if (expectedBossCount == 1 && WaveEnemyCount(waveIndex) <= WaveEnemyCount(waveIndex - 1))
                {
                    error = $"Boss wave {waveIndex + 1} must contain more enemies than the preceding wave.";
                    return false;
                }
            }

            for (int blockStart = 0; blockStart < Waves.Length; blockStart += 5)
            {
                float previousThreat = 0f;
                for (int waveIndex = blockStart; waveIndex < blockStart + 4; waveIndex++)
                {
                    float threat = WaveThreat(waveIndex);
                    if (threat <= previousThreat)
                    {
                        error = $"Wave {waveIndex + 1} must be harder than the previous normal wave in its block.";
                        return false;
                    }
                    previousThreat = threat;
                }
            }

            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                if (!EnemyDefinitions.ContainsKey(kind))
                {
                    error = $"Enemy definition is missing for {kind}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static int WaveEnemyCount(int waveIndex)
        {
            int count = 0;
            for (int batchIndex = 0; batchIndex < Waves[waveIndex].Length; batchIndex++)
            {
                count += SpawnCount(Waves[waveIndex][batchIndex]);
            }
            return count;
        }

        private static float WaveThreat(int waveIndex)
        {
            float threat = 0f;
            for (int batchIndex = 0; batchIndex < Waves[waveIndex].Length; batchIndex++)
            {
                SpawnBatch batch = Waves[waveIndex][batchIndex];
                int count = SpawnCount(batch);
                float weight = batch.Kind switch
                {
                    EnemyKind.Runner => 1.05f,
                    EnemyKind.Splitter => 1.80f,
                    EnemyKind.Shield => 2.40f,
                    EnemyKind.Support => 2.20f,
                    EnemyKind.Tank => 9.00f,
                    _ => 1.00f
                };
                threat += count * weight;
            }
            return threat;
        }

        private static Color Hex(int value)
        {
            return new Color(
                ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f,
                (value & 0xFF) / 255f,
                1f);
        }
    }
}
