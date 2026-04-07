using System.Collections.Generic;
using UnityEngine;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ObstacleSpawner : MonoBehaviour
    {
        private sealed class ActiveSegment
        {
            public GameObject root;
            public readonly List<GameObject> pieces = new();
        }

        [Header("References")]
        public Transform obstacleRoot;
        public FlappyPlayerController playerController;
        public List<ObstaclePatternDefinition> patternDefinitions = new();

        [Header("Layout")]
        public float spawnX = 13f;
        public float despawnX = -13f;
        public float initialSpawnDelay = 1.75f;
        public Vector2 spawnIntervalEasy = new(2.1f, 2.6f);
        public Vector2 spawnIntervalHard = new(1.35f, 1.8f);

        public float CurrentDifficulty => currentDifficulty;

        private readonly List<ActiveSegment> activeSegments = new();
        private readonly Dictionary<GameObject, Queue<GameObject>> pooledPieces = new();
        private readonly Dictionary<GameObject, GameObject> instancePrefabLookup = new();
        private readonly Queue<GameObject> pooledSegmentRoots = new();

        private float currentDifficulty;
        private float scrollSpeed;
        private float spawnTimer;
        private bool simulationRunning;
        private bool spawningEnabled;

        private void Awake()
        {
            if (obstacleRoot == null)
            {
                obstacleRoot = transform;
            }
        }

        private void Update()
        {
            if (!simulationRunning)
            {
                return;
            }

            MoveActiveSegments(Time.deltaTime);
            if (!spawningEnabled)
            {
                return;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f)
            {
                return;
            }

            spawnTimer = SpawnSegment();
        }

        public void SetScrollSpeed(float speed)
        {
            scrollSpeed = Mathf.Max(0f, speed);
        }

        public void SetDifficulty(float difficulty)
        {
            currentDifficulty = Mathf.Clamp01(difficulty);
        }

        public void BeginSpawning()
        {
            simulationRunning = true;
            spawningEnabled = true;
            spawnTimer = Mathf.Max(0f, initialSpawnDelay);
        }

        public void StopSimulation()
        {
            simulationRunning = false;
            spawningEnabled = false;
        }

        public void ResetRun()
        {
            StopSimulation();
            spawnTimer = Mathf.Max(0f, initialSpawnDelay);

            for (int index = activeSegments.Count - 1; index >= 0; index--)
            {
                RecycleSegment(activeSegments[index]);
            }

            activeSegments.Clear();
        }

        public static ObstaclePatternDefinition SelectPatternForDifficulty(
            IReadOnlyList<ObstaclePatternDefinition> patterns,
            float difficulty,
            float roll)
        {
            if (patterns == null || patterns.Count == 0)
            {
                return null;
            }

            List<ObstaclePatternDefinition> eligiblePatterns = new();
            float totalWeight = 0f;
            for (int index = 0; index < patterns.Count; index++)
            {
                ObstaclePatternDefinition pattern = patterns[index];
                if (pattern == null || !pattern.SupportsDifficulty(difficulty))
                {
                    continue;
                }

                eligiblePatterns.Add(pattern);
                totalWeight += Mathf.Max(0.01f, pattern.weight);
            }

            if (eligiblePatterns.Count == 0 || totalWeight <= 0f)
            {
                return null;
            }

            float threshold = Mathf.Clamp01(roll) * totalWeight;
            float cursor = 0f;
            for (int index = 0; index < eligiblePatterns.Count; index++)
            {
                ObstaclePatternDefinition pattern = eligiblePatterns[index];
                cursor += Mathf.Max(0.01f, pattern.weight);
                if (threshold <= cursor)
                {
                    return pattern;
                }
            }

            return eligiblePatterns[eligiblePatterns.Count - 1];
        }

        private void MoveActiveSegments(float deltaTime)
        {
            float horizontalDelta = scrollSpeed * Mathf.Max(0f, deltaTime);
            for (int index = activeSegments.Count - 1; index >= 0; index--)
            {
                ActiveSegment segment = activeSegments[index];
                if (segment.root == null)
                {
                    activeSegments.RemoveAt(index);
                    continue;
                }

                segment.root.transform.position += Vector3.left * horizontalDelta;
                if (segment.root.transform.position.x > despawnX)
                {
                    continue;
                }

                RecycleSegment(segment);
                activeSegments.RemoveAt(index);
            }
        }

        private float SpawnSegment()
        {
            ObstaclePatternDefinition pattern = SelectPatternForDifficulty(patternDefinitions, currentDifficulty, Random.value);
            if (pattern == null)
            {
                return GetSpawnInterval(null);
            }

            ActiveSegment segment = new()
            {
                root = GetSegmentRoot()
            };

            float anchorY = Random.Range(pattern.spawnYRange.x, pattern.spawnYRange.y);
            float gapSize = Random.Range(pattern.gapRange.x, pattern.gapRange.y);

            Transform rootTransform = segment.root.transform;
            rootTransform.SetParent(obstacleRoot, false);
            rootTransform.position = new Vector3(spawnX, anchorY, 0f);
            rootTransform.rotation = Quaternion.identity;
            rootTransform.localScale = Vector3.one;
            segment.root.SetActive(true);

            foreach (ObstaclePatternDefinition.PatternElement element in pattern.elements)
            {
                if (element == null || element.prefab == null)
                {
                    continue;
                }

                GameObject piece = GetPiece(element.prefab);
                Transform pieceTransform = piece.transform;
                pieceTransform.SetParent(rootTransform, false);
                pieceTransform.localPosition = GetSpawnLocalPosition(element, gapSize);
                pieceTransform.localRotation = Quaternion.Euler(GetSpawnRotation(element));
                pieceTransform.localScale = GetSpawnScale(element, gapSize);
                ConfigurePiece(piece, pieceTransform.localPosition, element);
                piece.SetActive(true);
                segment.pieces.Add(piece);
            }

            activeSegments.Add(segment);
            return GetSpawnInterval(pattern);
        }

        private float GetSpawnInterval(ObstaclePatternDefinition pattern)
        {
            Vector2 intervalRange = Vector2.Lerp(spawnIntervalEasy, spawnIntervalHard, currentDifficulty);
            float minInterval = Mathf.Min(intervalRange.x, intervalRange.y);
            float maxInterval = Mathf.Max(intervalRange.x, intervalRange.y);
            float interval = Random.Range(minInterval, maxInterval);
            float multiplier = pattern != null ? Mathf.Max(0.25f, pattern.spawnIntervalMultiplier) : 1f;
            return interval * multiplier;
        }

        private GameObject GetSegmentRoot()
        {
            if (pooledSegmentRoots.Count > 0)
            {
                return pooledSegmentRoots.Dequeue();
            }

            GameObject root = new("ObstacleSegment");
            root.transform.SetParent(obstacleRoot, false);
            return root;
        }

        private GameObject GetPiece(GameObject prefab)
        {
            if (!pooledPieces.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pooledPieces[prefab] = pool;
            }

            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }

            GameObject instance = Instantiate(prefab);
            instance.name = prefab.name;
            instancePrefabLookup[instance] = prefab;
            return instance;
        }

        private void RecycleSegment(ActiveSegment segment)
        {
            if (segment.root == null)
            {
                return;
            }

            for (int pieceIndex = 0; pieceIndex < segment.pieces.Count; pieceIndex++)
            {
                GameObject piece = segment.pieces[pieceIndex];
                if (piece == null || !instancePrefabLookup.TryGetValue(piece, out GameObject prefab))
                {
                    continue;
                }

                ResetPiece(piece);
                piece.SetActive(false);
                piece.transform.SetParent(obstacleRoot, false);
                pooledPieces[prefab].Enqueue(piece);
            }

            segment.pieces.Clear();
            segment.root.SetActive(false);
            segment.root.transform.SetParent(obstacleRoot, false);
            pooledSegmentRoots.Enqueue(segment.root);
        }

        private void ConfigurePiece(GameObject piece, Vector3 anchorLocalPosition, ObstaclePatternDefinition.PatternElement element)
        {
            ConfigureCollision(piece, element);
            ConfigureMotion(piece, anchorLocalPosition, element);
            ConfigureWind(piece, element);
            ConfigureRenderer(piece, element);
        }

        private void ConfigureCollision(GameObject piece, ObstaclePatternDefinition.PatternElement element)
        {
            ObstacleCollisionRelay relay = piece.GetComponent<ObstacleCollisionRelay>();
            if (element.causesCollision)
            {
                relay ??= piece.AddComponent<ObstacleCollisionRelay>();
                relay.enabled = true;
                relay.playerController = playerController;
            }
            else if (relay != null)
            {
                relay.playerController = null;
                relay.enabled = false;
            }
        }

        private void ConfigureMotion(GameObject piece, Vector3 anchorLocalPosition, ObstaclePatternDefinition.PatternElement element)
        {
            ObstacleMotionDriver motionDriver = piece.GetComponent<ObstacleMotionDriver>();
            if (element.enableMotion)
            {
                motionDriver ??= piece.AddComponent<ObstacleMotionDriver>();
                motionDriver.Configure(
                    anchorLocalPosition,
                    element.motionAxis,
                    element.motionAmplitude,
                    element.motionFrequency,
                    element.motionPhaseOffset);
            }
            else if (motionDriver != null)
            {
                motionDriver.DisableMotion(anchorLocalPosition);
                Destroy(motionDriver);
            }
        }

        private void ConfigureWind(GameObject piece, ObstaclePatternDefinition.PatternElement element)
        {
            WindZoneVolume windZone = piece.GetComponent<WindZoneVolume>();
            if (element.enableWind)
            {
                windZone ??= piece.AddComponent<WindZoneVolume>();
                windZone.Configure(element.windAcceleration, playerController);
            }
            else if (windZone != null)
            {
                Destroy(windZone);
            }
        }

        private static void ConfigureRenderer(GameObject piece, ObstaclePatternDefinition.PatternElement element)
        {
            if (!piece.TryGetComponent(out Renderer renderer))
            {
                return;
            }

            if (!element.useColorOverride)
            {
                renderer.SetPropertyBlock(null);
                return;
            }

            MaterialPropertyBlock block = new();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", element.colorOverride);
            block.SetColor("_Color", element.colorOverride);
            renderer.SetPropertyBlock(block);
        }

        private static void ResetPiece(GameObject piece)
        {
            if (piece == null)
            {
                return;
            }

            if (piece.TryGetComponent(out ObstacleCollisionRelay relay))
            {
                relay.playerController = null;
                relay.enabled = false;
            }

            if (piece.TryGetComponent(out WindZoneVolume windZone))
            {
                Destroy(windZone);
            }

            if (piece.TryGetComponent(out ObstacleMotionDriver motionDriver))
            {
                motionDriver.DisableMotion(Vector3.zero);
                Destroy(motionDriver);
            }

            if (piece.TryGetComponent(out Renderer renderer))
            {
                renderer.SetPropertyBlock(null);
            }
        }

        private static Vector3 GetSpawnRotation(ObstaclePatternDefinition.PatternElement element)
        {
            Vector3 rotation = element.localRotation;
            if (element.randomZRotationRange != Vector2.zero)
            {
                rotation.z += Random.Range(element.randomZRotationRange.x, element.randomZRotationRange.y);
            }

            return rotation;
        }

        private static Vector3 GetSpawnLocalPosition(ObstaclePatternDefinition.PatternElement element, float gapSize)
        {
            return element.localOffset + Vector3.up * (element.gapOffsetFactor * gapSize * 0.5f);
        }

        private static Vector3 GetSpawnScale(ObstaclePatternDefinition.PatternElement element, float gapSize)
        {
            Vector3 scale = element.localScale == Vector3.zero ? Vector3.one : element.localScale;
            if (element.fitGapHeight)
            {
                scale.y = Mathf.Max(0.1f, gapSize - element.gapHeightPadding);
            }

            return scale;
        }
    }
}
