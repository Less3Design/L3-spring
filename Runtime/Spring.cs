using UnityEngine;

namespace Less3.Springs
{
    /// <summary>
    /// A soft reference to a float spring. The handle becomes invalid as soon as the spring settles on its target or is cancelled.
    /// Unlike a tween, a running spring can be retargeted at any time with <see cref="SetTarget"/> and keeps its momentum.
    /// See <see cref="Spring2"/>, <see cref="Spring3"/> and <see cref="Spring4"/> for vector springs with the same API.
    /// </summary>
    public readonly struct Spring
    {
        private const byte DIMENSIONS = 1;

        public readonly ushort id;
        public readonly ushort version;// 0 = invalid

        public Spring(ushort id, ushort version)
        {
            this.id = id;
            this.version = version;
        }

        public int key { get { return (version << 16) | id; } }

        /// <summary>
        /// True while the spring this handle points to is still moving.
        /// </summary>
        public bool IsActive => SpringHandle.TryGet(id, version, DIMENSIONS, out _);

        /// <summary>Current value, or 0 when the handle is dead.</summary>
        public float Value => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? obj.value.x : 0f;
        /// <summary>Current velocity in units per second, or 0 when the handle is dead.</summary>
        public float Velocity => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? obj.velocity.x : 0f;
        /// <summary>Current target, or 0 when the handle is dead.</summary>
        public float Target => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? obj.target.x : 0f;

        /// <summary>
        /// Start a new spring from 0 toward the target. The returned handle replaces this one, so assign it: <c>s = s.Cancel().New(1f)</c>
        /// </summary>
        public Spring New(float target)
        {
            return New(0f, target);
        }

        /// <summary>
        /// Start a new spring at <c>from</c> that settles on <c>target</c>. The returned handle replaces this one, so assign it: <c>s = s.Cancel().New(0f, 1f)</c>
        /// </summary>
        public Spring New(float from, float target)
        {
            return Create(from, target);
        }

        /// <summary>
        /// Cancel the spring if it is still running. Returns an invalid handle.
        /// </summary>
        public Spring Cancel()
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
        public Spring SetTarget(float target)
        {
            if (Get(nameof(SetTarget), out SpringObject obj)) obj.target.x = target;
            return this;
        }

        /// <summary>Teleport the current value without touching velocity.</summary>
        public Spring SetValue(float value)
        {
            if (Get(nameof(SetValue), out SpringObject obj)) obj.value.x = value;
            return this;
        }

        /// <summary>Set the velocity in units per second. Use this to throw the spring, e.g. flicks and impacts.</summary>
        public Spring SetVelocity(float velocity)
        {
            if (Get(nameof(SetVelocity), out SpringObject obj)) obj.velocity.x = velocity;
            return this;
        }

        /// <summary>Add to the velocity. Repeated impulses stack.</summary>
        public Spring AddVelocity(float velocity)
        {
            if (Get(nameof(AddVelocity), out SpringObject obj)) obj.velocity.x += velocity;
            return this;
        }

        /// <summary>
        /// How hard the spring pulls toward the target. Higher is faster and snappier. Default 170.
        /// </summary>
        public Spring SetStiffness(float stiffness)
        {
            if (Get(nameof(SetStiffness), out SpringObject obj)) obj.stiffness = stiffness;
            return this;
        }

        /// <summary>
        /// How hard the spring resists motion. Low values bounce, high values ooze. Default 26, which is critically
        /// damped for the default stiffness. For a given stiffness k, <c>damping = 2 * sqrt(k)</c> is critically damped;
        /// below it overshoots, above it crawls in without overshoot.
        /// </summary>
        public Spring SetDamping(float damping)
        {
            if (Get(nameof(SetDamping), out SpringObject obj)) obj.damping = damping;
            return this;
        }

        /// <summary>
        /// Set stiffness and damping from a more intuitive pair: how many oscillations per second the undamped spring
        /// would make, and the damping ratio (0 = bounce forever, 1 = critically damped, above 1 = overdamped).
        /// </summary>
        public Spring SetFrequency(float frequencyHz, float dampingRatio)
        {
            if (Get(nameof(SetFrequency), out SpringObject obj)) SpringHandle.SetFrequency(obj, frequencyHz, dampingRatio);
            return this;
        }

        /// <summary>
        /// The spring is considered settled once both the distance to the target and the velocity are below this. Default 0.001.
        /// Raise it for springs on large ranges (pixels, world units) so they complete sooner.
        /// </summary>
        public Spring SetRestThreshold(float threshold)
        {
            if (Get(nameof(SetRestThreshold), out SpringObject obj)) obj.restThreshold = threshold;
            return this;
        }

        public Spring SetUpdateType(SpringUpdateType updateType)
        {
            if (Get(nameof(SetUpdateType), out SpringObject obj)) obj.deltaTimeType = updateType;
            return this;
        }

        public Spring SetOnUpdate(System.Action<float> onUpdate)
        {
            if (Get(nameof(SetOnUpdate), out SpringObject obj)) obj.onUpdate1 = onUpdate;
            return this;
        }

        public Spring SetOnComplete(System.Action onComplete)
        {
            if (Get(nameof(SetOnComplete), out SpringObject obj)) obj.onComplete = onComplete;
            return this;
        }

        public Spring SetOnCancel(System.Action onCancel)
        {
            if (Get(nameof(SetOnCancel), out SpringObject obj)) obj.onCancel = onCancel;
            return this;
        }

        private bool Get(string operation, out SpringObject obj) => SpringHandle.TryGet(id, version, DIMENSIONS, operation, out obj);

        //* static bits

        /// <summary>
        /// Start a spring from 0 toward the target
        /// </summary>
        public static Spring Create(float target)
        {
            return Create(0f, target);
        }

        /// <summary>
        /// Start a spring at <c>from</c> that settles on <c>target</c>
        /// </summary>
        public static Spring Create(float from, float target)
        {
            SpringObject obj = SpringManager.Instance.Allocate(new Vector4(from, 0f, 0f, 0f), new Vector4(target, 0f, 0f, 0f), DIMENSIONS);
            return obj == null ? default : new Spring(obj.id, obj.version);
        }
    }
}
