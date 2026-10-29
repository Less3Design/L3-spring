using UnityEngine;

namespace Less3.Springs
{
    public class SpringManager : MonoBehaviour
    {
        public static SpringManager Instance { get; private set; }

        /// Set by Configure(). Takes priority over the settings asset.
        private static int? configuredInitialSize;
        private static int? configuredMaxSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Bootstrap()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("L3 Spring Manager");
                Instance = obj.AddComponent<SpringManager>();
                DontDestroyOnLoad(obj);

                int initial = DEFAULT_INITIAL_POOL_SIZE;
                int max = DEFAULT_MAX_POOL_SIZE;
                SpringSettings settings = SpringSettings.Load();
                if (settings != null)
                {
                    initial = settings.initialPoolSize;
                    max = settings.maxPoolSize;
                }
                Instance.Initialize(configuredInitialSize ?? initial, configuredMaxSize ?? max);
            }
        }

        /// <summary>
        /// Code alternative to the settings asset. Call it before the first spring, e.g. from a
        /// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]</c>.
        /// Calling it later rebuilds the pool only while no springs are running.
        /// </summary>
        public static void Configure(int initialPoolSize, int maxPoolSize)
        {
            configuredInitialSize = initialPoolSize;
            configuredMaxSize = maxPoolSize;
            if (Instance == null) return;

            if (Instance.activeCount > 0)
            {
                Debug.LogWarning($"{nameof(SpringManager)}: Configure called while {Instance.activeCount} springs are running; the new sizes apply on the next bootstrap.");
                return;
            }
            Instance.Initialize(initialPoolSize, maxPoolSize);
        }

        // * ---------------------

        public const int DEFAULT_INITIAL_POOL_SIZE = 256;
        public const int DEFAULT_MAX_POOL_SIZE = 4096;
        /// Ids are ushorts, so the pool can never hold more than this.
        public const int ABSOLUTE_MAX_POOL_SIZE = ushort.MaxValue + 1;

        // * ---------------------

        /// Every SpringObject ever created, indexed by id. Handles point into this.
        private SpringObject[] springPool;
        /// FIFO ring of ids that are not in use. Round robin reuse keeps stale handles from colliding for as long as possible.
        private ushort[] freeIds;
        private int freeHead;
        private int freeCount;
        /// Dense list of the ids currently running. Only these are ticked.
        private ushort[] activeIds;
        private int activeCount;
        /// While ticking, releases only mark the entry stale; the list is compacted once the tick is over.
        private bool ticking;
        private bool needsCompact;

        private int maxPoolSize;

        public int PoolSize => springPool.Length;
        public int MaxPoolSize => maxPoolSize;
        public int ActiveCount => activeCount;
        /// Ids waiting in the free ring. ActiveCount + FreeCount == PoolSize whenever no tick is in progress.
        internal int FreeCount => freeCount;

        /// <summary>
        /// Make a manually created manager the one the Spring API talks to. Tests use this to run
        /// without the bootstrap. Pass null to clear it.
        /// </summary>
        internal static void InstallInstance(SpringManager manager)
        {
            Instance = manager;
        }

        public void Initialize()
        {
            Initialize(DEFAULT_INITIAL_POOL_SIZE, DEFAULT_MAX_POOL_SIZE);
        }

        /// <summary>
        /// Build the pool. The pool starts at initialSize and doubles whenever it runs out, up to maxSize.
        /// </summary>
        public void Initialize(int initialSize, int maxSize)
        {
            maxPoolSize = Mathf.Clamp(maxSize, 1, ABSOLUTE_MAX_POOL_SIZE);
            initialSize = Mathf.Clamp(initialSize, 1, maxPoolSize);

            springPool = new SpringObject[initialSize];
            freeIds = new ushort[initialSize];
            activeIds = new ushort[initialSize];
            for (int i = 0; i < initialSize; i++)
            {
                springPool[i] = new SpringObject((ushort)i);
                freeIds[i] = (ushort)i;
            }
            freeHead = 0;
            freeCount = initialSize;
            activeCount = 0;
        }

        /// <summary>
        /// Double the pool, up to maxPoolSize. Only called when the free ring is empty, so the ring is simply refilled.
        /// </summary>
        private bool TryGrow()
        {
            int oldSize = springPool.Length;
            if (oldSize >= maxPoolSize) return false;
            int newSize = Mathf.Min(oldSize * 2, maxPoolSize);

            System.Array.Resize(ref springPool, newSize);
            System.Array.Resize(ref freeIds, newSize);
            System.Array.Resize(ref activeIds, newSize);
            for (int i = oldSize; i < newSize; i++)
            {
                springPool[i] = new SpringObject((ushort)i);
            }
            // the ring is empty, so restart it at 0 and fill it with the new ids
            freeHead = 0;
            freeCount = 0;
            for (int i = oldSize; i < newSize; i++)
            {
                freeIds[freeCount++] = (ushort)i;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"{nameof(SpringManager)}: Pool grew from {oldSize} to {newSize} (max {maxPoolSize}). Raise the initial pool size in the spring settings to avoid this at runtime.");
#endif
            return true;
        }

        private void Update()
        {
            Step(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
            StepFixed(Time.fixedDeltaTime);
        }

        /// <summary>
        /// One frame of the DeltaTime and UnscaledDeltaTime springs. Update() calls this with the engine's times;
        /// tests call it directly to step deterministically.
        /// </summary>
        internal void Step(float deltaTime, float unscaledDeltaTime)
        {
            if (activeCount == 0) return;
            TickActive(fixedStep: false, deltaTime, unscaledDeltaTime);
        }

        /// <summary>
        /// One physics step of the FixedDeltaTime springs.
        /// </summary>
        internal void StepFixed(float fixedDeltaTime)
        {
            if (activeCount == 0) return;
            TickActive(fixedStep: true, fixedDeltaTime, fixedDeltaTime);
        }

        /// <summary>
        /// Tick every active spring that belongs to this step.
        /// Springs created during the tick are appended past the captured count and first tick next frame.
        /// </summary>
        private void TickActive(bool fixedStep, float deltaTime, float unscaledDeltaTime)
        {
            ticking = true;
            int count = activeCount;
            for (int i = 0; i < count; i++)
            {
                SpringObject obj = springPool[activeIds[i]];
                if (obj.activeIndex != i) continue;// released earlier in this tick
                bool isFixed = obj.deltaTimeType == SpringUpdateType.FixedDeltaTime;
                if (isFixed != fixedStep) continue;

                Tick(obj, obj.deltaTimeType == SpringUpdateType.UnscaledDeltaTime ? unscaledDeltaTime : deltaTime);
            }
            ticking = false;
            if (needsCompact) Compact();
        }

        private void Tick(SpringObject obj, float deltaTime)
        {
            ushort version = obj.version;
            bool settled = obj.Update(deltaTime);
            // onUpdate may have cancelled this spring, or cancelled and re-allocated the same slot.
            // either way it is no longer the spring we were ticking.
            if (!obj.active || obj.version != version) return;
            if (!settled) return;

            // release before invoking so the callback sees a dead handle and can safely cancel / allocate
            System.Action onComplete = obj.onComplete;
            Release(obj);
            try
            {
                onComplete?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SpringObject: Exception in onComplete callback: {e}");
            }
        }

        /// <summary>
        /// Drop the entries released during the last tick from the active list and return their ids to the free ring.
        /// </summary>
        private void Compact()
        {
            int write = 0;
            for (int read = 0; read < activeCount; read++)
            {
                ushort id = activeIds[read];
                SpringObject obj = springPool[id];
                if (obj.activeIndex != read)
                {
                    PushFree(id);
                    continue;
                }
                obj.activeIndex = write;
                activeIds[write] = id;
                write++;
            }
            activeCount = write;
            needsCompact = false;
        }

        private void PushFree(ushort id)
        {
            freeIds[(freeHead + freeCount) % freeIds.Length] = id;
            freeCount++;
        }

        public Spring AllocateSpring(float from, float target)
        {
            if (freeCount == 0 && !TryGrow())
            {
                Debug.LogError($"{nameof(SpringManager)}: No free spring slots available! Pool is at its max of {maxPoolSize}.");
                return default;
            }

            ushort id = freeIds[freeHead];
            freeHead = (freeHead + 1) % freeIds.Length;
            freeCount--;

            SpringObject obj = springPool[id];
            obj.Allocate(from, target);
            obj.activeIndex = activeCount;
            activeIds[activeCount++] = id;
            return new Spring(id, obj.version);
        }

        /// <summary>
        /// Kill the spring, invalidate its handles and return its id to the free ring.
        /// </summary>
        private void Release(SpringObject obj)
        {
            int index = obj.activeIndex;
            obj.Release();
            obj.activeIndex = -1;

            if (ticking)
            {
                // leave the entry in place so the tick loop's indices stay valid. the loop skips it and
                // Compact drops it (and frees the id) once the tick is over, so an id is never listed twice.
                needsCompact = true;
                return;
            }

            // swap remove
            activeCount--;
            if (index != activeCount)
            {
                ushort moved = activeIds[activeCount];
                activeIds[index] = moved;
                springPool[moved].activeIndex = index;
            }
            PushFree(obj.id);
        }

        public void CancelSpring(Spring spring)
        {
            if (TryGetSpringObject(spring, out SpringObject obj))
            {
                System.Action onCancel = obj.onCancel;
                Release(obj);
                try
                {
                    onCancel?.Invoke();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"SpringObject: Exception in onCancel callback: {e}");
                }
            }
        }

        /// <summary>
        /// Resolve a handle to its live SpringObject. Fails for default handles, stale handles and finished springs.
        /// </summary>
        public bool TryGetSpringObject(Spring spring, out SpringObject obj)
        {
            if (spring.version != 0 && spring.id < springPool.Length)
            {
                obj = springPool[spring.id];
                if (obj.active && obj.version == spring.version)
                {
                    return true;
                }
            }
            obj = null;
            return false;
        }
    }
}
