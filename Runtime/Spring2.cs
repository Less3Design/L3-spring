using UnityEngine;

namespace Less3.Springs
{
    /// <summary>
    /// A soft reference to a Vector2 spring: one pooled spring whose value, velocity and target are Vector2s.
    /// Stiffness, damping and the rest threshold are shared across components, so a diagonal move feels the same as an axis aligned one.
    /// The handle becomes invalid as soon as the spring settles on its target or is cancelled. A running spring can be
    /// retargeted at any time with <see cref="SetTarget"/> and keeps its momentum.
    /// </summary>
    public readonly struct Spring2
    {
        private const byte DIMENSIONS = 2;

        public readonly ushort id;
        public readonly ushort version;// 0 = invalid

        public Spring2(ushort id, ushort version)
        {
            this.id = id;
            this.version = version;
        }

        public int key { get { return (version << 16) | id; } }

        /// <summary>
        /// True while the spring this handle points to is still moving.
        /// </summary>
        public bool IsActive => SpringHandle.TryGet(id, version, DIMENSIONS, out _);

        /// <summary>Current value, or zero when the handle is dead.</summary>
        public Vector2 Value => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? (Vector2)obj.value : Vector2.zero;
        /// <summary>Current velocity in units per second, or zero when the handle is dead.</summary>
        public Vector2 Velocity => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? (Vector2)obj.velocity : Vector2.zero;
        /// <summary>Current target, or zero when the handle is dead.</summary>
        public Vector2 Target => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? (Vector2)obj.target : Vector2.zero;

        /// <summary>
        /// Start a new spring from zero toward the target. The returned handle replaces this one, so assign it: <c>s = s.Cancel().New(target)</c>
        /// </summary>
        public Spring2 New(Vector2 target)
        {
            return New(Vector2.zero, target);
        }

        /// <summary>
        /// Start a new spring at <c>from</c> that settles on <c>target</c>. The returned handle replaces this one, so assign it: <c>s = s.Cancel().New(from, target)</c>
        /// </summary>
        public Spring2 New(Vector2 from, Vector2 target)
        {
            return Create(from, target);
        }

        /// <summary>
        /// Cancel the spring if it is still running. Returns an invalid handle.
        /// </summary>
        public Spring2 Cancel()
        {
            if (version != 0 && SpringManager.Instance != null)
            {
                SpringManager.Instance.CancelSpring(id, version);
            }
            return default;
        }

        // * Set Functions

        /// <summary>
        /// Move the target while the spring is running. The spring keeps its current value and velocity, so this is
        /// the way to chase a moving value (cursor, hover state, scroll position) without restarting the motion.
        /// </summary>
        public Spring2 SetTarget(Vector2 target)
        {
            if (Get(nameof(SetTarget), out SpringObject obj)) obj.target = target;
            return this;
        }

        /// <summary>Teleport the current value without touching velocity.</summary>
        public Spring2 SetValue(Vector2 value)
        {
            if (Get(nameof(SetValue), out SpringObject obj)) obj.value = value;
            return this;
        }

        /// <summary>Set the velocity in units per second. Use this to throw the spring, e.g. flicks and impacts.</summary>
        public Spring2 SetVelocity(Vector2 velocity)
        {
            if (Get(nameof(SetVelocity), out SpringObject obj)) obj.velocity = velocity;
            return this;
        }

        /// <summary>Add to the velocity. Repeated impulses stack.</summary>
        public Spring2 AddVelocity(Vector2 velocity)
        {
            if (Get(nameof(AddVelocity), out SpringObject obj)) obj.velocity += (Vector4)velocity;
            return this;
        }

        /// <summary>
        /// How hard the spring pulls toward the target. Higher is faster and snappier. Default 170.
        /// </summary>
        public Spring2 SetStiffness(float stiffness)
        {
            if (Get(nameof(SetStiffness), out SpringObject obj)) obj.stiffness = stiffness;
            return this;
        }

        /// <summary>
        /// How hard the spring resists motion. Low values bounce, high values ooze. Default 26, which is critically
        /// damped for the default stiffness. For a given stiffness k, <c>damping = 2 * sqrt(k)</c> is critically damped;
        /// below it overshoots, above it crawls in without overshoot.
        /// </summary>
        public Spring2 SetDamping(float damping)
        {
            if (Get(nameof(SetDamping), out SpringObject obj)) obj.damping = damping;
            return this;
        }

        /// <summary>
        /// Set stiffness and damping from a more intuitive pair: how many oscillations per second the undamped spring
        /// would make, and the damping ratio (0 = bounce forever, 1 = critically damped, above 1 = overdamped).
        /// </summary>
        public Spring2 SetFrequency(float frequencyHz, float dampingRatio)
        {
            if (Get(nameof(SetFrequency), out SpringObject obj)) SpringHandle.SetFrequency(obj, frequencyHz, dampingRatio);
            return this;
        }

        /// <summary>
        /// The spring is considered settled once both the distance to the target and the speed are below this. Default 0.001.
        /// Raise it for springs on large ranges (pixels, world units) so they complete sooner.
        /// </summary>
        public Spring2 SetRestThreshold(float threshold)
        {
            if (Get(nameof(SetRestThreshold), out SpringObject obj)) obj.restThreshold = threshold;
            return this;
        }

        public Spring2 SetUpdateType(SpringUpdateType updateType)
        {
            if (Get(nameof(SetUpdateType), out SpringObject obj)) obj.deltaTimeType = updateType;
            return this;
        }

        public Spring2 SetOnUpdate(System.Action<Vector2> onUpdate)
        {
            if (Get(nameof(SetOnUpdate), out SpringObject obj)) obj.onUpdate2 = onUpdate;
            return this;
        }

        public Spring2 SetOnComplete(System.Action onComplete)
        {
            if (Get(nameof(SetOnComplete), out SpringObject obj)) obj.onComplete = onComplete;
            return this;
        }

        public Spring2 SetOnCancel(System.Action onCancel)
        {
            if (Get(nameof(SetOnCancel), out SpringObject obj)) obj.onCancel = onCancel;
            return this;
        }

        private bool Get(string operation, out SpringObject obj) => SpringHandle.TryGet(id, version, DIMENSIONS, operation, out obj);

        //* static bits

        /// <summary>
        /// Start a spring from zero toward the target
        /// </summary>
        public static Spring2 Create(Vector2 target)
        {
            return Create(Vector2.zero, target);
        }

        /// <summary>
        /// Start a spring at <c>from</c> that settles on <c>target</c>
        /// </summary>
        public static Spring2 Create(Vector2 from, Vector2 target)
        {
            SpringObject obj = SpringManager.Instance.Allocate(from, target, DIMENSIONS);
            return obj == null ? default : new Spring2(obj.id, obj.version);
        }
    }
}
