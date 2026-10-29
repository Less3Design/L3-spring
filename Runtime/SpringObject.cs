using UnityEngine;
using System;

namespace Less3.Springs
{
    /// <summary>
    /// The pooled state behind every spring handle. A damped harmonic oscillator with unit mass:
    /// every step the spring is pulled toward its target by stiffness and slowed by damping.
    /// State is a Vector4 so one object serves Spring, Spring2, Spring3 and Spring4; unused components stay at zero.
    /// Stiffness, damping and the rest threshold are shared across components, so a diagonal move feels the same as an axis aligned one.
    /// </summary>
    public class SpringObject
    {
        public ushort id;// maps to the index in the spring pool
        public ushort version;// increments each time the spring is released, so stale handles stop matching. never 0.
        public bool active;
        public int activeIndex = -1;// position in the manager's active list, -1 when not running

        public byte dimensions;// 1..4, decides which onUpdate is invoked
        public Vector4 value;
        public Vector4 velocity;
        public Vector4 target;
        public float stiffness;// how hard the spring pulls toward the target (k)
        public float damping;// how hard it resists motion (c)
        public float restThreshold;// at rest once both |value - target| and |velocity| are under this
        public SpringUpdateType deltaTimeType;

        // only the one matching dimensions is ever set
        public Action<float> onUpdate1;
        public Action<Vector2> onUpdate2;
        public Action<Vector3> onUpdate3;
        public Action<Vector4> onUpdate4;
        public Action onComplete;
        public Action onCancel;

        /// Integration happens in sub steps no longer than this so a frame hitch cannot make a stiff spring explode.
        public const float MAX_SUB_STEP = 1f / 120f;
        /// Cap on sub steps per frame. A longer frame is clamped to MAX_SUB_STEPS * MAX_SUB_STEP of simulated time,
        /// so a hitch slows the spring down rather than growing the step size and risking a blow up.
        public const int MAX_SUB_STEPS = 16;

        public const float DEFAULT_STIFFNESS = 170f;
        public const float DEFAULT_DAMPING = 26f;// critically damped for stiffness 170: no overshoot
        public const float DEFAULT_REST_THRESHOLD = 0.001f;

        public SpringObject(ushort id)
        {
            this.id = id;
            this.version = 1;
        }

        public void Allocate(Vector4 from, Vector4 target, byte dimensions)
        {
            active = true;
            this.dimensions = dimensions;
            this.value = from;
            this.target = target;
            this.velocity = Vector4.zero;

            // default values
            this.stiffness = DEFAULT_STIFFNESS;
            this.damping = DEFAULT_DAMPING;
            this.restThreshold = DEFAULT_REST_THRESHOLD;
            this.deltaTimeType = SpringUpdateType.DeltaTime;
            ClearCallbacks();
        }

        /// <summary>
        /// Advance the spring and invoke onUpdate. Returns true once the spring has come to rest on its target.
        /// The final onUpdate lands exactly on the target. Does not release the spring; the manager does that
        /// so callbacks see a dead handle.
        /// </summary>
        public bool Update(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                deltaTime = Mathf.Min(deltaTime, MAX_SUB_STEP * MAX_SUB_STEPS);
                int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / MAX_SUB_STEP), 1, MAX_SUB_STEPS);
                float h = deltaTime / steps;
                for (int i = 0; i < steps; i++)
                {
                    // semi implicit Euler: update velocity first, then move with the new velocity
                    Vector4 acceleration = -stiffness * (value - target) - damping * velocity;
                    velocity += acceleration * h;
                    value += velocity * h;
                }
            }

            float thresholdSq = restThreshold * restThreshold;
            bool atRest = (value - target).sqrMagnitude < thresholdSq && velocity.sqrMagnitude < thresholdSq;
            if (atRest)
            {
                value = target;
                velocity = Vector4.zero;
            }

            try
            {
                switch (dimensions)
                {
                    case 1: onUpdate1?.Invoke(value.x); break;
                    case 2: onUpdate2?.Invoke(value); break;
                    case 3: onUpdate3?.Invoke(value); break;
                    default: onUpdate4?.Invoke(value); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SpringObject: Exception in onUpdate callback: {e}");
            }
            return atRest;
        }

        /// <summary>
        /// Mark the spring dead and invalidate every handle pointing at it.
        /// </summary>
        public void Release()
        {
            active = false;
            version++;
            if (version == 0) version = 1; // 0 is reserved for invalid handles
            ClearCallbacks();
        }

        private void ClearCallbacks()
        {
            onUpdate1 = null;
            onUpdate2 = null;
            onUpdate3 = null;
            onUpdate4 = null;
            onComplete = null;
            onCancel = null;
        }
    }

    public enum SpringUpdateType : byte
    {
        DeltaTime,
        FixedDeltaTime,
        UnscaledDeltaTime
    }

    /// <summary>
    /// The lookup every handle struct goes through. Resolves (id, version) to a live object of the expected
    /// dimension and logs a warning when the handle is dead, so the setters on each handle stay one liners.
    /// </summary>
    internal static class SpringHandle
    {
        public static bool TryGet(ushort id, ushort version, byte dimensions, out SpringObject obj)
        {
            if (version != 0 && SpringManager.Instance != null)
            {
                return SpringManager.Instance.TryGetSpringObject(id, version, dimensions, out obj);
            }
            obj = null;
            return false;
        }

        public static bool TryGet(ushort id, ushort version, byte dimensions, string operation, out SpringObject obj)
        {
            if (TryGet(id, version, dimensions, out obj)) return true;
            Debug.LogWarning($"Spring{(dimensions > 1 ? dimensions.ToString() : "")}: {operation} failed to find SpringObject for id {id} version {version}");
            return false;
        }

        public static void SetFrequency(SpringObject obj, float frequencyHz, float dampingRatio)
        {
            float omega = 2f * Mathf.PI * frequencyHz;
            obj.stiffness = omega * omega;
            obj.damping = 2f * dampingRatio * omega;
        }
    }
}
