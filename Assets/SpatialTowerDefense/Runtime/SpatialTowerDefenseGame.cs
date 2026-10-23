using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PicoTowerDefense
{
    public sealed class SpatialTowerDefenseGame : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, BoardCell> _cells = new();
        private readonly List<SpatialActionTarget> _actions = new();
        private readonly List<TowerAgent> _towers = new();
        private readonly List<EnemyAgent> _enemies = new();
        private readonly List<ProjectileAgent> _projectiles = new();
        private readonly List<SpawnEvent> _spawnQueue = new();
        private readonly List<Vector3> _pathLocal = new();
        private readonly Dictionary<string, AudioClip> _audioClips = new();

        private Transform _arenaRoot;
        private Transform _actorsRoot;
        private Transform _projectilesRoot;
        private Transform _protectedStructure;
        private Transform _crystal;
        private Transform _baseHealthFill;
        private SpatialInputRig _input;
        private LotusDigitField _goldDigits, _lifeDigits, _waveDigits, _startDigits, _briefingWaveDigits;
        private readonly LotusDigitField[] _enemyCountDigits = new LotusDigitField[6];
        private readonly LotusDigitField[] _towerDigits = new LotusDigitField[4];
        private Renderer _waveArt;
        private Transform _statusHudRoot;
        private Transform _controlsRoot;
        private Transform _statusWaveProgress;
        private Transform _statusHealthFill;
        private Transform _waveBriefingRoot;
        private Transform _interactionGuideRoot;
        private Transform _startWaveProgressFill;
        private Transform _towerInfoRoot;
        private BoardCell _hoveredCell;
        private SpatialActionTarget _hoveredAction;
        private TowerAgent _hoveredMergeTower;
        private TowerAgent _hoveredInfoTower;
        private TowerAgent _draggedTower;
        private SpatialActionTarget _draggedCardAction;
        private Transform _draggedCardGhost;
        private TowerKind _draggedCardKind;
        private const float TowerMergeSnapRadius = 0.14f;
        private const float PlayerWeaponTargetGrace = 0.22f;
        private const float InteractionGuideForwardOffset = 0.50f;
        private static readonly Vector3 InteractionGuideBasePosition = new(1.55f, 1.75f, -0.22f);
        private const string ProtectedStructureObjectName = "tripo_convert_dabf4fdf-9e52-4695-ad96-a0409841049c";
        private LineRenderer _rangeRing;
        private AudioSource _audioSource;
        private AudioSource _musicSource;
        private Transform _startupCoverRoot;
        private Renderer _startupCoverRenderer;
        private Renderer _startupCoverStateRenderer;
        private Material _startupCoverMaterial;
        private Material _startupCoverStateMaterial;
        private Material _enemySmokeMaterial;
        private TowerKind _selectedTower = TowerKind.Arrow;
        private int _gold = GameDefinitions.StartingGold;
        private int _lives = GameDefinitions.StartingLives;
        private int _waveIndex;
        private float _spawnClock;
        private bool _spawning;
        private bool _waveInProgress;
        private bool _gameOver;
        private bool _won;
        private bool _built;
        private bool _xrRigPlacedAtDesignStart;
        private float _worldScale = 1f;
        private float _crystalClock;
        private float _playerWeaponCooldown;
        private float _playerWeaponTargetGrace;
        private EnemyAgent _lockedPlayerWeaponTarget;
        private int _currentWaveEnemyTotal;
        private int _currentWaveEnemyResolved;
        private bool _experienceStarted;
        private float _startupCoverClock;
        private float _startupCoverPressFeedback;
        private float _startupCoverStateAlpha;
        private bool _startupCoverPressArmed;
        private bool _startupCoverTransitioning;
        private float _titleInputReadyAt;
        private bool _titleInputHasSettled;
        private Vector3 _arenaWorldPositionAtBuild;
        private Quaternion _arenaWorldRotationAtBuild;
        private Vector3 _arenaWorldScaleAtBuild;
        private readonly List<TerrainData> _runtimeTerrainData = new();

        private const float MenuMusicVolume = 0.085f;
        private const float GameMusicVolume = 0.14f;
        private const float SfxMasterVolume = 0.62f;

        private readonly struct SpawnEvent
        {
            public readonly EnemyKind Kind;
            public readonly float Time;

            public SpawnEvent(EnemyKind kind, float time)
            {
                Kind = kind;
                Time = time;
            }
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                BuildWorld();
            }
        }

        private void OnDestroy()
        {
            // Room Anchored Arena deliberately lives beside this controller
            // instead of below it, so player motion can never carry the board.
            // Clean that independent root explicitly when the gameplay scene
            // or an editor smoke-test host is destroyed.
            if (_arenaRoot != null)
            {
                GameObject arenaObject = _arenaRoot.gameObject;
                _arenaRoot = null;
                DestroyRuntimeObject(arenaObject);
            }
            foreach (TerrainData data in _runtimeTerrainData)
                if (data != null) DestroyRuntimeObject(data);
            _runtimeTerrainData.Clear();
        }

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            _input.Tick();
            EnsureArenaWorldAnchor();
            bool recenterDown = _input.ConsumeRecenterDown();
            bool cycleTowerDown = _input.ConsumeCycleTowerDown();
            bool startWaveDown = _input.ConsumeStartWaveDown();
            if (_input.HasTrackedHead)
            {
                if (!_xrRigPlacedAtDesignStart || recenterDown)
                {
                    ResetGameplayXrRigToDesignStart();
                }
            }
            else
            {
                if (recenterDown)
                {
                    _input.ResetDesktopView();
                }

            }

            if (!_experienceStarted)
            {
                UpdateStartupCover();
                return;
            }

            if (cycleTowerDown)
            {
                _selectedTower = (TowerKind)(((int)_selectedTower + 1) % Enum.GetValues(typeof(TowerKind)).Length);
                Play("build", 0.35f);
                _input.Pulse(0.22f, 0.04f);
                RefreshStateDisplay();
            }
            if (startWaveDown)
            {
                ExecuteAction(SpatialAction.StartWave);
            }
            if (!_input.HasTrackedHead)
            {
                UpdateDesktopShortcuts();
            }

            UpdateInteraction();
            UpdatePlayerWeapon(Mathf.Min(Time.deltaTime, 0.05f));
            UpdateGame(Mathf.Min(Time.deltaTime, 0.05f));
            AnimateWorld(Time.deltaTime);
        }

        private void UpdateDesktopShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                ExecuteAction(SpatialAction.SelectArrow);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                ExecuteAction(SpatialAction.SelectCannon);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                ExecuteAction(SpatialAction.SelectFrost);
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                ExecuteAction(SpatialAction.StartWave);
            }
        }

        public void BuildWorld()
        {
            if (_built)
            {
                return;
            }

            if (!GameDefinitions.Validate(out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = 1.2f;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            _built = true;

            var inputObject = new GameObject("Player Space XR Rig");
            inputObject.transform.SetParent(transform, false);
            _input = inputObject.AddComponent<SpatialInputRig>();
            _input.Initialize();
            _input.SetControllerPresentationVisible(false);
            // Controller meshes remain hidden, while slim rays make the
            // left placement and right purification roles discoverable.
            _input.SetPointerPresentationVisible(true);

            _arenaRoot = new GameObject("Room Anchored Arena").transform;
            // The arena is a true scene root. Keeping it outside the gameplay
            // controller/XR Rig hierarchy makes head motion and recentering
            // structurally incapable of translating the board with the user.
            _arenaRoot.SetParent(null, false);
            EditableSceneModelLayout authoredLayout = GetComponent<EditableSceneModelLayout>();
            bool useAuthoredScene = authoredLayout != null;
            if (useAuthoredScene)
            {
                GameDefinitions.ApplyAuthoredSceneTransform(_arenaRoot);
                _worldScale = GameDefinitions.AuthoredSceneScale;
            }

            _actorsRoot = new GameObject("Actors").transform;
            _actorsRoot.SetParent(_arenaRoot, false);
            _projectilesRoot = new GameObject("Projectiles").transform;
            _projectilesRoot.SetParent(_arenaRoot, false);

            BuildLightingAndRoom();
            BuildTableAndBoard();
            // Scene-authored decorative models are saved in the editable scene
            // container. Adopt them before generating layout entries so the
            // complete island, including manually positioned assets, follows
            // the same XR world-space anchor as the gameplay board.
            if (useAuthoredScene)
            {
                SceneModelAnchor.AdoptSavedModels(_arenaRoot);
                AdoptAuthoredTerrain(_arenaRoot);
            }
            authoredLayout?.Build(_arenaRoot);
            // Saved objects must first be adopted in their authored coordinate
            // system. Then convert the entire assembled island to real meters,
            // preserving every model's relative pose and the saved scene assets.
            if (useAuthoredScene)
                NormalizeAuthoredArenaToMeters();
            _worldScale = _arenaRoot.lossyScale.x;
            _arenaWorldPositionAtBuild = _arenaRoot.position;
            _arenaWorldRotationAtBuild = _arenaRoot.rotation;
            _arenaWorldScaleAtBuild = _arenaRoot.lossyScale;
            QualitySettings.shadowDistance = 8f * _worldScale;
            EnableAuthoredStairColliders();
            if (!SceneModelAnchor.HasSavedModelForPath("GameplayModels/Scene_S12_ReleasePond"))
            {
                BuildMeadowForestPond();
            }
            BuildObjective();
            BuildSpatialControls();
            BuildInteractionGuide();
            BuildStatusDisplay();
            BuildWaveBriefingDisplay();
            BuildRangeRing();
            BuildTowerHoverDisplay();
            BuildAudio();
            // Gameplay is now a separate scene. This scene never creates the
            // Figma title art; it starts directly at the tabletop phase after
            // LotusTitleStage has unloaded itself through Start.
            _experienceStarted = true;
            SetGameplayInterfaceVisible(true);
            ConfigureGameplayStagePlayerStart();
            OptimizeStaticPresentation();
            // Avoid treating the OS click that focuses a newly opened desktop
            // player as a Start activation. A deliberate ray/mouse press and
            // release after the title is visible is required.
            _titleInputReadyAt = Time.unscaledTime + 0.75f;
            _titleInputHasSettled = false;
            RefreshStateDisplay();
        }

        public void BuildPreviewState()
        {
            BuildWorld();
            StartExperience();
            PlaceTowerAtCell(TowerKind.Arrow, new Vector2Int(0, 0), false);
            PlaceTowerAtCell(TowerKind.Cannon, new Vector2Int(5, 3), false);
            PlaceTowerAtCell(TowerKind.Frost, new Vector2Int(7, 4), false);
            _enemies.Add(new EnemyAgent(EnemyKind.Grunt, _pathLocal, _actorsRoot));
            _enemies.Add(new EnemyAgent(EnemyKind.Runner, _pathLocal, _actorsRoot));
            _enemies.Add(new EnemyAgent(EnemyKind.Tank, _pathLocal, _actorsRoot));
            _enemies.Add(new EnemyAgent(EnemyKind.Shield, _pathLocal, _actorsRoot));
            _enemies.Add(new EnemyAgent(EnemyKind.Support, _pathLocal, _actorsRoot));
            for (int i = 0; i < 42; i++)
            {
                _enemies[0].Update(0.05f);
            }
            for (int i = 0; i < 26; i++)
            {
                _enemies[1].Update(0.05f);
            }
            for (int i = 0; i < 18; i++)
            {
                _enemies[2].Update(0.05f);
            }
            for (int i = 0; i < 12; i++)
            {
                _enemies[3].Update(0.05f);
                _enemies[4].Update(0.05f);
            }
            RefreshStateDisplay();
            FaceAllTextNow();
        }

        public Camera PreviewCamera => _input != null ? _input.Camera : null;
        public Transform ArenaRoot => _arenaRoot;

        public bool RunMergeRuleSmokeTest(out string error)
        {
            BuildWorld();
            Vector2Int[] cells =
            {
                new(0, 0), new(1, 0), new(3, 0), new(4, 0)
            };
            TowerAgent first = PlaceTowerAtCell(TowerKind.Arrow, cells[0], true);
            TowerAgent second = PlaceTowerAtCell(TowerKind.Arrow, cells[1], true);
            TowerAgent third = PlaceTowerAtCell(TowerKind.Arrow, cells[2], true);
            TowerAgent fourth = PlaceTowerAtCell(TowerKind.Arrow, cells[3], true);
            bool merged = TryMergeTowers(first, second, false) &&
                          TryMergeTowers(third, fourth, false) &&
                          TryMergeTowers(second, fourth, false);

            int levelThreeCount = 0;
            for (int i = 0; i < _towers.Count; i++)
            {
                levelThreeCount += _towers[i].Kind == TowerKind.Arrow && _towers[i].Level == 3 ? 1 : 0;
            }

            bool consumedCellsReleased = !_cells[cells[0]].IsOccupied && !_cells[cells[1]].IsOccupied &&
                                         !_cells[cells[2]].IsOccupied && _cells[cells[3]].IsOccupied;
            TowerAgent differentLevel = PlaceTowerAtCell(TowerKind.Arrow, new Vector2Int(5, 0), false);
            TowerAgent differentKind = PlaceTowerAtCell(TowerKind.Cannon, new Vector2Int(6, 0), false);
            bool invalidMergesRejected = !CanMerge(differentLevel, fourth) &&
                                         !CanMerge(differentKind, differentLevel) &&
                                         !CanMerge(fourth, fourth) &&
                                         !TryMergeTowers(differentLevel, fourth, false) &&
                                         !TryMergeTowers(differentKind, differentLevel, false);
            _towers.Remove(differentLevel);
            _towers.Remove(differentKind);
            differentLevel.Dispose();
            differentKind.Dispose();

            if (!merged || _towers.Count != 1 || levelThreeCount != 1 || !consumedCellsReleased || !invalidMergesRejected)
            {
                error = "Manual merge rules failed: only same-kind/same-level towers below level three may merge.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool RunManualDragInteractionSmokeTest(out string error)
        {
            BuildWorld();
            Vector2Int sourceCell = new(0, 0);
            Vector2Int targetCell = new(1, 0);
            TowerAgent source = PlaceTowerAtCell(TowerKind.Arrow, sourceCell, true);
            TowerAgent target = PlaceTowerAtCell(TowerKind.Arrow, targetCell, true);

            BeginTowerDrag(source);
            source.DragToWorld(target.Root.transform.position + Vector3.right * (WorldTowerMergeSnapRadius * 0.65f));
            _hoveredMergeTower = FindNearbyTower(source.Root.transform.position, source);
            bool nearbyTargetDetected = _hoveredMergeTower == target;
            bool validDragMerged = CompleteTowerDrag(false, null);
            bool validState = nearbyTargetDetected && validDragMerged && _draggedTower == null && _hoveredMergeTower == null &&
                              !_cells[sourceCell].IsOccupied && _cells[targetCell].IsOccupied &&
                              _towers.Count == 1 && target.Level == 2;

            Vector2Int invalidSourceCell = new(3, 0);
            Vector2Int invalidTargetCell = new(4, 0);
            TowerAgent invalidSource = PlaceTowerAtCell(TowerKind.Arrow, invalidSourceCell, true);
            TowerAgent invalidTarget = PlaceTowerAtCell(TowerKind.Cannon, invalidTargetCell, true);
            Vector3 invalidSourceHome = invalidSource.Root.transform.localPosition;
            BeginTowerDrag(invalidSource);
            invalidSource.DragToWorld(invalidTarget.Root.transform.position + Vector3.forward * (WorldTowerMergeSnapRadius * 0.65f));
            _hoveredMergeTower = FindNearbyTower(invalidSource.Root.transform.position, invalidSource);
            bool invalidNearbyTargetDetected = _hoveredMergeTower == invalidTarget;
            bool invalidDragMerged = CompleteTowerDrag(false, null);
            bool invalidState = invalidNearbyTargetDetected && !invalidDragMerged && _draggedTower == null && _hoveredMergeTower == null &&
                                _cells[invalidSourceCell].IsOccupied && _cells[invalidTargetCell].IsOccupied &&
                                invalidSource.Level == 1 && invalidTarget.Level == 1 &&
                                Vector3.Distance(invalidSource.Root.transform.localPosition, invalidSourceHome) < 0.0001f;

            Vector2Int movedCellCoordinates = new(5, 0);
            BoardCell movedCell = _cells[movedCellCoordinates];
            BeginTowerDrag(invalidSource);
            invalidSource.DragToWorld(_arenaRoot.TransformPoint(CellTowerLocalPosition(movedCellCoordinates)));
            bool validMove = CompleteTowerDrag(false, movedCell);
            bool moveState = validMove && !_cells[invalidSourceCell].IsOccupied && movedCell.IsOccupied &&
                             invalidSource.Coordinates == movedCellCoordinates &&
                             Vector3.Distance(invalidSource.Root.transform.localPosition, CellTowerLocalPosition(movedCellCoordinates)) < 0.0001f;

            if (!validState || !invalidState || !moveState)
            {
                error = "Manual drag interaction failed to merge, return an invalid drop, or move a deployed tower to an empty cell.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool RunBuildCardDragSmokeTest(out string error)
        {
            BuildWorld();
            TowerAgent arrow = PlaceTowerAtCell(TowerKind.Arrow, new Vector2Int(0, 0), true);
            int startingGold = _gold;
            Vector3 nearbyCardPosition = arrow.Root.transform.position + Vector3.right * (WorldTowerMergeSnapRadius * 0.65f);
            TowerAgent nearbyCardTarget = FindNearbyTower(nearbyCardPosition, null);
            bool upgradedFromCard = nearbyCardTarget == arrow && TryUseBuildCard(TowerKind.Arrow, null, nearbyCardTarget, false);
            TowerAgent levelTwoArrow = PlaceTowerAtCell(TowerKind.Arrow, new Vector2Int(2, 0), true);
            levelTwoArrow.Upgrade();
            TowerAgent cannon = PlaceTowerAtCell(TowerKind.Cannon, new Vector2Int(3, 0), true);
            bool rejectedMismatchedCardTarget = !CanUseBuildCardOnTower(TowerKind.Arrow, levelTwoArrow) &&
                                               !CanUseBuildCardOnTower(TowerKind.Arrow, cannon);
            BoardCell cannonCell = _cells[new Vector2Int(1, 0)];
            bool deployedFromCard = TryUseBuildCard(TowerKind.Cannon, cannonCell, null, false);
            int expectedGold = startingGold - GameDefinitions.Tower(TowerKind.Arrow).Cost - GameDefinitions.Tower(TowerKind.Cannon).Cost;
            bool rejectedWithoutGold = !TryUseBuildCard(TowerKind.Frost, _cells[new Vector2Int(4, 0)], null, false);

            if (!upgradedFromCard || arrow.Level != 2 || !deployedFromCard || !cannonCell.IsOccupied ||
                !rejectedMismatchedCardTarget || _gold != expectedGold || !rejectedWithoutGold)
            {
                error = "Build-card drag rules failed to detect a nearby matching level-one tower, reject mismatched targets, deploy a tower, or reject an unaffordable card.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool RunPlayerWeaponSmokeTest(out string error)
        {
            BuildWorld();
            Vector3 postGateDirection = (_pathLocal[2] - _pathLocal[1]).normalized;
            var enemy = new EnemyAgent(
                EnemyKind.Grunt,
                _pathLocal,
                _actorsRoot,
                startSegment: 1,
                startLocalPosition: _pathLocal[1] + postGateDirection * 0.02f);
            _enemies.Add(enemy);
            Vector3 worldAimDirection = _actorsRoot.TransformDirection(postGateDirection);
            Ray aimedRay = new(enemy.Position - worldAimDirection, worldAimDirection);
            EnemyAgent acquired = FindPlayerWeaponTarget(aimedRay);
            float hitPointsBefore = enemy.HitPoints;
            bool fired = FirePlayerWeapon(acquired, false);
            float expectedDamage = GameDefinitions.PlayerWeaponDamage * (1f - enemy.Definition.Armor);
            bool damageValid = Mathf.Abs(enemy.HitPoints - (hitPointsBefore - expectedDamage)) < 0.001f;
            enemy.Dispose();
            bool disposedTargetRejected = !enemy.IsValid &&
                                          !IsPlayerWeaponTargetValid(enemy, aimedRay.origin) &&
                                          !FirePlayerWeapon(enemy, false);

            if (!fired || acquired != enemy || !damageValid || !disposedTargetRejected)
            {
                error = "The right-hand player weapon failed acquisition, damage, or destroyed-target rejection.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool RunCombatContinuitySmokeTest(out string error)
        {
            BuildWorld();
            StartExperience();
            bool briefingVisible = _waveBriefingRoot != null && _waveBriefingRoot.gameObject.activeSelf &&
                                   _enemyCountDigits[0] != null && _enemyCountDigits[0].Value == "8";
            bool waveStarted = StartNextWave();
            bool briefingHiddenDuringWave = _waveBriefingRoot != null && !_waveBriefingRoot.gameObject.activeSelf;
            // Place the smoke-test tower beside the existing lower gate so the
            // test exercises both the protected approach and normal acquisition.
            TowerAgent tower = PlaceTowerAtCell(TowerKind.Arrow, new Vector2Int(0, 0), true);
            var support = new EnemyAgent(EnemyKind.Support, _pathLocal, _actorsRoot);
            _enemies.Add(support);

            ProjectileAgent blockedProjectile = tower.TryFire(1f, _enemies, _projectilesRoot);
            Vector3 expectedHeading = (_pathLocal[1] - _pathLocal[0]).normalized;
            Vector3 actualHeading = support.Root.transform.localRotation * Vector3.forward;
            bool headingValid = Vector3.Dot(actualHeading, expectedHeading) > 0.999f;

            _enemies.Clear();
            support.Dispose();
            Vector3 postGateDirection = (_pathLocal[2] - _pathLocal[1]).normalized;
            var grunt = new EnemyAgent(
                EnemyKind.Grunt,
                _pathLocal,
                _actorsRoot,
                startSegment: 1,
                startLocalPosition: _pathLocal[1] + postGateDirection * 0.02f);
            _enemies.Add(grunt);
            ProjectileAgent projectile = tower.TryFire(1f, _enemies, _projectilesRoot);

            if (!briefingVisible || !waveStarted || !briefingHiddenDuringWave ||
                blockedProjectile != null || projectile == null || !headingValid)
            {
                error = $"Enemy skills failed: briefing={briefingVisible}, waveStarted={waveStarted}, briefingHidden={briefingHiddenDuringWave}, " +
                        $"preGateBlocked={blockedProjectile == null}, postGateTargetable={projectile != null}, heading={headingValid}.";
                return false;
            }

            DestroyRuntimeObject(projectile.Root);
            error = string.Empty;
            return true;
        }

        public bool RunStartupCoverCleanupSmokeTest(out string error)
        {
            BuildWorld();
            bool noTitleObjectsInGameplay = _startupCoverRoot == null &&
                                            _startupCoverRenderer == null &&
                                            _startupCoverStateRenderer == null;
            if (!_experienceStarted || !noTitleObjectsInGameplay)
            {
                error = "Gameplay scene must not instantiate a startup overlay; the independent title scene owns all title visuals and Start targets.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool RunWorldSpacePresentationSmokeTest(out string error)
        {
            BuildWorld();
            bool arenaPresentation = _controlsRoot != null && _controlsRoot.parent == _arenaRoot &&
                                     _statusHudRoot != null && _statusHudRoot.parent == _arenaRoot &&
                                     _waveBriefingRoot != null && _waveBriefingRoot.parent == _arenaRoot &&
                                     _interactionGuideRoot != null && _interactionGuideRoot.parent == _arenaRoot &&
                                     _actorsRoot != null && _actorsRoot.parent == _arenaRoot;
            bool arenaIndependentFromPlayer = _arenaRoot.parent == null &&
                                              _arenaRoot != _input.transform &&
                                              !_arenaRoot.IsChildOf(_input.transform);
            bool primaryPanelsFaceViewer =
                _controlsRoot.GetComponent<BillboardText>() != null &&
                _statusHudRoot.GetComponent<BillboardText>() != null &&
                _waveBriefingRoot.GetComponent<BillboardText>() != null &&
                _interactionGuideRoot.GetComponent<BillboardText>() != null;
            bool noTitleInGameplay = _startupCoverRoot == null;
            Vector3 target = _arenaRoot.TransformPoint(GameDefinitions.DesignPlayerViewTargetLocal);
            // Capture the desktop design composition before explicitly
            // exercising the XR-only recenter path below.
            float expectedDistance = GameDefinitions.DesignPlayerTableDistance;
            float actualDistance = Vector3.Distance(_input.Camera.transform.position, target);
            bool desktopPoseMatches = Mathf.Abs(actualDistance - expectedDistance) < 0.02f * _worldScale;
            Vector3 arenaPositionBeforeRecenter = _arenaRoot.position;
            Quaternion arenaRotationBeforeRecenter = _arenaRoot.rotation;
            _input.transform.position += new Vector3(0.48f, 0.12f, -0.31f) * _worldScale;
            _input.transform.rotation *= Quaternion.Euler(0f, 32f, 0f);
            EnsureArenaWorldAnchor();
            ResetGameplayXrRigToDesignStart();
            bool arenaStayedFixed = Vector3.Distance(arenaPositionBeforeRecenter, _arenaRoot.position) < 0.0001f &&
                                    Quaternion.Angle(arenaRotationBeforeRecenter, _arenaRoot.rotation) < 0.001f;
            if (!arenaPresentation || !arenaIndependentFromPlayer || !primaryPanelsFaceViewer ||
                !arenaStayedFixed || !noTitleInGameplay || !desktopPoseMatches)
            {
                error = $"Gameplay presentation failed: arena={arenaPresentation}, independent={arenaIndependentFromPlayer}, fixedPanels={primaryPanelsFaceViewer}, fixedAfterRecenter={arenaStayedFixed}, noTitle={noTitleInGameplay}, distance={actualDistance:F3}, expected={expectedDistance:F3}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool RunInteractionGuideLifecycleSmokeTest(out string error)
        {
            BuildWorld();
            Vector3 expectedPosition = InteractionGuidePositionTowardViewer();
            bool positionValid = _interactionGuideRoot != null &&
                                 Vector3.Distance(_interactionGuideRoot.localPosition, expectedPosition) < 0.0001f;
            Vector3 movement = expectedPosition - InteractionGuideBasePosition;
            Vector3 directionToViewer = (GameDefinitions.DesignPlayerEyeLocal - InteractionGuideBasePosition).normalized;
            bool movementDirectionValid = Mathf.Abs(movement.magnitude - InteractionGuideForwardOffset) < 0.0001f &&
                                          Vector3.Dot(movement.normalized, directionToViewer) > 0.9999f;
            bool visibleBeforeWave = _interactionGuideRoot != null && _interactionGuideRoot.gameObject.activeSelf;
            bool waveStarted = StartNextWave();
            bool visibleDuringFirstWave = _interactionGuideRoot != null && _interactionGuideRoot.gameObject.activeSelf;

            // Resolve the first wave without spawning its enemies. This tests
            // the lifecycle boundary directly while leaving production wave
            // composition and combat timing untouched.
            _spawning = false;
            _spawnQueue.Clear();
            UpdateGame(0f);
            bool hiddenAfterFirstWave = _waveIndex == 1 &&
                                        _interactionGuideRoot != null &&
                                        !_interactionGuideRoot.gameObject.activeSelf;

            if (!positionValid || !movementDirectionValid || !visibleBeforeWave || !waveStarted ||
                !visibleDuringFirstWave || !hiddenAfterFirstWave)
            {
                error = $"Two-hand guide failed its camera-facing/lifecycle contract: position={positionValid}, direction={movementDirectionValid}, before={visibleBeforeWave}, started={waveStarted}, during={visibleDuringFirstWave}, after={hiddenAfterFirstWave}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void BuildLightingAndRoom()
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var skybox = new Material(skyShader);
                skybox.SetColor("_SkyTint", new Color(0.40f, 0.56f, 0.60f));
                skybox.SetColor("_GroundColor", new Color(0.12f, 0.15f, 0.15f));
                skybox.SetFloat("_SunSize", 0.035f);
                skybox.SetFloat("_AtmosphereThickness", 0.85f);
                skybox.SetFloat("_Exposure", 0.62f);
                RenderSettings.skybox = skybox;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.47f, 0.43f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.31f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.13f, 0.10f);

            // Gentle temple mist — linear fog adds depth without obscuring
            // the close-up boardgame scale, and complements the celadon palette.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.34f, 0.38f, 0.37f, 1f);
            RenderSettings.fogStartDistance = 4.5f;
            RenderSettings.fogEndDistance = 16.0f;

            var sunObject = new GameObject("Key Light");
            sunObject.transform.SetParent(_arenaRoot, false);
            sunObject.transform.localPosition = new Vector3(-1.9f, 3.6f, -2.2f);
            sunObject.transform.localRotation = Quaternion.Euler(48f, -28f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 0.68f;
            sun.shadows = LightShadows.Hard;
            sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            RenderSettings.sun = sun;

            var fillObject = new GameObject("Cool Fill Light");
            fillObject.transform.SetParent(_arenaRoot, false);
            fillObject.transform.localRotation = Quaternion.Euler(58f, 142f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.50f, 0.72f, 1f);
            fill.intensity = 0.14f;
            fill.shadows = LightShadows.None;

        }

        private void BuildTableAndBoard()
        {
            float width = GameDefinitions.GridColumns * GameDefinitions.CellSize;
            float depth = GameDefinitions.GridRows * GameDefinitions.CellSize;

            // The saved scene already contains the two water-island foundations
            // visible in the approved reference. Only smoke-test or empty scenes
            // need the generated Meadow/rock fallback; stacking both versions
            // produces the oversized slab that hides the authored islands.
            if (!SceneModelAnchor.HasSavedModelForPath("GameplayModels/Scene_S12_ReleasePond"))
            {
                BuildMeadowForestGroundUnderlay(width, depth);
                BuildRockyThreeTierBase(width, depth);
            }

            GameObject stonePathPrefab = Resources.Load<GameObject>(ProceduralFactory.BoardCellVisualResourcePath);
            if (stonePathPrefab == null)
            {
                throw new InvalidOperationException($"Board tile model is missing: Resources/{ProceduralFactory.BoardCellVisualResourcePath}");
            }
            GameObject routeTilePrefab = Resources.Load<GameObject>(ProceduralFactory.BoardRouteVisualResourcePath);
            if (routeTilePrefab == null)
            {
                throw new InvalidOperationException($"Route tile model is missing: Resources/{ProceduralFactory.BoardRouteVisualResourcePath}");
            }

            HashSet<Vector2Int> pathCells = GameDefinitions.BuildPathCellSet();
            HashSet<Vector2Int> placementCells = GameDefinitions.BuildPlacementCellSet();
            for (int column = 0; column < GameDefinitions.GridColumns; column++)
            {
                for (int row = 0; row < GameDefinitions.GridRows; row++)
                {
                    Vector2Int coordinates = new(column, row);
                    bool isPath = pathCells.Contains(coordinates);
                    bool isBuildable = placementCells.Contains(coordinates);
                    bool showBoardTile = isPath || isBuildable;
                    var tile = new GameObject($"Cell {column},{row}");
                    tile.layer = 0;
                    tile.transform.SetParent(_arenaRoot, false);
                    Vector3 local = GameDefinitions.CellLocalPosition(column, row);
                    tile.transform.localPosition = new Vector3(local.x, GameDefinitions.CellSurfaceHeight(row) + 0.001f, local.z);
                    var collider = tile.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0f, 0.042f, 0f);
                    collider.size = new Vector3(GameDefinitions.CellSize * 0.955f, 0.090f, GameDefinitions.CellSize * 0.955f);
                    Renderer[] renderers = System.Array.Empty<Renderer>();
                    if (showBoardTile)
                    {
                        // Requested layout: mossy tiles mark the enemy route and the
                        // white square stones are exclusively tower placement cells.
                        GameObject tilePrefab = isPath ? routeTilePrefab : stonePathPrefab;
                        string tileResourcePath = isPath ? ProceduralFactory.BoardRouteVisualResourcePath : ProceduralFactory.BoardPlacementVisualResourcePath;
                        renderers = ProceduralFactory.BuildBoardCellVisual(tilePrefab, tile.transform, tileResourcePath);
                    }
                    else
                    {
                        // Keep the logical grid for route calculations, while leaving
                        // the intentionally empty cells free for the authored scenery.
                        collider.enabled = false;
                        tile.name += " (Scenery Reserve)";
                    }
                    var cell = tile.AddComponent<BoardCell>();
                    cell.Initialize(column, row, isPath, renderers, isBuildable);
                    _cells.Add(coordinates, cell);
                }
            }

            Vector2Int existingGateCell = GameDefinitions.PathCorners[0];
            Vector3 approachStart = GameDefinitions.CellLocalPosition(existingGateCell.x, existingGateCell.y);
            approachStart.x -= GameDefinitions.EnemyApproachDistance;
            approachStart.y = RouteTileSurfaceHeight(existingGateCell.y) + 0.004f;
            _pathLocal.Add(approachStart);

            for (int i = 0; i < GameDefinitions.PathCorners.Length; i++)
            {
                Vector3 point = GameDefinitions.CellLocalPosition(GameDefinitions.PathCorners[i].x, GameDefinitions.PathCorners[i].y);
                point.y = RouteTileSurfaceHeight(GameDefinitions.PathCorners[i].y) + 0.004f;
                _pathLocal.Add(point);
            }
        }

        private void BuildMeadowForestGroundUnderlay(float boardWidth, float boardDepth)
        {
            const string groundName = "Meadow Forest Grass Ground Underlay";
            if (_arenaRoot != null && _arenaRoot.Find(groundName) != null)
            {
                return;
            }

            ProceduralFactory.BuildImportedGroundPatch(
                "PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Ground_Mound_Large_01",
                groundName,
                _arenaRoot,
                new Vector3(0f, -0.004f, 0.02f),
                boardWidth * 1.20f,
                boardDepth * 1.16f,
                GameDefinitions.TableHeight - 0.092f,
                0f);
        }

        /// <summary>
        /// Adds the requested lower-right water surface without touching any
        /// saved model Transform or gameplay board cell.
        /// </summary>
        private void BuildMeadowForestPond()
        {
            const string waterName = "Meadow Forest Right Lower Pond Water";
            if (_arenaRoot != null && _arenaRoot.Find(waterName) != null)
            {
                return;
            }

            float boardWidth = GameDefinitions.GridColumns * GameDefinitions.CellSize;
            float boardDepth = GameDefinitions.GridRows * GameDefinitions.CellSize;
            float halfWidth = boardWidth * 0.5f;
            float halfDepth = boardDepth * 0.5f;
            float pondX = halfWidth * 0.62f;
            float pondZ = -halfDepth - 0.055f;
            float pondSurface = GameDefinitions.TableHeight + 0.105f;

            var pondRoot = new GameObject(waterName).transform;
            pondRoot.SetParent(_arenaRoot, false);

            // The imported water plane carries the package's scrolling shader.
            // Add opaque readable layers underneath it for desktop/Metal and MR
            // runtimes where that shader can resolve nearly transparent.
            Material pondShore = ProceduralFactory.CreateMaterial(new Color(0.08f, 0.20f, 0.16f), 0f, 0.30f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Meadow Forest Pond Shore",
                pondRoot,
                new Vector3(pondX, pondSurface - 0.010f, pondZ),
                new Vector3(0.66f, 0.006f, 0.40f),
                pondShore);
            Material readableWater = ProceduralFactory.CreateMaterial(new Color(0.08f, 0.38f, 0.52f), 0.04f, 0.72f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Meadow Forest Pond Readable Surface",
                pondRoot,
                new Vector3(pondX, pondSurface, pondZ),
                new Vector3(0.58f, 0.006f, 0.34f),
                readableWater);

            Material ripple = ProceduralFactory.CreateMaterial(new Color(0.34f, 0.76f, 0.82f), 0.02f, 0.82f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Meadow Forest Pond Ripple",
                pondRoot,
                new Vector3(pondX, pondSurface + 0.008f, pondZ),
                new Vector3(0.28f, 0.003f, 0.12f),
                ripple);
        }

        private static float RouteTileSurfaceHeight(int row)
        {
            return GameDefinitions.CellSurfaceHeight(row) + ProceduralFactory.RouteTileTopOffset;
        }

        private static float PlacementTileSurfaceHeight(int row)
        {
            return GameDefinitions.CellSurfaceHeight(row) + ProceduralFactory.PlacementTileTopOffset;
        }

        private void BuildRockyThreeTierBase(float width, float depth)
        {
            Material lowerRock = ProceduralFactory.CreateMaterial(new Color(0.30f, 0.32f, 0.29f), 0.01f, 0.18f);
            Material deepRock = ProceduralFactory.CreateMaterial(new Color(0.42f, 0.43f, 0.38f), 0.01f, 0.22f);
            Material weatheredRock = ProceduralFactory.CreateMaterial(new Color(0.56f, 0.55f, 0.47f), 0.01f, 0.26f);
            Material terraceStone = ProceduralFactory.CreateMaterial(new Color(0.67f, 0.66f, 0.58f), 0.01f, 0.32f);
            Material terraceTop = ProceduralFactory.CreateMaterial(new Color(0.24f, 0.34f, 0.25f), 0f, 0.26f);
            float middleSurface = GameDefinitions.CellSurfaceHeight(3);
            float upperSurface = GameDefinitions.CellSurfaceHeight(6);

            var foundation = new GameObject("Gameplay Three Tier Foundation").transform;
            foundation.gameObject.layer = 2;
            foundation.SetParent(_arenaRoot, false);

            // The authored board tiles are the visual top surface. The island
            // therefore stays strictly underneath them; no imported grass chunk
            // is aligned to a tile top, which was the source of the occlusion.
            ProceduralFactory.BuildImportedGroundPatch(
                "GameplayModels/Scene_S01_FloatingMountainBase",
                "Saved Floating Island Underside",
                foundation,
                new Vector3(0f, 0f, 0.02f),
                width * 1.22f,
                depth * 1.18f,
                GameDefinitions.TableHeight - 0.085f,
                0f);

            // Three low shells provide the requested stepped island silhouette.
            // Their top faces stop below the corresponding tile bottoms, leaving
            // every authored white/green brick unobstructed and selectable.
            BuildTerrainLayer(
                "Lower Weathered Mountain Layer",
                foundation,
                Vector3.zero,
                width * 0.82f,
                depth * 0.88f,
                0.20f,
                GameDefinitions.TableHeight - 0.075f,
                0.08f,
                0.82f,
                0.050f,
                lowerRock,
                deepRock);
            BuildTerrainLayer(
                "Middle Weathered Mountain Layer",
                foundation,
                new Vector3(0.015f, 0f, 0.015f),
                width * 0.77f,
                depth * 0.84f,
                GameDefinitions.TableHeight - 0.075f,
                middleSurface - 0.075f,
                0.065f,
                0.80f,
                0.055f,
                deepRock,
                weatheredRock);
            BuildTerrainLayer(
                "Upper Mossy Mountain Layer",
                foundation,
                new Vector3(-0.012f, 0f, 0.025f),
                width * 0.74f,
                depth * 0.86f,
                middleSurface - 0.075f,
                upperSurface - 0.075f,
                0.050f,
                0.80f,
                0.042f,
                weatheredRock,
                terraceTop);

            float middleCenterZ = (GameDefinitions.CellLocalPosition(0, 2).z + GameDefinitions.CellLocalPosition(0, 4).z) * 0.5f;
            float upperCenterZ = (GameDefinitions.CellLocalPosition(0, 5).z + GameDefinitions.CellLocalPosition(0, 7).z) * 0.5f;
            float tierDepth = GameDefinitions.CellSize * 3.08f;

            // Narrow support bands sit under each tier. They are deliberately
            // inset from the tile footprint and have a visible height change,
            // while their top remains below the gameplay mesh.
            BuildTerrainLayer(
                "Middle Rounded Temple Terrace",
                foundation,
                new Vector3(0f, 0f, middleCenterZ),
                width * 0.58f,
                tierDepth * 0.60f,
                GameDefinitions.TableHeight - 0.075f,
                middleSurface - 0.022f,
                0.028f,
                0.30f,
                0.018f,
                terraceStone,
                terraceTop);
            BuildTerrainLayer(
                "Upper Rounded Temple Terrace",
                foundation,
                new Vector3(0f, 0f, upperCenterZ),
                width * 0.57f,
                tierDepth * 0.60f,
                middleSurface - 0.075f,
                upperSurface - 0.022f,
                0.025f,
                0.30f,
                0.014f,
                terraceStone,
                terraceTop);
        }

        private static void BuildTerrainLayer(
            string name,
            Transform parent,
            Vector3 center,
            float halfWidth,
            float halfDepth,
            float lowerY,
            float upperY,
            float lowerExpansion,
            float shapePower,
            float irregularity,
            Material sideMaterial,
            Material topMaterial)
        {
            const int segments = 20;
            var vertices = new Vector3[segments * 2 + 1];
            var sideTriangles = new int[segments * 6];
            var topTriangles = new int[segments * 3];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                float shapedX = Mathf.Sign(cosine) * Mathf.Pow(Mathf.Abs(cosine), shapePower);
                float shapedZ = Mathf.Sign(sine) * Mathf.Pow(Mathf.Abs(sine), shapePower);
                float variation = 1f + irregularity * (Mathf.Sin(i * 2.17f) * 0.58f + Mathf.Sin(i * 4.31f + 0.7f) * 0.42f);

                vertices[i] = center + new Vector3(
                    shapedX * (halfWidth + lowerExpansion) * variation,
                    lowerY,
                    shapedZ * (halfDepth + lowerExpansion) * variation);
                vertices[segments + i] = center + new Vector3(
                    shapedX * halfWidth * variation,
                    upperY,
                    shapedZ * halfDepth * variation);

                int next = (i + 1) % segments;
                int sideIndex = i * 6;
                sideTriangles[sideIndex] = i;
                sideTriangles[sideIndex + 1] = segments + i;
                sideTriangles[sideIndex + 2] = segments + next;
                sideTriangles[sideIndex + 3] = i;
                sideTriangles[sideIndex + 4] = segments + next;
                sideTriangles[sideIndex + 5] = next;

                int topIndex = i * 3;
                topTriangles[topIndex] = segments * 2;
                topTriangles[topIndex + 1] = segments + next;
                topTriangles[topIndex + 2] = segments + i;
            }

            vertices[segments * 2] = center + new Vector3(0f, upperY, 0f);
            var mesh = new Mesh { name = name + " Mesh", subMeshCount = 2 };
            mesh.vertices = vertices;
            mesh.SetTriangles(sideTriangles, 0);
            mesh.SetTriangles(topTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var layer = new GameObject(name);
            layer.layer = 2;
            layer.transform.SetParent(parent, false);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            layer.AddComponent<MeshRenderer>().sharedMaterials = new[] { sideMaterial, topMaterial };
        }

        private void BuildRockyTier(
            string name,
            float lowerHeight,
            float upperHeight,
            float centerZ,
            float width,
            float depth,
            Material deepRock,
            Material mossRock,
            Material weatheredRock,
            float stairOpeningX)
        {
            float tierHeight = upperHeight - lowerHeight;
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                $"{name} Stone Heart",
                _arenaRoot,
                new Vector3(0f, lowerHeight + tierHeight * 0.5f - 0.012f, centerZ),
                new Vector3(width, tierHeight + 0.024f, depth),
                deepRock,
                Quaternion.Euler(0f, 1.5f, 0f));
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                $"{name} Level Garden Top",
                _arenaRoot,
                new Vector3(0f, upperHeight - 0.010f, centerZ),
                new Vector3(width - 0.035f, 0.022f, depth - 0.020f),
                mossRock);

            float frontZ = centerZ - depth * 0.5f;
            float backZ = centerZ + depth * 0.5f;
            BuildRockLedge($"{name} Front Cliff", frontZ, lowerHeight, upperHeight, width, deepRock, weatheredRock, stairOpeningX);
            BuildRockLedge($"{name} Back Cliff", backZ, lowerHeight, upperHeight, width, deepRock, weatheredRock, float.MaxValue);

            float sideY = lowerHeight + tierHeight * 0.50f;
            float sideX = width * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int segment = 0; segment < 3; segment++)
                {
                    float z = Mathf.Lerp(frontZ + 0.10f, backZ - 0.10f, segment / 2f);
                    float size = 0.105f + segment * 0.018f;
                    ProceduralFactory.VisualPrimitive(
                        PrimitiveType.Sphere,
                        $"{name} Side Outcrop {side},{segment + 1}",
                        _arenaRoot,
                        new Vector3(side * (sideX - 0.008f), sideY + (segment % 2) * 0.010f, z),
                        new Vector3(size, tierHeight + 0.070f, size * 1.45f),
                        (segment + (side + 1) / 2) % 2 == 0 ? weatheredRock : deepRock,
                        Quaternion.Euler(10f + segment * 3f, side * (16f + segment * 11f), side * 9f));
                }
            }
        }

        private void BuildRockLedge(
            string name,
            float z,
            float lowerHeight,
            float upperHeight,
            float width,
            Material deepRock,
            Material weatheredRock,
            float openingX)
        {
            float tierHeight = upperHeight - lowerHeight;
            int segments = 9;
            float halfWidth = width * 0.5f;
            for (int segment = 0; segment < segments; segment++)
            {
                float t = segment / (float)(segments - 1);
                float x = Mathf.Lerp(-halfWidth + 0.035f, halfWidth - 0.035f, t);
                if (Mathf.Abs(x - openingX) < GameDefinitions.CellSize * 0.60f)
                {
                    continue;
                }

                float wobble = Mathf.Sin(segment * 2.31f) * 0.022f;
                // Overlapping large stones conceal the rectangular shelf core and read as a cliff face.
                float widthScale = 0.165f + (segment % 3) * 0.018f;
                float outward = Mathf.Abs(z) < 0.02f ? 0f : Mathf.Sign(z) * 0.095f;
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Sphere,
                    $"{name} Rock {segment + 1}",
                    _arenaRoot,
                    new Vector3(x, lowerHeight + tierHeight * 0.47f, z + wobble + outward),
                    new Vector3(widthScale, tierHeight + 0.105f, 0.125f + (segment % 2) * 0.020f),
                    segment % 3 == 1 ? weatheredRock : deepRock,
                    Quaternion.Euler(8f + segment % 4 * 4f, segment * 21f, segment % 2 == 0 ? -8f : 9f));
            }
        }

        private void BuildOuterRockFootings(float width, float depth, Material deepRock, Material weatheredRock, Material mossRock)
        {
            float halfWidth = width * 0.70f;
            float halfDepth = depth * 0.82f;
            Vector2[] perimeter =
            {
                new(-halfWidth, -halfDepth), new(-halfWidth * 0.44f, -halfDepth - 0.045f), new(halfWidth * 0.23f, -halfDepth - 0.030f), new(halfWidth, -halfDepth * 0.74f),
                new(halfWidth + 0.035f, -halfDepth * 0.18f), new(halfWidth, halfDepth * 0.46f), new(halfWidth * 0.42f, halfDepth + 0.040f), new(-halfWidth * 0.18f, halfDepth + 0.060f),
                new(-halfWidth * 0.73f, halfDepth), new(-halfWidth - 0.040f, halfDepth * 0.42f), new(-halfWidth - 0.025f, -halfDepth * 0.20f), new(-halfWidth * 0.78f, -halfDepth - 0.025f)
            };

            for (int index = 0; index < perimeter.Length; index++)
            {
                Vector2 position = perimeter[index];
                float size = 0.26f + (index % 4) * 0.035f;
                Material material = index % 5 == 0 ? mossRock : index % 2 == 0 ? weatheredRock : deepRock;
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Sphere,
                    $"Mountain Footing Boulder {index + 1}",
                    _arenaRoot,
                    new Vector3(position.x, 0.49f + (index % 3) * 0.035f, position.y),
                    new Vector3(size * 1.25f, size * 1.60f, size),
                    material,
                    Quaternion.Euler(9f + index % 4 * 6f, index * 31f, index % 2 == 0 ? -12f : 11f));
            }

            for (int ridge = 0; ridge < 8; ridge++)
            {
                float x = Mathf.Lerp(-halfWidth * 0.82f, halfWidth * 0.82f, ridge / 7f);
                float z = ridge % 2 == 0 ? -halfDepth - 0.012f : halfDepth + 0.012f;
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Sphere,
                    $"Weathered Base Ridge {ridge + 1}",
                    _arenaRoot,
                    new Vector3(x, 0.79f + (ridge % 3) * 0.020f, z),
                    new Vector3(0.24f, 0.17f, 0.16f),
                    ridge % 3 == 0 ? mossRock : weatheredRock,
                    Quaternion.Euler(14f, ridge * 22f, ridge % 2 == 0 ? -10f : 8f));
            }
        }

        private void BuildPlayableTerrainDetails(HashSet<Vector2Int> pathCells)
        {
            Material grass = ProceduralFactory.CreateMaterial(new Color(0.16f, 0.39f, 0.23f), 0f, 0.30f);
            Material moss = ProceduralFactory.CreateMaterial(new Color(0.38f, 0.48f, 0.29f), 0f, 0.32f);
            Material stone = ProceduralFactory.CreateMaterial(new Color(0.53f, 0.54f, 0.48f), 0.02f, 0.36f);
            for (int column = 0; column < GameDefinitions.GridColumns; column++)
            {
                for (int row = 0; row < GameDefinitions.GridRows; row++)
                {
                    Vector2Int coordinates = new(column, row);
                    if (pathCells.Contains(coordinates))
                    {
                        continue;
                    }

                    Vector3 position = GameDefinitions.CellLocalPosition(column, row);
                    position.y = GameDefinitions.CellSurfaceHeight(row) + 0.022f;
                    float seed = column * 17f + row * 31f;
                    float offsetX = Mathf.Sin(seed) * 0.042f;
                    float offsetZ = Mathf.Cos(seed * 1.7f) * 0.042f;
                    float patchScale = 0.055f + (seed % 3f) * 0.008f;
                    ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"Grass Tuft {column},{row}", _arenaRoot,
                        position + new Vector3(offsetX, 0.006f, offsetZ), new Vector3(patchScale * 1.6f, 0.014f, patchScale),
                        (column + row) % 3 == 0 ? moss : grass, Quaternion.Euler(0f, seed * 13f, 8f));

                    if ((column * 3 + row * 5) % 7 == 0)
                    {
                        ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"Garden Stone {column},{row}", _arenaRoot,
                            position + new Vector3(-offsetZ * 0.7f, 0.010f, offsetX * 0.7f), new Vector3(0.042f, 0.024f, 0.032f),
                            stone, Quaternion.Euler(11f, seed * 7f, -13f));
                    }
                }
            }
        }

        private void BuildTempleScenery()
        {
            float width = GameDefinitions.GridColumns * GameDefinitions.CellSize;
            float depth = GameDefinitions.GridRows * GameDefinitions.CellSize;
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            Material paleStone = ProceduralFactory.CreateMaterial(new Color(0.70f, 0.69f, 0.62f), 0.02f, 0.42f);
            Material darkStone = ProceduralFactory.CreateMaterial(new Color(0.29f, 0.31f, 0.29f), 0.05f, 0.30f);
            Material mossStone = ProceduralFactory.CreateMaterial(new Color(0.33f, 0.42f, 0.33f), 0.02f, 0.34f);
            Material oldWood = ProceduralFactory.CreateMaterial(new Color(0.19f, 0.12f, 0.08f), 0.04f, 0.27f);
            Material jadeTile = ProceduralFactory.CreateMaterial(new Color(0.10f, 0.31f, 0.27f), 0.12f, 0.38f);
            Material vermilion = ProceduralFactory.CreateMaterial(new Color(0.45f, 0.10f, 0.07f), 0.08f, 0.34f);
            Material gold = ProceduralFactory.CreateMaterial(new Color(0.92f, 0.68f, 0.24f), 0.48f, 0.70f);
            Material water = ProceduralFactory.CreateTransparentMaterial(new Color(0.26f, 0.55f, 0.58f, 0.52f));

            float middleSurface = GameDefinitions.CellSurfaceHeight(3);
            float upperSurface = GameDefinitions.CellSurfaceHeight(6);
            HashSet<Transform> boardChildrenBeforeTempleModels = SnapshotArenaChildren();

            BuildStairFlight("Lower Mountain Steps", 4, 1, 3, paleStone);
            BuildStairFlight("Upper Mountain Steps", 1, 3, 5, paleStone);
            BuildPathPavers(paleStone, oldWood);

            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                "Lower Water Bank",
                _arenaRoot,
                new Vector3(-halfWidth - 0.22f, GameDefinitions.TableHeight + 0.014f, -0.38f),
                new Vector3(0.26f, 0.024f, 0.88f),
                water);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                "Release Pond",
                _arenaRoot,
                new Vector3(halfWidth + 0.20f, middleSurface + 0.014f, -0.10f),
                new Vector3(0.26f, 0.018f, 0.46f),
                water);

            BuildStoneBridge(new Vector3(GameDefinitions.CellLocalPosition(2, 1).x, GameDefinitions.TableHeight + 0.025f,
                GameDefinitions.CellLocalPosition(2, 1).z), paleStone, oldWood);
            BuildRockCluster("Water Bank Rocks", new Vector3(-halfWidth - 0.17f, GameDefinitions.TableHeight + 0.03f, -0.74f), darkStone, mossStone, 0.90f);
            BuildRockCluster("Release Pond Rocks", new Vector3(halfWidth + 0.16f, middleSurface + 0.035f, 0.10f), darkStone, mossStone, 0.74f);

            Vector3 gate = GameDefinitions.CellLocalPosition(0, 1);
            gate.y = GameDefinitions.CellSurfaceHeight(1);
            for (int side = -1; side <= 1; side += 2)
            {
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Mountain Gate Post", _arenaRoot, gate + new Vector3(0f, 0.15f, side * 0.13f), new Vector3(0.055f, 0.30f, 0.055f), vermilion);
            }
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Mountain Gate Beam", _arenaRoot, gate + new Vector3(0f, 0.30f, 0f), new Vector3(0.08f, 0.055f, 0.36f), oldWood);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Mountain Gate Jade Roof", _arenaRoot, gate + new Vector3(0f, 0.36f, 0f), new Vector3(0.12f, 0.035f, 0.43f), jadeTile);

            BuildBellOrDrumTower("Morning Bell Tower Scenery", new Vector3(-halfWidth - 0.17f, middleSurface + 0.02f, 0.22f), false, paleStone, oldWood, jadeTile, gold);
            BuildBellOrDrumTower("Evening Drum Tower Scenery", new Vector3(halfWidth + 0.16f, upperSurface + 0.02f, 0.51f), true, paleStone, oldWood, jadeTile, vermilion);
            BuildTempleHall(new Vector3(0.50f, upperSurface + 0.02f, halfDepth + 0.20f), paleStone, oldWood, jadeTile, vermilion, gold);
            BuildBuddhaNiche(new Vector3(-0.43f, upperSurface + 0.04f, halfDepth + 0.30f), paleStone, darkStone, gold);
            BuildStonePagoda(new Vector3(halfWidth + 0.20f, upperSurface + 0.02f, halfDepth + 0.19f), paleStone, jadeTile, gold);
            BuildMountainBackdrop(new Vector3(-0.14f, upperSurface + 0.10f, halfDepth + 0.45f), darkStone, mossStone);

            for (int lantern = 0; lantern < 6; lantern++)
            {
                float x = -0.86f + lantern * 0.34f;
                BuildStoneLantern($"Restoration Lantern {lantern + 1}", new Vector3(x, upperSurface + 0.02f, halfDepth + 0.09f), paleStone, oldWood, gold);
            }

            BuildBodhiTree("Lower Bodhi Tree", new Vector3(-halfWidth - 0.18f, GameDefinitions.TableHeight + 0.025f, 0.48f), oldWood, mossStone, gold, 0.82f);
            BuildBodhiTree("Upper Bodhi Tree", new Vector3(-halfWidth - 0.14f, upperSurface + 0.025f, halfDepth + 0.18f), oldWood, jadeTile, gold, 0.72f);
            GroupNewArenaChildren(boardChildrenBeforeTempleModels, "Placed Generated Temple Models");
        }

        private HashSet<Transform> SnapshotArenaChildren()
        {
            var children = new HashSet<Transform>();
            foreach (Transform child in _arenaRoot)
            {
                children.Add(child);
            }

            return children;
        }

        private void GroupNewArenaChildren(HashSet<Transform> existingChildren, string groupName)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(_arenaRoot, false);
            var generatedChildren = new List<Transform>();
            foreach (Transform child in _arenaRoot)
            {
                if (!existingChildren.Contains(child) && child != group)
                {
                    generatedChildren.Add(child);
                }
            }

            foreach (Transform child in generatedChildren)
            {
                child.SetParent(group, true);
            }
        }

        private void BuildTempleHall(Vector3 basePosition, Material stone, Material wood, Material tile, Material vermilion, Material gold)
        {
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Main Hall Stone Base", _arenaRoot, basePosition, new Vector3(0.72f, 0.10f, 0.36f), stone);
            for (int side = -1; side <= 1; side += 2)
            {
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Main Hall Column", _arenaRoot, basePosition + new Vector3(side * 0.25f, 0.20f, -0.08f), new Vector3(0.045f, 0.40f, 0.045f), vermilion);
            }
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Main Hall Timber", _arenaRoot, basePosition + new Vector3(0f, 0.24f, 0f), new Vector3(0.58f, 0.34f, 0.28f), wood);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Main Hall Jade Roof", _arenaRoot, basePosition + new Vector3(0f, 0.45f, 0f), new Vector3(0.82f, 0.075f, 0.52f), tile, Quaternion.Euler(0f, 4f, 0f));
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Main Hall Upper Roof", _arenaRoot, basePosition + new Vector3(0f, 0.54f, 0.01f), new Vector3(0.58f, 0.052f, 0.36f), tile, Quaternion.Euler(0f, -4f, 0f));
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Main Hall Door Screen", _arenaRoot, basePosition + new Vector3(0f, 0.23f, -0.15f), new Vector3(0.22f, 0.22f, 0.016f), vermilion);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, "Main Hall Roof Finial", _arenaRoot, basePosition + new Vector3(0f, 0.60f, 0f), new Vector3(0.035f, 0.050f, 0.035f), gold);
            for (int step = 0; step < 3; step++)
            {
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"Main Hall Entry Step {step + 1}", _arenaRoot,
                    basePosition + new Vector3(0f, 0.02f + step * 0.018f, -0.24f - step * 0.026f),
                    new Vector3(0.42f + step * 0.07f, 0.025f, 0.065f), stone);
            }
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Main Hall Lamp", _arenaRoot, basePosition + new Vector3(0f, 0.31f, -0.18f), Vector3.one * 0.055f, gold);
        }

        private void BuildBuddhaNiche(Vector3 basePosition, Material stone, Material darkStone, Material gold)
        {
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Mountain Grotto Wall", _arenaRoot, basePosition + new Vector3(0f, 0.34f, 0.08f), new Vector3(0.72f, 0.72f, 0.24f), darkStone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Grotto Cliff Left", _arenaRoot, basePosition + new Vector3(-0.28f, 0.36f, 0.01f), new Vector3(0.32f, 0.54f, 0.24f), darkStone, Quaternion.Euler(0f, 20f, -14f));
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Grotto Cliff Right", _arenaRoot, basePosition + new Vector3(0.28f, 0.40f, 0.02f), new Vector3(0.30f, 0.60f, 0.24f), darkStone, Quaternion.Euler(0f, -16f, 12f));
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Buddha Niche Arch", _arenaRoot, basePosition + new Vector3(0f, 0.54f, -0.07f), new Vector3(0.46f, 0.48f, 0.12f), stone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Capsule, "Seated Buddha Silhouette", _arenaRoot, basePosition + new Vector3(0f, 0.40f, -0.20f), new Vector3(0.22f, 0.30f, 0.15f), stone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Buddha Head", _arenaRoot, basePosition + new Vector3(0f, 0.76f, -0.20f), Vector3.one * 0.15f, stone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, "Lotus Seat", _arenaRoot, basePosition + new Vector3(0f, 0.14f, -0.20f), new Vector3(0.31f, 0.06f, 0.24f), stone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Niche Halo", _arenaRoot, basePosition + new Vector3(0f, 0.63f, -0.14f), new Vector3(0.25f, 0.25f, 0.055f), gold);
        }

        private void BuildStairFlight(string name, int column, int lowerRow, int upperRow, Material stone)
        {
            Vector3 lower = GameDefinitions.CellLocalPosition(column, lowerRow);
            Vector3 upper = GameDefinitions.CellLocalPosition(column, upperRow);
            lower.y = GameDefinitions.CellSurfaceHeight(lowerRow);
            upper.y = GameDefinitions.CellSurfaceHeight(upperRow);
            const int stepCount = 5;
            float stepHeight = (upper.y - lower.y) / stepCount;
            for (int step = 0; step < stepCount; step++)
            {
                float t = (step + 0.5f) / stepCount;
                Vector3 position = Vector3.Lerp(lower, upper, t);
                position.y = lower.y + stepHeight * (step + 1) - 0.010f;
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"{name} {step + 1}", _arenaRoot, position,
                    new Vector3(GameDefinitions.CellSize * 0.82f, 0.022f + stepHeight, GameDefinitions.CellSize * 0.46f), stone);
            }
        }

        private void BuildPathPavers(Material stone, Material wood)
        {
            foreach (Vector2Int coordinates in GameDefinitions.BuildPathCellSet())
            {
                Vector3 position = GameDefinitions.CellLocalPosition(coordinates.x, coordinates.y);
                position.y = GameDefinitions.CellSurfaceHeight(coordinates.y) + 0.018f;
                Material material = coordinates.y <= 1 ? wood : stone;
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"Pilgrim Path Paver {coordinates.x},{coordinates.y}", _arenaRoot,
                    position, new Vector3(GameDefinitions.CellSize * 0.73f, 0.008f, GameDefinitions.CellSize * 0.73f), material);
            }
        }

        private void EnableAuthoredStairColliders()
        {
            // The two visible stair models are the walkable surface. Re-enable
            // their own mesh colliders after saved scenery is adopted; this
            // creates no replacement ramp or visible geometry.
            SceneModelAnchor[] anchors = FindObjectsByType<SceneModelAnchor>(FindObjectsInactive.Include);
            for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
            {
                SceneModelAnchor anchor = anchors[anchorIndex];
                if (anchor == null || anchor.ResourcePath != "GameplayModels/Scene_S05_StoneStairs")
                {
                    continue;
                }

                MeshFilter[] meshes = anchor.GetComponentsInChildren<MeshFilter>(true);
                for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
                {
                    MeshFilter meshFilter = meshes[meshIndex];
                    if (meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    MeshCollider stairCollider = meshFilter.GetComponent<MeshCollider>();
                    if (stairCollider == null)
                    {
                        stairCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    }
                    stairCollider.sharedMesh = meshFilter.sharedMesh;
                    stairCollider.convex = false;
                    stairCollider.isTrigger = false;
                    stairCollider.enabled = true;
                }
            }
        }

        private void BuildStoneBridge(Vector3 position, Material stone, Material wood)
        {
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Pilgrim Stone Bridge", _arenaRoot, position,
                new Vector3(GameDefinitions.CellSize * 1.04f, 0.036f, GameDefinitions.CellSize * 0.72f), stone);
            for (int side = -1; side <= 1; side += 2)
            {
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"Bridge Rail {side}", _arenaRoot,
                    position + new Vector3(0f, 0.075f, side * GameDefinitions.CellSize * 0.34f),
                    new Vector3(GameDefinitions.CellSize * 0.90f, 0.075f, 0.016f), wood);
            }
        }

        private void BuildRockCluster(string name, Vector3 center, Material darkStone, Material mossStone, float scale)
        {
            Vector3[] offsets = { new(-0.07f, 0f, -0.04f), new(0.08f, 0.008f, 0.03f), new(-0.02f, 0.018f, 0.09f), new(0.12f, 0.015f, -0.09f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                float size = (0.10f + i * 0.022f) * scale;
                ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"{name} {i + 1}", _arenaRoot, center + offsets[i] * scale,
                    new Vector3(size * 1.25f, size, size), i % 2 == 0 ? darkStone : mossStone,
                    Quaternion.Euler(12f * i, 29f * i, 18f * (i - 1)));
            }
        }

        private void BuildBodhiTree(string name, Vector3 position, Material wood, Material leaves, Material gold, float scale)
        {
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, $"{name} Trunk", _arenaRoot, position + new Vector3(0f, 0.14f * scale, 0f),
                new Vector3(0.035f * scale, 0.14f * scale, 0.035f * scale), wood);
            for (int branch = 0; branch < 6; branch++)
            {
                float angle = branch * Mathf.PI / 3f;
                Vector3 canopy = position + new Vector3(Mathf.Cos(angle) * 0.09f * scale, 0.28f * scale + (branch % 2) * 0.025f, Mathf.Sin(angle) * 0.09f * scale);
                ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"{name} Canopy {branch + 1}", _arenaRoot, canopy,
                    Vector3.one * (0.105f * scale), leaves);
            }
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"{name} Prayer Light", _arenaRoot, position + new Vector3(0f, 0.29f * scale, 0f),
                Vector3.one * (0.030f * scale), gold);
        }

        private void BuildBellOrDrumTower(string name, Vector3 position, bool drum, Material stone, Material wood, Material tile, Material accent)
        {
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, $"{name} Base", _arenaRoot, position,
                new Vector3(0.13f, 0.035f, 0.13f), stone);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"{name} Post {x},{z}", _arenaRoot,
                        position + new Vector3(x * 0.070f, 0.13f, z * 0.070f), new Vector3(0.020f, 0.24f, 0.020f), wood);
                }
            }
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"{name} Roof", _arenaRoot, position + new Vector3(0f, 0.27f, 0f),
                new Vector3(0.26f, 0.040f, 0.24f), tile, Quaternion.Euler(0f, 45f, 0f));
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, drum ? $"{name} Sacred Drum" : $"{name} Bronze Bell", _arenaRoot,
                position + new Vector3(0f, 0.14f, 0f), new Vector3(0.060f, 0.054f, 0.060f), accent, Quaternion.Euler(90f, 0f, 0f));
        }

        private void BuildStoneLantern(string name, Vector3 position, Material stone, Material wood, Material gold)
        {
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, $"{name} Base", _arenaRoot, position,
                new Vector3(0.035f, 0.022f, 0.035f), stone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cylinder, $"{name} Stem", _arenaRoot, position + new Vector3(0f, 0.075f, 0f),
                new Vector3(0.017f, 0.075f, 0.017f), wood);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"{name} Cap", _arenaRoot, position + new Vector3(0f, 0.145f, 0f),
                new Vector3(0.090f, 0.020f, 0.070f), stone);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"{name} Flame", _arenaRoot, position + new Vector3(0f, 0.120f, 0f),
                Vector3.one * 0.027f, gold);

            // Warm amber point light with gentle flicker — no shadows to
            // keep draw call budget within PICO 4 mobile VR limits.
            var lightObj = new GameObject($"{name} Glow");
            lightObj.layer = 2;
            lightObj.transform.SetParent(_arenaRoot, false);
            lightObj.transform.localPosition = position + new Vector3(0f, 0.126f, 0f);
            Light lanternLight = lightObj.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1f, 0.76f, 0.34f);
            lanternLight.range = 0.55f;
            lanternLight.intensity = 0.22f;
            lanternLight.shadows = LightShadows.None;
            // 默认画质为 Ultra（4 盏像素光），内置管线下像素点光会给范围内每个物体追加一次绘制；
            // 改为顶点光：保留暖光氛围，几乎不增加 Draw Call
            lanternLight.renderMode = LightRenderMode.ForceVertex;
            lightObj.AddComponent<LanternFlicker>();
        }

        private void BuildStonePagoda(Vector3 position, Material stone, Material tile, Material gold)
        {
            for (int level = 0; level < 3; level++)
            {
                float y = level * 0.105f;
                float width = 0.17f - level * 0.025f;
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"Sutra Pagoda Body {level + 1}", _arenaRoot,
                    position + new Vector3(0f, y + 0.040f, 0f), new Vector3(width, 0.080f, width), stone);
                ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, $"Sutra Pagoda Eave {level + 1}", _arenaRoot,
                    position + new Vector3(0f, y + 0.088f, 0f), new Vector3(width + 0.055f, 0.018f, width + 0.055f), tile, Quaternion.Euler(0f, 45f, 0f));
            }
            ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, "Sutra Pagoda Finial", _arenaRoot, position + new Vector3(0f, 0.35f, 0f),
                Vector3.one * 0.032f, gold);
        }

        private void BuildMountainBackdrop(Vector3 center, Material darkStone, Material mossStone)
        {
            Vector3[] offsets = { new(-0.38f, 0.17f, 0.02f), new(-0.12f, 0.25f, 0.03f), new(0.16f, 0.18f, 0.02f), new(0.42f, 0.12f, 0.04f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                float width = 0.34f - i * 0.018f;
                float height = 0.48f + (i % 2) * 0.14f;
                ProceduralFactory.VisualPrimitive(PrimitiveType.Sphere, $"Mountain Backdrop Peak {i + 1}", _arenaRoot, center + offsets[i],
                    new Vector3(width, height, 0.20f), i % 2 == 0 ? darkStone : mossStone,
                    Quaternion.Euler(0f, i * 17f, i % 2 == 0 ? -13f : 12f));
            }
        }

        private void BuildObjective()
        {
            EditableSceneModelLayout layout = GetComponent<EditableSceneModelLayout>();
            Transform temple = layout != null ? FindAuthoredProtectedStructure() : null;
            bool usesCustomProtectedStructure = temple != null;
            if (temple != null && !temple.IsChildOf(_arenaRoot))
            {
                temple.SetParent(_arenaRoot, true);
            }
            else if (temple == null && layout != null)
            {
                layout.TryGetBuiltEntry("Upper Main Temple", out temple);
            }

            // The saved scene may use a custom label for the main hall. The
            // Resources path identifies the player's authored temple without
            // adding, moving, or replacing any saved scene object.
            if (temple == null)
            {
                // Verification also creates isolated runtime smoke-test worlds;
                // they do not contain the saved scene anchor. In that case, use
                // a visible layout entry as the temple instead of emitting a
                // misleading warning or instantiating a substitute structure.
                if (!SceneModelAnchor.TryGetSavedModelForPath("GameplayModels/Scene_S08_MainTemple", out temple) && layout != null)
                {
                    IReadOnlyList<EditableSceneModelLayout.SceneModelEntry> entries = layout.Entries;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        EditableSceneModelLayout.SceneModelEntry entry = entries[i];
                        if (entry != null && entry.visible &&
                            string.Equals(entry.modelPath, "GameplayModels/Scene_S08_MainTemple", StringComparison.Ordinal))
                        {
                            temple = new GameObject("Protected Temple Reference").transform;
                            temple.gameObject.layer = 2;
                            temple.SetParent(_arenaRoot, false);
                            temple.localPosition = entry.position;
                            temple.localRotation = Quaternion.Euler(entry.rotation);
                            temple.localScale = Vector3.one;
                            break;
                        }
                    }
                }
            }

            // The protection target is the user's saved main temple. The old
            // implementation spawned a Frost tower at the path end, which
            // silently replaced the authored building and changed the layout.
            _protectedStructure = temple;
            Bounds structureBounds = default;
            bool hasStructureBounds = temple != null && TryCalculateWorldRendererBounds(temple, out structureBounds);

            Vector3 overlayPosition;
            if (hasStructureBounds)
            {
                overlayPosition = new Vector3(
                    usesCustomProtectedStructure ? structureBounds.center.x : temple.position.x,
                    structureBounds.max.y + 0.06f * _worldScale,
                    usesCustomProtectedStructure ? structureBounds.center.z : temple.position.z);
            }
            else
            {
                Vector2Int end = GameDefinitions.PathCorners[^1];
                overlayPosition = _arenaRoot.TransformPoint(new Vector3(
                    GameDefinitions.CellLocalPosition(end.x, end.y).x,
                    RouteTileSurfaceHeight(end.y) + 0.05f,
                    GameDefinitions.CellLocalPosition(end.x, end.y).z));
                Debug.Log("[Objective] No authored temple exists in this isolated smoke-test world; using the route endpoint as a temporary fallback marker.", this);
            }

            var root = new GameObject("Protected Main Temple Objective Overlay").transform;
            root.gameObject.layer = 2;
            root.SetParent(_arenaRoot, false);
            root.localPosition = _arenaRoot.InverseTransformPoint(overlayPosition);
            root.localScale = Vector3.one * 0.62f;
            AttachBillboard(root);

            _crystal = new GameObject("Temple Heart Lamp Purification Focus").transform;
            _crystal.gameObject.layer = 2;
            _crystal.SetParent(root, false);
            _crystal.localPosition = new Vector3(0f, 0.08f, 0f);

            Material bronzeRim = ProceduralFactory.CreateUnlitMaterial(new Color(0.62f, 0.43f, 0.18f));
            Material barBack = ProceduralFactory.CreateUnlitMaterial(new Color(0.035f, 0.055f, 0.050f));
            Material barFill = ProceduralFactory.CreateUnlitMaterial(new Color(0.24f, 0.78f, 0.42f));
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Temple Protection Bronze Rim", root, Vector3.zero, new Vector3(0.48f, 0.075f, 0.018f), bronzeRim);
            ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Temple Protection Bar Back", root, new Vector3(0f, 0f, -0.012f), new Vector3(0.44f, 0.050f, 0.016f), barBack);
            _baseHealthFill = ProceduralFactory.VisualPrimitive(PrimitiveType.Cube, "Temple Protection Strength", root, new Vector3(0f, 0f, -0.024f), new Vector3(0.40f, 0.030f, 0.018f), barFill).transform;

            Light glow = root.gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.64f, 0.16f);
            glow.range = 0.46f;
            glow.intensity = 0.18f;
            glow.shadows = LightShadows.None;
            // Pulse slowly to draw the eye toward the protected structure.
            root.gameObject.AddComponent<TempleHeartPulse>();
        }

        private static Transform FindAuthoredProtectedStructure()
        {
            GameObject target = GameObject.Find(ProtectedStructureObjectName);
            return target != null ? target.transform : null;
        }

        private static bool TryCalculateWorldRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found && bounds.size.sqrMagnitude > 0.0001f;
        }

        private void BuildSpatialControls()
        {
            _controlsRoot = PanelRoot("Spatial Arsenal UI", new Vector3(0f, 1.03f, -1.40f));
            const float width = 0.18f, height = 0.36f;
            for (int index = 0; index < 4; index++)
            {
                float x = (index - 1.5f) * 0.20f;
                GameObject art = GeneratedLotusPanel.Create(_controlsRoot, "Generated Arsenal Card " + index,
                    "Arsenal", new Vector2(width, height), new Vector3(x, 0f, 0f), index);
                art.layer = 0;
                BoxCollider collider = art.AddComponent<BoxCollider>();
                collider.size = new Vector3(1f, 1f, 0.02f);
                SpatialAction action = index == 0 ? SpatialAction.SelectArrow : index == 1 ? SpatialAction.SelectCannon :
                    index == 2 ? SpatialAction.SelectFrost : SpatialAction.StartWave;
                SpatialActionTarget target = art.AddComponent<SpatialActionTarget>();
                target.Initialize(action, Color.white);
                _actions.Add(target);
                if (index == 3)
                {
                    _waveArt = art.GetComponent<Renderer>();
                    // v4: 波次计数器从 y=-0.098 下移到 y=-0.155，避免与烘焙文字 "Start Wave [Left Y]" 重叠
                    _startDigits = GeneratedLotusPanel.Digits(_controlsRoot, new Vector3(x, -0.155f, -0.003f), 0.029f);
                }
            }
        }

        private Transform PanelRoot(string name, Vector3 localPosition)
        {
            Transform panel = new GameObject(name).transform;
            panel.gameObject.layer = 2;
            panel.SetParent(_arenaRoot, false);
            panel.localPosition = localPosition;
            AttachBillboard(panel);
            return panel;
        }

        private static Material CreateGeneratedCardMaterial(TowerKind kind)
        {
            return GeneratedLotusPanel.Material("Arsenal", (int)kind);
        }

        private static Material CreateGeneratedUiMaterial(string resourcePath, Vector2 cropScale, Vector2 cropOffset)
        {
            return CreateGeneratedUiMaterial(resourcePath, cropScale, cropOffset, "UI/GeneratedUiSurface");
        }

        private static Material CreateGeneratedOverlayUiMaterial(string resourcePath, Vector2 cropScale, Vector2 cropOffset)
        {
            return CreateGeneratedUiMaterial(resourcePath, cropScale, cropOffset, "UI/GeneratedUiOverlaySurface");
        }

        private static Material CreateGeneratedUiMaterial(
            string resourcePath,
            Vector2 cropScale,
            Vector2 cropOffset,
            string shaderResourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            Shader shader = Resources.Load<Shader>(shaderResourcePath) ?? Shader.Find("Unlit/Texture");
            if (texture == null || shader == null)
            {
                return ProceduralFactory.CreateMaterial(new Color(0.11f, 0.24f, 0.20f), 0.08f, 0.28f);
            }

            // Bilinear sampling with an inward crop prevents the pale source
            // matte from bleeding into the card edge at oblique XR angles.
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 16;
            texture.mipMapBias = -0.35f;
            texture.wrapMode = TextureWrapMode.Clamp;
            var material = new Material(shader)
            {
                color = Color.white,
                mainTexture = texture,
                mainTextureScale = cropScale,
                mainTextureOffset = cropOffset
            };
            return material;
        }

        private static void BuildLotusPanelFrame(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            Material bronze,
            Material jade,
            float depth)
        {
            float innerWidth = Mathf.Max(0.02f, size.x - 0.046f);
            float innerHeight = Mathf.Max(0.02f, size.y - 0.046f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                name + " Bronze Frame",
                parent,
                center,
                new Vector3(size.x, size.y, depth),
                bronze);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cube,
                name + " Jade Inlay",
                parent,
                center + new Vector3(0f, 0f, -depth * 0.55f),
                new Vector3(innerWidth, innerHeight, depth * 0.82f),
                jade);

            Vector3[] cornerOffsets =
            {
                new(-size.x * 0.43f, size.y * 0.43f, -depth * 1.05f),
                new(size.x * 0.43f, size.y * 0.43f, -depth * 1.05f),
                new(-size.x * 0.43f, -size.y * 0.43f, -depth * 1.05f),
                new(size.x * 0.43f, -size.y * 0.43f, -depth * 1.05f)
            };
            Vector3 ornamentScale = new(Mathf.Min(0.055f, size.x * 0.15f), Mathf.Min(0.034f, size.y * 0.12f), depth * 0.65f);
            for (int i = 0; i < cornerOffsets.Length; i++)
            {
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Cube,
                    name + $" Corner Ornament {i + 1}",
                    parent,
                    center + cornerOffsets[i],
                    ornamentScale,
                    bronze,
                    Quaternion.Euler(0f, 0f, i % 2 == 0 ? 32f : -32f));
            }
        }

        private void BuildStatusDisplay()
        {
            _statusHudRoot = PanelRoot("World Status HUD", new Vector3(0f, 1.90f, 0.26f));
            // 等比放大 ×1.333：0.90×0.30 → 1.20×0.40m，比例 3:1 与图片 2172×724 一致。
            GeneratedLotusPanel.Create(_statusHudRoot, "Generated Status HUD", "Status", new Vector2(1.20f, 0.40f), Vector3.zero);
            // 数字槽按 ×1.333 同比移动：X ±0.30→±0.40，Y -0.042→-0.056，高度相应放大。
            _goldDigits = GeneratedLotusPanel.Digits(_statusHudRoot, new Vector3(-0.40f, -0.056f, -0.003f), 0.073f);
            _lifeDigits = GeneratedLotusPanel.Digits(_statusHudRoot, new Vector3(0f, -0.056f, -0.003f), 0.067f);
            _waveDigits = GeneratedLotusPanel.Digits(_statusHudRoot, new Vector3(0.40f, -0.056f, -0.003f), 0.067f);
        }

        private void BuildWaveBriefingDisplay()
        {
            _waveBriefingRoot = PanelRoot("Next Wave Enemy Skill Briefing", new Vector3(-1.55f, 1.80f, -0.10f));
            // 等比放大 ×1.143：0.70×1.05 → 0.80×1.20m，比例 0.667:1 与图片 1024×1536 一致。
            // 扩大至 0.80×1.20 以满足 VR 可读性规格 C（d=2.79m，68px名字/50px描述）。
            GeneratedLotusPanel.Create(_waveBriefingRoot, "Generated Enemy Briefing", "Briefing", new Vector2(0.80f, 1.20f), Vector3.zero);
            // 波次数字：Y 按 ×1.143 缩放 0.335→0.383，高度 0.039→0.045。
            _briefingWaveDigits = GeneratedLotusPanel.Digits(_waveBriefingRoot, new Vector3(0f, 0.383f, -0.003f), 0.045f);
            float[] rows = { 0.275f, 0.387f, 0.498f, 0.610f, 0.723f, 0.839f };
            for (int i = 0; i < 6; i++)
                // 敌人计数：X ×1.143 0.1645→0.188，Y 乘数 1.05→1.20，高度 0.049→0.056。
                _enemyCountDigits[i] = GeneratedLotusPanel.Digits(_waveBriefingRoot,
                    new Vector3(0.188f, (0.5f - rows[i]) * 1.20f, -0.003f), 0.056f, 3);
        }

        private void BuildInteractionGuide()
        {
            // Move along the panel-to-camera line rather than a world axis.
            // The side panel therefore comes both inward and forward as the
            // viewer sees it, matching its camera-facing billboard normal.
            _interactionGuideRoot = PanelRoot("Two Hand World Interaction Guide", InteractionGuidePositionTowardViewer());
            // 等比放大 ×1.333：0.72×0.48 → 0.96×0.64m，比例 1.5:1 与图片 1536×1024 一致。
            GeneratedLotusPanel.Create(_interactionGuideRoot, "Generated Controller Guide", "Guide", new Vector2(0.96f, 0.64f), Vector3.zero);
        }

        private static Vector3 InteractionGuidePositionTowardViewer()
        {
            Vector3 directionToViewer = GameDefinitions.DesignPlayerEyeLocal - InteractionGuideBasePosition;
            return InteractionGuideBasePosition + directionToViewer.normalized * InteractionGuideForwardOffset;
        }

        private void OptimizeStaticPresentation()
        {
            var renderers = new HashSet<MeshRenderer>();
            foreach (SceneModelAnchor anchor in _arenaRoot.GetComponentsInChildren<SceneModelAnchor>(true))
                foreach (MeshRenderer renderer in anchor.GetComponentsInChildren<MeshRenderer>(true)) renderers.Add(renderer);
            Transform layout = _arenaRoot.Find("Editable Decorative Scene Models");
            if (layout != null)
                foreach (MeshRenderer renderer in layout.GetComponentsInChildren<MeshRenderer>(true)) renderers.Add(renderer);
            var batchObjects = new List<GameObject>();
            int shadowCasters = 0;
            long triangles = 0;
            foreach (MeshRenderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i) / 3;
                // The restored pond uses the authored depth-based water shader.
                // Transparent water must not cast a stippled shadow onto its bed.
                Material material = renderer.sharedMaterial;
                if (material != null && material.shader != null && material.shader.name == "SyntyStudios/WaterShader")
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    _input.Camera.depthTextureMode |= DepthTextureMode.Depth;
                }
                // Small ornaments contribute little visible shadow on headset,
                // but used to be rendered again into the shadow map every frame.
                if (renderer.bounds.size.magnitude < 0.32f) renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off) shadowCasters++;
                if (mesh.isReadable) batchObjects.Add(renderer.gameObject);
            }
            if (Application.isPlaying && batchObjects.Count > 1)
                StaticBatchingUtility.Combine(batchObjects.ToArray(), _arenaRoot.gameObject);
            Debug.Log($"[Lotus Performance] static renderers={renderers.Count}, triangles={triangles}, batch candidates={batchObjects.Count}, shadow casters={shadowCasters}, MSAA={QualitySettings.antiAliasing}, XR scale=1.");
        }

        private void NormalizeAuthoredArenaToMeters()
        {
            float conversion = 1f / GameDefinitions.AuthoredSceneScale;
            // Unity Terrain ignores Transform scale: resize a runtime copy of
            // its heightfield as well, never the user's TerrainData asset.
            foreach (Terrain terrain in _arenaRoot.GetComponentsInChildren<Terrain>(true))
            {
                if (terrain.terrainData == null) continue;
                TerrainData data = Instantiate(terrain.terrainData);
                data.name += " (Runtime Meters)";
                data.size *= conversion;
                _runtimeTerrainData.Add(data);
                terrain.terrainData = data;
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null) collider.terrainData = data;
            }
            _arenaRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _arenaRoot.localScale = Vector3.one;
            Debug.Log($"[Lotus Walkaround] Authored island converted to meters. Board width={GameDefinitions.GridColumns * GameDefinitions.CellSize:F3}m; XR tracking scale remains 1.");
        }

        private void EnsureArenaWorldAnchor()
        {
            if (_arenaRoot == null || _input == null)
            {
                return;
            }

            bool wrongParent = _arenaRoot.parent != null;
            bool moved = Vector3.Distance(_arenaRoot.position, _arenaWorldPositionAtBuild) > 0.0001f ||
                         Quaternion.Angle(_arenaRoot.rotation, _arenaWorldRotationAtBuild) > 0.001f ||
                         Vector3.Distance(_arenaRoot.lossyScale, _arenaWorldScaleAtBuild) > 0.0001f;
            if (wrongParent || moved)
            {
                Debug.LogError("[Yi Nian Lotus City] Arena world anchor moved; restoring the authored room-space pose.", this);
                _arenaRoot.SetParent(null, true);
                _arenaRoot.SetPositionAndRotation(_arenaWorldPositionAtBuild, _arenaWorldRotationAtBuild);
                _arenaRoot.localScale = _arenaWorldScaleAtBuild;
            }
        }

        private static void ConfigureOverlayRenderer(GameObject panel)
        {
            Renderer renderer = panel.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sortingOrder = 0;
        }

        private void BuildRangeRing()
        {
            var ringObject = new GameObject("Tower Range Preview");
            ringObject.layer = 2;
            ringObject.transform.SetParent(_arenaRoot, false);
            _rangeRing = ringObject.AddComponent<LineRenderer>();
            _rangeRing.useWorldSpace = false;
            _rangeRing.loop = true;
            _rangeRing.positionCount = 64;
            _rangeRing.startWidth = 0.010f;
            _rangeRing.endWidth = 0.010f;
            _rangeRing.material = new Material(Shader.Find("Sprites/Default"));
            _rangeRing.enabled = false;
        }

        private void BuildTowerHoverDisplay()
        {
            _towerInfoRoot = PanelRoot("Tower Hover Information Panel", Vector3.zero);
            // 等比放大 ×1.333：0.66×0.33 → 0.88×0.44m，比例 2:1 与图片 1774×887 一致。
            GeneratedLotusPanel.Create(_towerInfoRoot, "Generated Tower Detail", "Stats", new Vector2(0.88f, 0.44f), Vector3.zero);
            float[] columns = { 0.15f, 0.38f, 0.61f, 0.84f };
            for (int i = 0; i < 4; i++)
                // X 乘数 0.66→0.88，Y -0.033→-0.044，高度 0.050→0.056。
                _towerDigits[i] = GeneratedLotusPanel.Digits(_towerInfoRoot,
                    new Vector3((columns[i] - 0.5f) * 0.88f, -0.044f, -0.003f), 0.056f, 5);
            _towerInfoRoot.gameObject.SetActive(false);
        }

        private void UpdateTowerHover(TowerAgent tower)
        {
            if (tower == null || _towerInfoRoot == null)
            {
                HideTowerHover();
                return;
            }

            _hoveredInfoTower = tower;
            SetTowerHoverText(tower.Kind, tower.Level);

            if (_towerInfoRoot.parent != _arenaRoot)
            {
                _towerInfoRoot.SetParent(_arenaRoot, false);
                _towerInfoRoot.localScale = Vector3.one;
            }
            BillboardText towerBillboard = _towerInfoRoot.GetComponent<BillboardText>();
            if (towerBillboard != null)
            {
                towerBillboard.enabled = true;
            }

            // Keep the compact detail panel just above and slightly to the side
            // of the hovered tower so it stays readable without covering it.
            Vector3 panelPosition = tower.Root.transform.localPosition;
            panelPosition.x += 0.22f;
            panelPosition.y += 0.34f;
            _towerInfoRoot.localPosition = panelPosition;
            _towerInfoRoot.gameObject.SetActive(true);
        }

        private void UpdateTowerCardHover(TowerKind kind, Transform card)
        {
            if (_towerInfoRoot == null || card == null)
            {
                HideTowerHover();
                return;
            }

            _hoveredInfoTower = null;
            SetTowerHoverText(kind, 1);

            // Keep the detail panel world-anchored above the arsenal. Its own
            // billboard faces the viewer, independent of the arsenal's tilt.
            Transform controlsRoot = card.parent;
            _towerInfoRoot.SetParent(_arenaRoot, false);
            _towerInfoRoot.position = controlsRoot.position + Vector3.up * 0.40f;
            _towerInfoRoot.localScale = Vector3.one;
            BillboardText cardBillboard = _towerInfoRoot.GetComponent<BillboardText>();
            if (cardBillboard != null)
            {
                cardBillboard.enabled = true;
                cardBillboard.FaceTarget();
            }
            _towerInfoRoot.gameObject.SetActive(true);
        }

        private void SetTowerHoverText(TowerKind kind, int level)
        {
            TowerDefinition definition = GameDefinitions.Tower(kind, level);
            _towerDigits[0]?.Set(level.ToString());
            _towerDigits[1]?.Set(definition.Damage.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            _towerDigits[2]?.Set(definition.FireRate.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            _towerDigits[3]?.Set((definition.Range / GameDefinitions.SpatialScale).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string LocalizedTowerName(TowerKind kind)
        {
            return kind switch
            {
                TowerKind.Cannon => "Prayer Wheel Guardian",
                TowerKind.Frost => "Lotus Lamp",
                _ => "Morning Bell"
            };
        }

        private static string TowerRoleDescription(TowerKind kind)
        {
            return kind switch
            {
                TowerKind.Cannon => "Heavy Purify",
                TowerKind.Frost => "Frost Control",
                _ => "Area Purification"
            };
        }

        private void HideTowerHover()
        {
            _hoveredInfoTower = null;
            if (_towerInfoRoot != null)
            {
                _towerInfoRoot.gameObject.SetActive(false);
            }
        }

        private void BuildAudio()
        {
            _audioSource = _arenaRoot.gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.78f;
            _audioSource.minDistance = 0.4f * _worldScale;
            _audioSource.maxDistance = 8f * _worldScale;
            _audioClips["build"] = LoadOrTone("Audio/TowerPlace", "Build", 440f, 700f, 0.11f);
            _audioClips["upgrade"] = LoadOrTone("Audio/TowerUpgrade", "Upgrade", 520f, 980f, 0.22f);
            _audioClips["wave"] = LoadOrTone("Audio/WaveStart", "Wave", 210f, 430f, 0.18f);
            _audioClips["arrow"] = LoadOrTone("Audio/TempleBell", "Temple Bell", 410f, 205f, 0.22f);
            _audioClips["cannon"] = LoadOrTone("Audio/DharmaWheel", "Dharma Wheel", 180f, 480f, 0.16f);
            _audioClips["frost"] = LoadOrTone("Audio/IncensePulse", "Incense Pulse", 690f, 940f, 0.14f);
            _audioClips["player"] = LoadOrTone("Audio/KeeperPurification", "Keeper Purification", 920f, 1380f, 0.10f);
            _audioClips["invalid"] = CreateTone("Invalid", 180f, 110f, 0.10f);
            _audioClips["win"] = LoadOrTone("Audio/Victory", "Victory", 520f, 920f, 0.38f);
            _audioClips["lose"] = CreateTone("Defeat", 220f, 75f, 0.42f);

            AudioClip music = Resources.Load<AudioClip>("Audio/LotusCityMusic");
            music ??= CreateAmbientMusic();
            _musicSource = _arenaRoot.gameObject.AddComponent<AudioSource>();
            _musicSource.clip = music;
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.spatialBlend = 0f;
            _musicSource.volume = MenuMusicVolume;
            _musicSource.Play();
        }

        private void BuildStartupCover()
        {
            // This is a complete standalone title stage. GameplayRoot is
            // inactive while it exists, so no island, HUD, health bar, enemy,
            // collider or game audio can show through the Figma source frame.
            const float coverHeight = 3.60f;
            const float coverWidth = coverHeight * (16f / 9f);
            _startupCoverRoot = new GameObject("Independent World Space Yi Nian Lotus City Title Root").transform;
            _startupCoverRoot.SetParent(transform, false);
            _startupCoverRoot.localPosition = GameDefinitions.TitlePanelLocalPosition;
            _startupCoverRoot.localRotation = Quaternion.Euler(0f, GameDefinitions.TitlePanelFacingYaw, 0f);
            _startupCoverRoot.localScale = Vector3.one;

            _startupCoverMaterial = CreateStartupCoverMaterial();
            // Use a double-sided unlit material in world space: the shrine
            // stays visible when the player approaches or circles the table,
            // while it remains an independent scene object rather than a HUD.
            GameObject backdrop = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Quad,
                "Figma Lotus City World Space Start Cover",
                _startupCoverRoot,
                Vector3.zero,
                new Vector3(coverWidth, coverHeight, 1f),
                _startupCoverMaterial);
            ConfigureOverlayRenderer(backdrop);
            _startupCoverRenderer = backdrop.GetComponent<Renderer>();

            _startupCoverStateMaterial = CreateStartupCoverStateMaterial();
            GameObject stateBackdrop = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Quad,
                "Figma Lotus City World Space Start Interaction State",
                _startupCoverRoot,
                new Vector3(0f, 0f, -0.004f),
                new Vector3(coverWidth, coverHeight, 1f),
                _startupCoverStateMaterial);
            ConfigureOverlayRenderer(stateBackdrop);
            _startupCoverStateRenderer = stateBackdrop.GetComponent<Renderer>();
            _startupCoverStateRenderer.enabled = false;

            // The Figma Start button occupies roughly x=0.20..0.32 and
            // y=0.53..0.62 of the 16:9 frame. Its renderer stays hidden; the
            // cover artwork remains the single visual source of truth.
            GameObject startButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
            startButton.name = "Figma Lotus City World Space Start Hit Area";
            startButton.transform.SetParent(_startupCoverRoot, false);
            startButton.transform.localPosition = new Vector3(-coverWidth * 0.28f, -coverHeight * 0.074f, -0.018f);
            startButton.transform.localScale = new Vector3(coverWidth * 0.20f, coverHeight * 0.12f, 0.025f);
            Renderer startRenderer = startButton.GetComponent<Renderer>();
            if (startRenderer != null)
            {
                startRenderer.enabled = false;
            }
            SpatialActionTarget startTarget = startButton.AddComponent<SpatialActionTarget>();
            startTarget.Initialize(SpatialAction.StartExperience, new Color(0.83f, 0.66f, 0.30f));
            _actions.Add(startTarget);
            SetStartupCoverVisualAlpha(1f, 0f);
        }

        private static Material CreateStartupCoverMaterial()
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/Startup/LotusCityStartCover");
            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Texture");
            if (texture == null || shader == null)
            {
                return ProceduralFactory.CreateUnlitMaterial(new Color(0.86f, 0.84f, 0.74f));
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var material = new Material(shader)
            {
                color = Color.white,
                mainTexture = texture,
                renderQueue = 3000
            };
            material.SetInt("_Cull", (int)CullMode.Back);
            return material;
        }

        private static Material CreateStartupCoverStateMaterial()
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/Startup/LotusCityStartCoverPressed");
            if (texture == null)
            {
                texture = Resources.Load<Texture2D>("UI/Startup/LotusCityStartCover");
            }

            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Texture");
            if (texture == null || shader == null)
            {
                return ProceduralFactory.CreateUnlitMaterial(Color.white);
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var material = new Material(shader)
            {
                color = Color.white,
                mainTexture = texture,
                renderQueue = 3000
            };
            material.SetInt("_Cull", (int)CullMode.Back);
            return material;
        }

        private void UpdateStartupCover()
        {
            _startupCoverClock += Time.deltaTime;
            _startupCoverPressFeedback = Mathf.Max(0f, _startupCoverPressFeedback - Time.unscaledDeltaTime * 6.5f);
            if (_startupCoverRoot == null || _startupCoverTransitioning)
            {
                return;
            }

            Ray ray = _input.AimRay();
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 8f * _worldScale, ~(1 << 2));
            SpatialActionTarget action = hit ? hitInfo.collider.GetComponent<SpatialActionTarget>() : null;
            _hoveredAction = action != null && action.Action == SpatialAction.StartExperience ? action : null;
            bool releasedThisFrame = _input.ConsumeConfirmUp(out bool releasedPointerDrag);
            if (!_titleInputHasSettled)
            {
                // A new desktop window can inherit the click that focused it.
                // Consume that entire gesture before allowing a deliberate
                // title activation, including the equivalent controller state.
                _input.ConsumeConfirmDown();
                if (!_input.IsInteractionConfirmHeld)
                {
                    _titleInputHasSettled = true;
                }
                _startupCoverPressArmed = false;
                UpdateStartupCoverFeedback();
                RefreshActionVisuals();
                _input.SetAimVisual(hit ? hitInfo.point : ray.origin + ray.direction * (4f * _worldScale), hit);
                return;
            }

            bool titleInputReady = Time.unscaledTime >= _titleInputReadyAt;
            if (_input.ConsumeConfirmDown() && titleInputReady)
            {
                _startupCoverPressArmed = _hoveredAction != null;
                if (_startupCoverPressArmed)
                {
                    _startupCoverPressFeedback = 1f;
                }
            }
            UpdateStartupCoverFeedback();
            RefreshActionVisuals();
            _input.SetAimVisual(hit ? hitInfo.point : ray.origin + ray.direction * (4f * _worldScale), hit);

            if (releasedThisFrame)
            {
                bool activate = titleInputReady && _startupCoverPressArmed && !releasedPointerDrag && _hoveredAction != null;
                _startupCoverPressArmed = false;
                if (activate)
                {
                    StartCoroutine(AnimateFigmaStartTransition());
                }
            }
        }

        private void UpdateStartupCoverFeedback()
        {
            bool hovered = _hoveredAction != null;
            float target = _startupCoverPressFeedback > 0f ? 1f : hovered ? 0.52f : 0f;
            float spring = 1f - Mathf.Exp(-Time.unscaledDeltaTime *
                (target > _startupCoverStateAlpha ? 19f : 14f));
            _startupCoverStateAlpha = Mathf.Lerp(_startupCoverStateAlpha, target, spring);
            float stateAlpha = Mathf.Clamp01(_startupCoverStateAlpha);
            if (_startupCoverStateMaterial != null)
            {
                Color color = Color.white;
                color.a = stateAlpha;
                _startupCoverStateMaterial.color = color;
                if (_startupCoverStateMaterial.HasProperty("_Color"))
                {
                    _startupCoverStateMaterial.SetColor("_Color", color);
                }
                if (_startupCoverStateMaterial.HasProperty("_BaseColor"))
                {
                    _startupCoverStateMaterial.SetColor("_BaseColor", color);
                }
            }
            if (_startupCoverStateRenderer != null)
            {
                _startupCoverStateRenderer.enabled = stateAlpha > 0.002f;
            }
        }

        private IEnumerator AnimateFigmaStartTransition()
        {
            if (_startupCoverTransitioning)
            {
                yield break;
            }

            _startupCoverTransitioning = true;
            _hoveredAction = null;
            float elapsed = 0f;
            const float duration = 0.34f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                // Figma prototype: Smart Animate / Spring / mass 1 / stiffness
                // 711.1 / damping 40. The interaction switches only between
                // exported Figma frames; no runtime highlight or recoloured
                // substitute is introduced here.
                float stateAlpha = FigmaSpringProgress(elapsed);
                float coverAlpha = 1f - stateAlpha;
                SetStartupCoverVisualAlpha(coverAlpha, stateAlpha);
                yield return null;
            }

            SetStartupCoverVisualAlpha(0f, 0f);
            StartExperience();
        }

        private static float FigmaSpringProgress(float time)
        {
            const float mass = 1f;
            const float stiffness = 711.1f;
            const float damping = 40f;
            float naturalFrequency = Mathf.Sqrt(stiffness / mass);
            float dampingRatio = damping / (2f * Mathf.Sqrt(stiffness * mass));
            float exponential = Mathf.Exp(-dampingRatio * naturalFrequency * time);
            if (dampingRatio < 0.999f)
            {
                float dampedFrequency = naturalFrequency * Mathf.Sqrt(1f - dampingRatio * dampingRatio);
                float response = 1f - exponential *
                    (Mathf.Cos(dampedFrequency * time) +
                     dampingRatio * naturalFrequency / dampedFrequency * Mathf.Sin(dampedFrequency * time));
                return Mathf.Clamp01(response);
            }

            return Mathf.Clamp01(1f - exponential * (1f + naturalFrequency * time));
        }

        private void SetStartupCoverVisualAlpha(float coverAlpha, float stateAlpha)
        {
            coverAlpha = Mathf.Clamp01(coverAlpha);
            stateAlpha = Mathf.Clamp01(stateAlpha);
            if (_startupCoverMaterial != null)
            {
                Color coverColor = Color.white;
                coverColor.a = coverAlpha;
                _startupCoverMaterial.color = coverColor;
                if (_startupCoverMaterial.HasProperty("_Color"))
                {
                    _startupCoverMaterial.SetColor("_Color", coverColor);
                }
            }
            if (_startupCoverStateMaterial != null)
            {
                Color stateColor = Color.white;
                stateColor.a = Mathf.Clamp01(stateAlpha);
                _startupCoverStateMaterial.color = stateColor;
                if (_startupCoverStateMaterial.HasProperty("_Color"))
                {
                    _startupCoverStateMaterial.SetColor("_Color", stateColor);
                }
            }
            if (_startupCoverRenderer != null)
            {
                _startupCoverRenderer.enabled = coverAlpha > 0.002f;
            }
            if (_startupCoverStateRenderer != null)
            {
                _startupCoverStateRenderer.enabled = stateAlpha > 0.002f;
            }
        }

        private void StartExperience()
        {
            if (_experienceStarted)
            {
                return;
            }

            _experienceStarted = true;
            Debug.Log("[Yi Nian Lotus City] Start activated: world-space shrine cleared; tabletop gameplay is now visible.");
            _hoveredAction = null;
            _startupCoverPressFeedback = 0f;
            _startupCoverStateAlpha = 0f;
            _startupCoverPressArmed = false;
            _startupCoverTransitioning = false;
            _startupCoverRenderer = null;
            _startupCoverStateRenderer = null;
            _startupCoverMaterial = null;
            _startupCoverStateMaterial = null;
            _actions.RemoveAll(action => action != null && action.Action == SpatialAction.StartExperience);
            if (_startupCoverRoot != null)
            {
                GameObject coverObject = _startupCoverRoot.gameObject;
                _startupCoverRoot = null;
                if (Application.isPlaying)
                {
                    Destroy(coverObject);
                }
                else
                {
                    DestroyImmediate(coverObject);
                }
            }
            SetGameplayInterfaceVisible(true);
            ConfigureGameplayStagePlayerStart();
            if (_musicSource != null)
            {
                _musicSource.volume = GameMusicVolume;
            }
            Play("build", 0.42f);
            _input.Pulse(0.38f, 0.06f);
            RefreshStateDisplay();
        }

        private void SetGameplayInterfaceVisible(bool visible)
        {
            if (_controlsRoot != null) _controlsRoot.gameObject.SetActive(visible);
            if (_statusHudRoot != null) _statusHudRoot.gameObject.SetActive(visible);
            if (_waveBriefingRoot != null) _waveBriefingRoot.gameObject.SetActive(visible);
            if (_interactionGuideRoot != null) _interactionGuideRoot.gameObject.SetActive(visible && _waveIndex == 0);
            if (_towerInfoRoot != null) _towerInfoRoot.gameObject.SetActive(false);
            if (_rangeRing != null) _rangeRing.enabled = false;
        }

        private void UpdateInteraction()
        {
            Ray ray = _input.AimRay();
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, 8f * _worldScale, ~(1 << 2));
            _input.SetAimVisual(
                hitSomething ? hit.point : ray.origin + ray.direction * (4f * _worldScale),
                hitSomething);

            BoardCell nextCell = null;
            SpatialActionTarget nextAction = null;
            TowerAgent nextTower = null;
            if (hitSomething)
            {
                nextCell = hit.collider.GetComponent<BoardCell>();
                nextAction = hit.collider.GetComponent<SpatialActionTarget>();
                TowerMergeTarget towerTarget = hit.collider.GetComponent<TowerMergeTarget>();
                nextTower = towerTarget != null ? towerTarget.Agent : null;
            }

            if (_draggedTower != null)
            {
                _hoveredAction = null;
                HideTowerHover();
                _rangeRing.enabled = false;
                UpdateDraggedTower(ray);

                // A tower drop should be forgiving in world space. Once the dragged
                // model is close to another tower, use that model as the target even
                // when the pointer ray is no longer exactly over its collider.
                TowerAgent nearbyTower = FindNearbyTower(_draggedTower.Root.transform.position, _draggedTower);
                if (nearbyTower != null)
                {
                    nextTower = nearbyTower;
                    _draggedTower.SnapDragPreviewTo(nearbyTower.Root.transform.position);
                }

                if (_hoveredMergeTower != nextTower)
                {
                    _hoveredMergeTower?.SetMergeHighlight(false, false);
                    _hoveredMergeTower = nextTower;
                }
                _hoveredMergeTower?.SetMergeHighlight(true, CanMerge(_draggedTower, _hoveredMergeTower));

                if (_hoveredCell != nextCell)
                {
                    _hoveredCell?.SetHovered(false, false);
                    _hoveredCell = nextCell;
                }
                _hoveredCell?.SetHovered(true, CanRelocate(_draggedTower, _hoveredCell));
            }
            else if (_draggedCardGhost != null)
            {
                _hoveredAction = null;
                HideTowerHover();
                _rangeRing.enabled = false;
                UpdateDragTransform(_draggedCardGhost, ray);

                TowerAgent nearbyTower = FindNearbyTower(_draggedCardGhost.position, null);
                if (nearbyTower != null)
                {
                    nextTower = nearbyTower;
                    SnapCardDragPreviewTo(nearbyTower.Root.transform.position);
                }

                if (_hoveredMergeTower != nextTower)
                {
                    _hoveredMergeTower?.SetMergeHighlight(false, false);
                    _hoveredMergeTower = nextTower;
                }
                _hoveredMergeTower?.SetMergeHighlight(true, CanUseBuildCardOnTower(_draggedCardKind, _hoveredMergeTower));

                if (_hoveredCell != nextCell)
                {
                    _hoveredCell?.SetHovered(false, false);
                    _hoveredCell = nextCell;
                }
                _hoveredCell?.SetHovered(true, _hoveredCell != null && CanBuild(_hoveredCell, _draggedCardKind));
            }
            else
            {
                _hoveredMergeTower?.SetMergeHighlight(false, false);
                _hoveredMergeTower = null;
                if (_hoveredCell != nextCell)
                {
                    _hoveredCell?.SetHovered(false, false);
                    _hoveredCell = nextCell;
                }

                _hoveredAction = nextAction;
                bool cellValid = _hoveredCell != null && CanBuild(_hoveredCell, _selectedTower);
                _hoveredCell?.SetHovered(true, cellValid);
                if (nextTower != null)
                {
                    UpdateTowerHover(nextTower);
                    UpdateRangePreview(nextTower);
                }
                else if (nextAction != null && TryGetTowerCardKind(nextAction.Action, out TowerKind hoveredCardKind))
                {
                    UpdateTowerCardHover(hoveredCardKind, nextAction.transform);
                    _rangeRing.enabled = false;
                }
                else
                {
                    HideTowerHover();
                    UpdateRangePreview(_hoveredCell, cellValid);
                }
            }
            RefreshActionVisuals();

            if (_input.ConsumeConfirmDown() && _draggedTower == null && _draggedCardGhost == null)
            {
                if (nextTower != null)
                {
                    BeginTowerDrag(nextTower);
                }
                else if (nextAction != null && TryGetTowerCardKind(nextAction.Action, out TowerKind cardKind))
                {
                    BeginBuildCardDrag(nextAction, cardKind);
                }
            }

            if (!_input.ConsumeConfirmUp(out bool wasPointerDrag))
            {
                return;
            }

            if (_draggedTower != null)
            {
                FinishTowerDrag(nextCell);
            }
            else if (_draggedCardGhost != null)
            {
                FinishBuildCardDrag(nextCell, nextTower, nextAction, wasPointerDrag);
            }
            else if (!wasPointerDrag)
            {
                if (_hoveredAction != null)
                {
                    ExecuteAction(_hoveredAction.Action);
                }
                else if (_hoveredCell != null)
                {
                    TryPlaceTower(_hoveredCell);
                }
            }
        }

        private void UpdatePlayerWeapon(float deltaTime)
        {
            _playerWeaponCooldown = Mathf.Max(0f, _playerWeaponCooldown - deltaTime);
            _playerWeaponTargetGrace = Mathf.Max(0f, _playerWeaponTargetGrace - deltaTime);
            Ray ray = _input.AttackRay();
            bool attackPressed = _input.IsAttackPressed;
            EnemyAgent target = FindPlayerWeaponTarget(ray);
            if (target != null)
            {
                _lockedPlayerWeaponTarget = target;
                _playerWeaponTargetGrace = PlayerWeaponTargetGrace;
            }
            else if (attackPressed && _playerWeaponTargetGrace > 0f && IsPlayerWeaponTargetValid(_lockedPlayerWeaponTarget, ray.origin))
            {
                target = _lockedPlayerWeaponTarget;
            }
            else
            {
                _lockedPlayerWeaponTarget = null;
                _playerWeaponTargetGrace = 0f;
            }

            Vector3 endPoint = target != null
                ? target.Position + Vector3.up * (target.Definition.Radius * _worldScale)
                : ray.origin + ray.direction * (3.5f * _worldScale);
            _input.SetAttackVisual(endPoint, target != null);

            if (!attackPressed || _gameOver || _playerWeaponCooldown > 0f)
            {
                return;
            }

            FirePlayerWeapon(target, true);
        }

        private EnemyAgent FindPlayerWeaponTarget(Ray ray)
        {
            EnemyAgent best = null;
            float nearestDistance = GameDefinitions.PlayerWeaponRange * _worldScale;
            Vector3 direction = ray.direction.normalized;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyAgent enemy = _enemies[i];
                if (!enemy.IsValid || enemy.IsDead || enemy.ReachedEnd || !enemy.HasReachedWeaponGate)
                {
                    continue;
                }

                Vector3 targetPoint = enemy.Position + Vector3.up * (enemy.Definition.Radius * _worldScale);
                Vector3 toTarget = targetPoint - ray.origin;
                float distanceAlongRay = Vector3.Dot(toTarget, direction);
                if (distanceAlongRay <= 0f || distanceAlongRay > nearestDistance)
                {
                    continue;
                }

                // Comfortable room-scale aiming needs a larger target than the small visual meshes.
                float aimAssistRadius = Mathf.Max(
                    enemy.Definition.Radius * 2.6f,
                    0.12f * GameDefinitions.SpatialScale) * _worldScale;
                Vector3 closestPoint = ray.origin + direction * distanceAlongRay;
                if ((targetPoint - closestPoint).sqrMagnitude > aimAssistRadius * aimAssistRadius)
                {
                    continue;
                }

                best = enemy;
                nearestDistance = distanceAlongRay;
            }

            return best;
        }

        private bool IsPlayerWeaponTargetValid(EnemyAgent target, Vector3 weaponOrigin)
        {
            return target != null && target.IsValid && target.HasReachedWeaponGate && !target.IsDead && !target.ReachedEnd &&
                   (target.Position - weaponOrigin).sqrMagnitude <=
                   Mathf.Pow(GameDefinitions.PlayerWeaponRange * _worldScale, 2f);
        }

        private bool FirePlayerWeapon(EnemyAgent target, bool feedback)
        {
            if (target == null || !target.IsValid || !target.HasReachedWeaponGate || target.IsDead || target.ReachedEnd)
            {
                return false;
            }

            target.ApplyDamage(GameDefinitions.PlayerWeaponDamage);
            _playerWeaponCooldown = 1f / GameDefinitions.PlayerWeaponFireRate;
            if (feedback)
            {
                CombatVisualEffects.SpawnKeeperImpact(
                    target.Position + Vector3.up * (target.Definition.Radius * 0.65f * _worldScale),
                    _projectilesRoot);
                Play("player", 0.42f);
                _input.PulseAttack(0.28f, 0.045f);
            }
            return true;
        }

        private void ExecuteAction(SpatialAction action)
        {
            switch (action)
            {
                case SpatialAction.StartExperience:
                    StartExperience();
                    return;
                case SpatialAction.SelectArrow:
                    _selectedTower = TowerKind.Arrow;
                    Play("build", 0.35f);
                    break;
                case SpatialAction.SelectCannon:
                    _selectedTower = TowerKind.Cannon;
                    Play("build", 0.35f);
                    break;
                case SpatialAction.SelectFrost:
                    _selectedTower = TowerKind.Frost;
                    Play("build", 0.35f);
                    break;
                case SpatialAction.StartWave:
                    if (_gameOver)
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene(LotusTitleStage.GameplaySceneName);
                        return;
                    }
                    if (StartNextWave())
                    {
                        Play("wave", 0.65f);
                    }
                    else
                    {
                        Play("invalid", 0.3f);
                    }
                    break;
                case SpatialAction.RecenterArena:
                    ResetGameplayXrRigToDesignStart();
                    break;
            }

            _input.Pulse();
            RefreshStateDisplay();
        }

        private void TryPlaceTower(BoardCell cell)
        {
            if (!CanBuild(cell, _selectedTower))
            {
                Play("invalid", 0.35f);
                _input.Pulse(0.12f, 0.03f);
                return;
            }

            TowerDefinition definition = GameDefinitions.Tower(_selectedTower);
            _gold -= definition.Cost;
            PlaceTowerAtCell(_selectedTower, cell.Coordinates, true);
            Play("build", 0.6f);
            _input.Pulse(0.45f, 0.08f);
            RefreshStateDisplay();
        }

        private TowerAgent PlaceTowerAtCell(TowerKind kind, Vector2Int coordinates, bool markOccupied)
        {
            BoardCell cell = _cells[coordinates];
            if (markOccupied)
            {
                cell.IsOccupied = true;
            }

            Vector3 position = CellTowerLocalPosition(coordinates);
            var tower = new TowerAgent(kind, coordinates, position, _actorsRoot, _pathLocal);
            _towers.Add(tower);
            return tower;
        }

        private void BeginTowerDrag(TowerAgent tower)
        {
            _draggedTower = tower;
            _draggedTower.BeginDrag();
            _input.SetInteractionDragActive(true);
            _input.Pulse(0.20f, 0.035f);
        }

        private void UpdateDraggedTower(Ray ray)
        {
            UpdateDragTransform(_draggedTower.Root.transform, ray);
        }

        private TowerAgent FindNearbyTower(Vector3 sourcePosition, TowerAgent excludedTower)
        {
            TowerAgent nearest = null;
            float nearestDistance = WorldTowerMergeSnapRadius;
            Vector2 sourcePlanarPosition = new(sourcePosition.x, sourcePosition.z);
            for (int i = 0; i < _towers.Count; i++)
            {
                TowerAgent candidate = _towers[i];
                if (candidate == null || candidate == excludedTower)
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.Root.transform.position;
                float distance = Vector2.Distance(sourcePlanarPosition, new Vector2(candidatePosition.x, candidatePosition.z));
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void SnapCardDragPreviewTo(Vector3 worldPosition)
        {
            Vector3 local = _draggedCardGhost.parent.InverseTransformPoint(worldPosition);
            local.y += 0.15f;
            _draggedCardGhost.localPosition = local;
        }

        private void UpdateDragTransform(Transform draggedTransform, Ray ray)
        {
            Vector3 planePoint = _arenaRoot.TransformPoint(new Vector3(0f, GameDefinitions.TableHeight, 0f));
            var tablePlane = new Plane(_arenaRoot.up, planePoint);
            if (tablePlane.Raycast(ray, out float distance))
            {
                if (_draggedTower != null && draggedTransform == _draggedTower.Root.transform)
                {
                    _draggedTower.DragToWorld(ray.GetPoint(distance));
                }
                else
                {
                    Vector3 local = draggedTransform.parent.InverseTransformPoint(ray.GetPoint(distance));
                    local.y = GameDefinitions.TableHeight + 0.15f;
                    draggedTransform.localPosition = local;
                }
            }
        }

        private void FinishTowerDrag(BoardCell relocationCell)
        {
            CompleteTowerDrag(true, relocationCell);
        }

        private bool CompleteTowerDrag(bool feedback, BoardCell relocationCell)
        {
            TowerAgent source = _draggedTower;
            TowerAgent target = _hoveredMergeTower;
            bool merged = TryMergeTowers(source, target, feedback);
            bool returnedHome = !merged && relocationCell != null && relocationCell.Coordinates == source.Coordinates;
            bool moved = !merged && !returnedHome && CanRelocate(source, relocationCell);
            if (moved)
            {
                _cells[source.Coordinates].IsOccupied = false;
                relocationCell.IsOccupied = true;
                source.MoveTo(relocationCell.Coordinates, CellTowerLocalPosition(relocationCell.Coordinates));
                if (feedback)
                {
                    Play("build", 0.48f);
                    _input.Pulse(0.32f, 0.06f);
                }
            }
            else if (returnedHome)
            {
                source.EndDrag(true);
            }
            else if (!merged)
            {
                source.EndDrag(true);
                if (feedback)
                {
                    Play("invalid", 0.28f);
                    _input.Pulse(0.12f, 0.03f);
                }
            }

            target?.SetMergeHighlight(false, false);
            _hoveredCell?.SetHovered(false, false);
            _hoveredCell = null;
            _hoveredMergeTower = null;
            _draggedTower = null;
            _input.SetInteractionDragActive(false);
            RefreshStateDisplay();
            return merged || moved || returnedHome;
        }

        private void BeginBuildCardDrag(SpatialActionTarget action, TowerKind kind)
        {
            _selectedTower = kind;
            _draggedCardAction = action;
            _draggedCardKind = kind;
            _draggedCardGhost = new GameObject($"{kind} L1 Card Drag Preview").transform;
            _draggedCardGhost.SetParent(_actorsRoot, false);
            ProceduralFactory.BuildTowerVisual(kind, _draggedCardGhost, 1);
            SetLayerAndDisableColliders(_draggedCardGhost, 2);
            _input.SetInteractionDragActive(true);
            _input.Pulse(0.18f, 0.03f);
        }

        private void FinishBuildCardDrag(BoardCell cell, TowerAgent tower, SpatialActionTarget action, bool wasPointerDrag)
        {
            SpatialAction sourceAction = _draggedCardAction.Action;
            bool used = TryUseBuildCard(_draggedCardKind, cell, tower, true);
            bool selectedOnly = !used && !wasPointerDrag && action == _draggedCardAction;
            if (selectedOnly)
            {
                ExecuteAction(sourceAction);
            }
            else if (!used)
            {
                Play("invalid", 0.28f);
                _input.Pulse(0.12f, 0.03f);
            }

            _hoveredMergeTower?.SetMergeHighlight(false, false);
            _hoveredCell?.SetHovered(false, false);
            _hoveredMergeTower = null;
            _hoveredCell = null;
            DestroyRuntimeObject(_draggedCardGhost.gameObject);
            _draggedCardGhost = null;
            _draggedCardAction = null;
            _input.SetInteractionDragActive(false);
            RefreshStateDisplay();
        }

        private bool TryUseBuildCard(TowerKind kind, BoardCell cell, TowerAgent tower, bool feedback)
        {
            TowerDefinition definition = GameDefinitions.Tower(kind);
            if (_gold < definition.Cost)
            {
                return false;
            }

            if (CanUseBuildCardOnTower(kind, tower))
            {
                _gold -= definition.Cost;
                tower.Upgrade();
                if (feedback)
                {
                    Play("upgrade", 0.75f);
                    _input.Pulse(0.72f, 0.13f);
                    if (Application.isPlaying)
                    {
                        StartCoroutine(AnimateMergeBurst(tower.Root.transform.localPosition, tower.Definition.Color));
                    }
                }
                return true;
            }

            if (cell != null && CanBuild(cell, kind))
            {
                _gold -= definition.Cost;
                PlaceTowerAtCell(kind, cell.Coordinates, true);
                if (feedback)
                {
                    Play("build", 0.6f);
                    _input.Pulse(0.45f, 0.08f);
                }
                return true;
            }

            return false;
        }

        private bool CanUseBuildCardOnTower(TowerKind kind, TowerAgent tower)
        {
            return !_gameOver && tower != null && tower.Kind == kind && tower.Level == 1 &&
                   _gold >= GameDefinitions.Tower(kind).Cost;
        }

        private bool CanRelocate(TowerAgent tower, BoardCell cell)
        {
            return !_gameOver && tower != null && cell != null && cell.IsBuildable && !cell.IsPath &&
                   (!cell.IsOccupied || cell.Coordinates == tower.Coordinates);
        }

        private bool TryMergeTowers(TowerAgent source, TowerAgent target, bool feedback)
        {
            if (!CanMerge(source, target))
            {
                return false;
            }

            _towers.Remove(source);
            if (_cells.TryGetValue(source.Coordinates, out BoardCell sourceCell))
            {
                sourceCell.IsOccupied = false;
            }
            source.Dispose();
            target.Upgrade();
            if (feedback)
            {
                Play("upgrade", 0.75f);
                _input.Pulse(0.72f, 0.13f);
                if (Application.isPlaying)
                {
                    StartCoroutine(AnimateMergeBurst(target.Root.transform.localPosition, target.Definition.Color));
                }
            }
            return true;
        }

        private static bool CanMerge(TowerAgent source, TowerAgent target)
        {
            return source != null && target != null && source != target &&
                   source.Kind == target.Kind && source.Level == target.Level &&
                   source.Level < GameDefinitions.MaxTowerLevel;
        }

        private static bool TryGetTowerCardKind(SpatialAction action, out TowerKind kind)
        {
            switch (action)
            {
                case SpatialAction.SelectArrow:
                    kind = TowerKind.Arrow;
                    return true;
                case SpatialAction.SelectCannon:
                    kind = TowerKind.Cannon;
                    return true;
                case SpatialAction.SelectFrost:
                    kind = TowerKind.Frost;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static Vector3 CellTowerLocalPosition(Vector2Int coordinates)
        {
            Vector3 position = GameDefinitions.CellLocalPosition(coordinates.x, coordinates.y);
            position.y = PlacementTileSurfaceHeight(coordinates.y) + 0.004f;
            return position;
        }

        private static void SetLayerAndDisableColliders(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerAndDisableColliders(child, layer);
            }
            Collider collider = root.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private IEnumerator AnimateMergeBurst(Vector3 localPosition, Color color)
        {
            var burst = new GameObject("Tower Merge Burst").transform;
            burst.gameObject.layer = 2;
            burst.SetParent(_actorsRoot, false);
            burst.localPosition = localPosition + Vector3.up * 0.10f;
            Material material = ProceduralFactory.CreateMaterial(Color.Lerp(color, Color.white, 0.35f), 0.1f, 0.75f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Sphere,
                    $"Merge Spark {i + 1}",
                    burst,
                    new Vector3(Mathf.Cos(angle) * 0.06f, 0f, Mathf.Sin(angle) * 0.06f),
                    Vector3.one * 0.022f,
                    material);
            }

            float elapsed = 0f;
            while (elapsed < 0.46f)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                burst.Rotate(Vector3.up, 240f * delta, Space.Self);
                burst.localScale = Vector3.one * (1f + elapsed * 1.5f);
                burst.localPosition += Vector3.up * (0.10f * delta);
                yield return null;
            }

            Destroy(burst.gameObject);
        }

        /// <summary>
        /// Spawns a single expanding prayer-bell ripple ring on the arena
        /// floor when a new wave begins. Purely cosmetic — no gameplay effect.
        /// </summary>
        private IEnumerator AnimateWaveBellRipple()
        {
            var rippleRoot = new GameObject("Wave Bell Ripple").transform;
            rippleRoot.gameObject.layer = 2;
            rippleRoot.SetParent(_actorsRoot, false);
            rippleRoot.localPosition = new Vector3(0f, GameDefinitions.TableHeight + 0.006f, 0f);

            const int Segments = 48;
            var line = rippleRoot.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = CombatVisualEffects.LineMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.startWidth = 0.007f;
            line.endWidth = 0.007f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            const float Duration = 0.90f;
            const float MaxRadius = 1.60f;
            Color rippleColor = new Color(0.55f, 0.82f, 0.72f, 0f);
            float elapsed = 0f;

            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);
                float radius = Mathf.Lerp(0.06f, MaxRadius, 1f - Mathf.Pow(1f - t, 2f));
                rippleColor.a = Mathf.Sin(t * Mathf.PI) * 0.42f;
                line.startColor = rippleColor;
                line.endColor = new Color(rippleColor.r, rippleColor.g, rippleColor.b, rippleColor.a * 0.25f);
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i / (float)Segments * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }
                yield return null;
            }

            Destroy(rippleRoot.gameObject);
        }

        private IEnumerator AnimatePurifiedLight(Vector3 startWorldPosition)
        {
            Vector3 startLocal = _actorsRoot.InverseTransformPoint(startWorldPosition) + Vector3.up * 0.08f;
            Transform coin = ProceduralFactory.BuildImportedUiVisual(
                ProceduralFactory.CoinVisualResourcePath,
                "Enemy Defeat Coin",
                _actorsRoot,
                startLocal,
                0.065f,
                0.075f,
                -8f,
                90f);
            if (coin == null)
            {
                yield break;
            }

            // The reward is the supplied coin model, with the same face-on
            // orientation used by the coin HUD. Billboard the whole visual so
            // its front face stays readable from desktop and PICO views.
            AttachBillboard(coin);

            Vector3 start = startLocal;
            Vector3 targetLocal = _actorsRoot.InverseTransformPoint(_crystal.position);
            float elapsed = 0f;
            const float duration = 1.45f;
            while (elapsed < duration && _crystal != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.22f;
                coin.localPosition = Vector3.Lerp(start, targetLocal, t) + arc;
                coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.28f, t);
                yield return null;
            }

            Destroy(coin.gameObject);
        }

        private bool CanBuild(BoardCell cell, TowerKind kind)
        {
            return !_gameOver && cell != null && cell.IsBuildable && !cell.IsPath && !cell.IsOccupied && _gold >= GameDefinitions.Tower(kind).Cost;
        }

        private void SpawnSplitChildren(EnemyAgent source)
        {
            _currentWaveEnemyTotal += 2;
            Vector3 origin = source.LocalPosition;
            _enemies.Add(new EnemyAgent(
                EnemyKind.Runner,
                _pathLocal,
                _actorsRoot,
                source.PathSegment,
                origin + new Vector3(-0.025f, 0f, 0.018f)));
            _enemies.Add(new EnemyAgent(
                EnemyKind.Runner,
                _pathLocal,
                _actorsRoot,
                source.PathSegment,
                origin + new Vector3(0.025f, 0f, -0.018f)));
        }

        private bool StartNextWave()
        {
            if (_gameOver || _waveInProgress || _waveIndex >= GameDefinitions.Waves.Length)
            {
                return false;
            }

            _spawnQueue.Clear();
            float time = 0f;
            SpawnBatch[] wave = GameDefinitions.Waves[_waveIndex];
            for (int batchIndex = 0; batchIndex < wave.Length; batchIndex++)
            {
                SpawnBatch batch = wave[batchIndex];
                for (int i = 0; i < GameDefinitions.SpawnCount(batch); i++)
                {
                    _spawnQueue.Add(new SpawnEvent(batch.Kind, time));
                    time += batch.Gap;
                }
            }

            _spawnClock = 0f;
            _spawning = true;
            _waveInProgress = true;
            _currentWaveEnemyTotal = WaveEnemyCount(_waveIndex);
            _currentWaveEnemyResolved = 0;
            RefreshStateDisplay();

            // Brief double-pulse tells the player the wave has launched.
            _input.Pulse(0.55f, 0.10f);
            if (Application.isPlaying && _actorsRoot != null)
            {
                StartCoroutine(AnimateWaveBellRipple());
            }

            return true;
        }

        private void UpdateGame(float deltaTime)
        {
            if (_gameOver)
            {
                return;
            }

            if (_spawning)
            {
                _spawnClock += deltaTime;
                while (_spawnQueue.Count > 0 && _spawnQueue[0].Time <= _spawnClock)
                {
                    _enemies.Add(new EnemyAgent(_spawnQueue[0].Kind, _pathLocal, _actorsRoot));
                    _spawnQueue.RemoveAt(0);
                }

                if (_spawnQueue.Count == 0)
                {
                    _spawning = false;
                }
            }

            for (int i = 0; i < _towers.Count; i++)
            {
                ProjectileAgent projectile = _towers[i].TryFire(deltaTime, _enemies, _projectilesRoot);
                if (projectile != null)
                {
                    _projectiles.Add(projectile);
                    Play(projectile.Kind switch
                    {
                        TowerKind.Cannon => "cannon",
                        TowerKind.Frost => "frost",
                        _ => "arrow"
                    }, projectile.Kind == TowerKind.Cannon ? 0.18f : 0.10f);
                }
            }

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                _projectiles[i].Update(deltaTime, _enemies);
                if (_projectiles[i].IsFinished)
                {
                    _projectiles.RemoveAt(i);
                }
            }

            bool stateChanged = false;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                EnemyAgent enemy = _enemies[i];
                enemy.Update(deltaTime);
                enemy.HealNearby(deltaTime, _enemies);
                if (enemy.IsDead)
                {
                    _currentWaveEnemyResolved++;
                    if (enemy.Kind == EnemyKind.Splitter)
                    {
                        SpawnSplitChildren(enemy);
                    }
                    if (Application.isPlaying)
                    {
                        StartCoroutine(AnimateEnemyDefeatSmoke(
                            enemy.LocalPosition,
                            Mathf.Max(0.045f, enemy.Definition.Radius)));
                        StartCoroutine(AnimatePurifiedLight(enemy.Position));
                    }
                    _gold += enemy.Definition.Reward;
                    enemy.Dispose();
                    _enemies.RemoveAt(i);
                    stateChanged = true;
                }
                else if (enemy.ReachedEnd)
                {
                    _currentWaveEnemyResolved++;
                    _lives = Mathf.Max(0, _lives - enemy.Definition.CoreDamage);
                    if (enemy.Kind == EnemyKind.Runner)
                    {
                        _gold = Mathf.Max(0, _gold - 10);
                    }
                    enemy.Dispose();
                    _enemies.RemoveAt(i);
                    stateChanged = true;
                }
            }

            if (_lives <= 0)
            {
                _gameOver = true;
                _won = false;
                Play("lose", 0.9f);
                // Sustained low pulse signals defeat.
                _input.Pulse(0.80f, 0.22f);
                stateChanged = true;
            }
            else if (_waveInProgress && !_spawning && _spawnQueue.Count == 0 && _enemies.Count == 0)
            {
                _waveInProgress = false;
                _waveIndex++;
                if (_waveIndex == 1 && _interactionGuideRoot != null)
                {
                    // The guide stays visible throughout preparation and the
                    // whole first wave, then makes room only after that wave
                    // has genuinely resolved.
                    _interactionGuideRoot.gameObject.SetActive(false);
                }
                stateChanged = true;
                if (_waveIndex >= GameDefinitions.Waves.Length)
                {
                    _gameOver = true;
                    _won = true;
                    Play("win", 0.9f);
                    // Crisp strong pulse marks victory.
                    _input.Pulse(0.88f, 0.18f);
                }
            }

            if (stateChanged)
            {
                RefreshStateDisplay();
            }
        }

        private IEnumerator AnimateEnemyDefeatSmoke(Vector3 localPosition, float enemyRadius)
        {
            var smokeObject = new GameObject("Purified Enemy Cyan Smoke");
            smokeObject.layer = 2;
            smokeObject.transform.SetParent(_actorsRoot, false);
            smokeObject.transform.localPosition = localPosition + Vector3.up * enemyRadius * 0.55f;

            ParticleSystem particles = smokeObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.42f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.82f, 1.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.13f, 0.26f);
            main.startSize = new ParticleSystem.MinMaxCurve(enemyRadius * 0.50f, enemyRadius * 0.96f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = -0.055f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.68f, 0.58f, 0.68f),
                new Color(0.50f, 0.92f, 0.76f, 0.84f));
            main.maxParticles = 48;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 36f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = enemyRadius * 0.24f;
            shape.radiusThickness = 1f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = enemyRadius * 1.2f;
            noise.frequency = 0.72f;
            noise.scrollSpeed = 0.28f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.36f, 0.86f, 0.70f), 0f),
                    new GradientColorKey(new Color(0.13f, 0.50f, 0.46f), 0.58f),
                    new GradientColorKey(new Color(0.10f, 0.28f, 0.30f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.72f, 0.12f),
                    new GradientAlphaKey(0.42f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.34f),
                    new Keyframe(0.40f, 1f),
                    new Keyframe(1f, 1.55f)));

            ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particles.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);

            ParticleSystemRenderer renderer = smokeObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sharedMaterial = EnemySmokeMaterial();

            particles.Play();

            // Small golden burst at ground level — visual cue that Spirit Light
            // is being released. Reuses the line material via point emitters.
            if (enemyRadius >= 0.03f)
            {
                var sparkRoot = new GameObject("Purification Spark Burst").transform;
                sparkRoot.gameObject.layer = 2;
                sparkRoot.SetParent(_actorsRoot, false);
                sparkRoot.localPosition = localPosition + Vector3.up * enemyRadius * 0.28f;
                Material sparkMat = ProceduralFactory.CreateTransparentMaterial(new Color(1f, 0.86f, 0.42f, 0.88f)); // 透明材质，淡出才会生效
                for (int si = 0; si < 6; si++)
                {
                    float a = si / 6f * Mathf.PI * 2f;
                    float r = enemyRadius * 0.38f;
                    ProceduralFactory.VisualPrimitive(
                        PrimitiveType.Sphere,
                        $"Spark {si + 1}",
                        sparkRoot,
                        new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                        Vector3.one * (enemyRadius * 0.09f),
                        sparkMat);
                }
                float sparkElapsed = 0f;
                while (sparkElapsed < 0.50f)
                {
                    sparkElapsed += Time.deltaTime;
                    float st = Mathf.Clamp01(sparkElapsed / 0.50f);
                    sparkRoot.localScale = Vector3.one * (1f + st * 2.2f);
                    sparkRoot.localPosition = localPosition + Vector3.up * (enemyRadius * 0.28f + st * 0.18f);
                    sparkMat.color = new Color(1f, 0.86f, 0.42f, (1f - st) * 0.88f);
                    yield return null;
                }
                Destroy(sparkRoot.gameObject);
                Destroy(sparkMat); // 每次迸发新建的材质要销毁，避免泄漏
            }

            yield return new WaitForSeconds(1.15f);
            Destroy(smokeObject);
        }

        private Material EnemySmokeMaterial()
        {
            if (_enemySmokeMaterial != null)
            {
                return _enemySmokeMaterial;
            }

            Shader shader = Shader.Find("Particles/Standard Unlit") ??
                            Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
                            Shader.Find("Sprites/Default");
            _enemySmokeMaterial = new Material(shader)
            {
                name = "Enemy Cyan Smoke Material",
                color = Color.white,
                mainTexture = BuildSoftSmokeTexture()
            };
            _enemySmokeMaterial.renderQueue = 3010;
            return _enemySmokeMaterial;
        }

        private static Texture2D BuildSoftSmokeTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Soft Smoke Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - distance));
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void AnimateWorld(float deltaTime)
        {
            _crystalClock += deltaTime;
            if (_crystal != null)
            {
                Vector3 local = _crystal.localPosition;
                local.y = 0.20f + Mathf.Sin(_crystalClock * 2.2f) * 0.012f;
                _crystal.localPosition = local;
            }

        }

        private void UpdateRangePreview(BoardCell cell, bool valid)
        {
            if (cell == null)
            {
                _rangeRing.enabled = false;
                return;
            }

            Vector3 center = GameDefinitions.CellLocalPosition(cell.Coordinates.x, cell.Coordinates.y);
            center.y = PlacementTileSurfaceHeight(cell.Coordinates.y) + 0.010f;
            SetRangePreview(center, GameDefinitions.Tower(_selectedTower).Range,
                valid ? new Color(0.35f, 0.78f, 0.82f, 0.42f) : new Color(0.82f, 0.20f, 0.24f, 0.42f));
        }

        private void UpdateRangePreview(TowerAgent tower)
        {
            if (tower == null)
            {
                _rangeRing.enabled = false;
                return;
            }

            Vector3 center = _arenaRoot.InverseTransformPoint(tower.Root.transform.position);
            center.y = tower.Root.transform.localPosition.y + 0.012f;
            SetRangePreview(center, tower.Definition.Range, new Color(0.36f, 0.88f, 0.78f, 0.70f));
        }

        private void SetRangePreview(Vector3 center, float radius, Color color)
        {
            _rangeRing.enabled = true;
            _rangeRing.transform.localPosition = center;
            for (int i = 0; i < _rangeRing.positionCount; i++)
            {
                float angle = i / (float)_rangeRing.positionCount * Mathf.PI * 2f;
                _rangeRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            _rangeRing.startColor = color;
            _rangeRing.endColor = color;
        }

        private void RefreshActionVisuals()
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                SpatialActionTarget action = _actions[i];
                bool selected = action.Action switch
                {
                    SpatialAction.SelectArrow => _selectedTower == TowerKind.Arrow,
                    SpatialAction.SelectCannon => _selectedTower == TowerKind.Cannon,
                    SpatialAction.SelectFrost => _selectedTower == TowerKind.Frost,
                    _ => false
                };
                bool enabled = action.Action switch
                {
                    SpatialAction.StartExperience => !_experienceStarted,
                    SpatialAction.StartWave => _gameOver || (!_waveInProgress && _waveIndex < GameDefinitions.Waves.Length),
                    _ => true
                };
                action.SetState(selected, action == _hoveredAction, enabled);
            }
        }

        private void RefreshStateDisplay()
        {
            if (_statusHudRoot == null)
            {
                return;
            }

            int displayWave = Mathf.Clamp(_waveIndex + 1, 1, GameDefinitions.Waves.Length);
            _goldDigits.Set(_gold.ToString());
            _lifeDigits.Set($"{_lives}/{GameDefinitions.StartingLives}");
            _waveDigits.Set($"{displayWave}/{GameDefinitions.Waves.Length}");
            _waveArt.sharedMaterial = _gameOver ? GeneratedLotusPanel.Material("WaveStates", _won ? 1 : 2) :
                _waveInProgress ? GeneratedLotusPanel.Material("WaveStates", 0) : GeneratedLotusPanel.Material("Arsenal", 3);
            _startDigits.Set(_gameOver ? (_won ? "25/25" : "-") : $"{displayWave}/25");
            if (_baseHealthFill != null)
            {
                float width = 0.40f * Mathf.Clamp01(_lives / (float)GameDefinitions.StartingLives);
                Vector3 scale = _baseHealthFill.localScale;
                scale.x = Mathf.Max(0.005f, width);
                _baseHealthFill.localScale = scale;
                Vector3 position = _baseHealthFill.localPosition;
                position.x = -0.20f + width * 0.5f;
                _baseHealthFill.localPosition = position;
            }
            RefreshWaveBriefing();
            RefreshActionVisuals();
        }

        private void RefreshWaveBriefing()
        {
            if (_waveBriefingRoot == null)
            {
                return;
            }

            bool visible = _experienceStarted && !_gameOver && !_waveInProgress && !_spawning &&
                           _waveIndex >= 0 && _waveIndex < GameDefinitions.Waves.Length;
            _waveBriefingRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _briefingWaveDigits.Set($"{_waveIndex + 1}/25");
            int[] counts = new int[6];
            foreach (SpawnBatch batch in GameDefinitions.Waves[_waveIndex])
                counts[(int)batch.Kind] += GameDefinitions.SpawnCount(batch);
            for (int i = 0; i < 6; i++) _enemyCountDigits[i].Set(counts[i].ToString());
        }

        private static int WaveEnemyCount(int waveIndex)
        {
            if (waveIndex < 0 || waveIndex >= GameDefinitions.Waves.Length)
            {
                return 0;
            }

            int count = 0;
            SpawnBatch[] wave = GameDefinitions.Waves[waveIndex];
            for (int i = 0; i < wave.Length; i++)
            {
                count += GameDefinitions.SpawnCount(wave[i]);
            }
            return count;
        }

        private static string EnemySkillSummary(EnemyKind kind, int count)
        {
            return kind switch
            {
                EnemyKind.Runner => $"Fleeting Obsession ×{count}\nFast, steals Spirit Light",
                EnemyKind.Tank => $"Ignorance Beast ×{count}\n800 HP / 38% Armor\nTemple Damage 4",
                EnemyKind.Shield => $"Doubt Carapace ×{count}\nStone-shell shield",
                EnemyKind.Splitter => $"Wrath Flame ×{count}\nHastens allies, splits",
                EnemyKind.Support => $"Delusion Fog ×{count}\nHeals allies, silences towers",
                _ => $"Drifting Dust ×{count}\nSwarming raid"
            };
        }

        private void ConfigureGameplayStagePlayerStart()
        {
            _input.ConfigureDesktopView(
                _arenaRoot.TransformPoint(GameDefinitions.DesignPlayerViewTargetLocal),
                _worldScale,
                GameDefinitions.DesignPlayerYaw,
                GameDefinitions.DesignPlayerPitch,
                GameDefinitions.DesignPlayerTableDistance);
            // The desktop fallback camera is a child of the same rig but its
            // own orbit is already the design pose. Only a tracked XR head
            // needs the floor-origin root repositioned.
            if (_input.HasTrackedHead)
            {
                ResetGameplayXrRigToDesignStart();
            }
            else
            {
                _xrRigPlacedAtDesignStart = false;
            }
        }

        private void ResetGameplayXrRigToDesignStart()
        {
            if (_input == null || _arenaRoot == null)
            {
                return;
            }

            // The authored arena and all gameplay stay fixed in world space.
            // Only the XR Rig returns to the single design start, preserving
            // the headset's local tracking offset in SpatialInputRig.
            Vector3 designEyeWorldPosition = _arenaRoot.TransformPoint(GameDefinitions.DesignPlayerEyeLocal);
            Quaternion yaw = _arenaRoot.rotation * Quaternion.Euler(0f, GameDefinitions.DesignPlayerYaw, 0f);
            Vector3 rigWorldPosition = designEyeWorldPosition - yaw * Vector3.up * GameDefinitions.DesignPlayerEyeHeight;
            _input.ResetXrRigToDesignStart(rigWorldPosition, yaw);
            _xrRigPlacedAtDesignStart = true;
        }

        private float WorldTowerMergeSnapRadius => TowerMergeSnapRadius * _worldScale;

        private static void AdoptAuthoredTerrain(Transform arenaRoot)
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            for (int i = 0; i < terrains.Length; i++)
            {
                Transform terrain = terrains[i].transform;
                if (terrain != arenaRoot && !terrain.IsChildOf(arenaRoot))
                {
                    terrain.SetParent(arenaRoot, true);
                }
            }
        }

        private void AttachBillboard(Transform textTransform)
        {
            BillboardText billboard = textTransform.gameObject.AddComponent<BillboardText>();
            billboard.Target = _input.Camera.transform;
            billboard.FaceTarget();
        }

        private void FacePanelTowardDesignStart(Transform panel)
        {
            if (panel == null || _arenaRoot == null)
            {
                return;
            }

            Vector3 designEye = GameDefinitions.DesignPlayerEyeLocal;
            Vector3 awayFromViewer = panel.localPosition - designEye;
            if (awayFromViewer.sqrMagnitude > 0.0001f)
            {
                panel.localRotation = Quaternion.LookRotation(awayFromViewer.normalized, Vector3.up);
            }
        }

        private void FaceAllTextNow()
        {
            BillboardText[] texts = _arenaRoot.GetComponentsInChildren<BillboardText>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].FaceTarget();
            }
        }

        private AudioClip CreateTone(string clipName, float startFrequency, float endFrequency, float duration)
        {
            const int sampleRate = 24000;
            int count = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Sin(Mathf.PI * t) * 0.28f;
                samples[i] = Mathf.Sin(phase) * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip LoadOrTone(string resourcePath, string clipName, float startFrequency, float endFrequency, float duration)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            return clip != null ? clip : CreateTone(clipName, startFrequency, endFrequency, duration);
        }

        private AudioClip CreateAmbientMusic()
        {
            const int sampleRate = 24000;
            const float duration = 12f;
            int count = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[count];
            float[] chord = { 110f, 138.59f, 164.81f, 220f, 277.18f, 329.63f };
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                int noteIndex = Mathf.FloorToInt(time * 2f) % chord.Length;
                float note = chord[noteIndex];
                float pulse = Mathf.SmoothStep(0f, 1f, Mathf.Sin((time * 2f % 1f) * Mathf.PI));
                float pad = Mathf.Sin(time * Mathf.PI * 2f * 55f) * 0.045f +
                            Mathf.Sin(time * Mathf.PI * 2f * 82.41f) * 0.032f;
                float melody = Mathf.Sin(time * Mathf.PI * 2f * note) * pulse * 0.035f;
                samples[i] = (pad + melody) * 0.72f;
            }

            AudioClip clip = AudioClip.Create("Procedural Lotus City Loop", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Play(string clipName, float volume)
        {
            if (_audioSource != null && _audioClips.TryGetValue(clipName, out AudioClip clip))
            {
                _audioSource.PlayOneShot(clip, volume * SfxMasterVolume);
            }
        }
    }
}
