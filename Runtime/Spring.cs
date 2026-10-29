using UnityEngine;

namespace Less3.Springs
{
    /// <summary>
    /// A soft reference to a spring. The handle becomes invalid as soon as the spring settles on its target or is cancelled.
    /// Unlike a tween, a running spring can be retargeted at any time with <see cref="SetTarget"/> and keeps its momentum.
    /// </summary>
    public readonly struct Spring
    {
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
        public bool IsActive => TryGet(out _);

        /// <summary>Current value, or 0 when the handle is dead.</summary>
        public float Value => TryGet(out SpringObject obj) ? obj.value : 0f;
        /// <summary>Current velocity in units per second, or 0 when the handle is dead.</summary>
        public float Velocity => TryGet(out SpringObject obj) ? obj.velocity : 0f;
        /// <summary>Current target, or 0 when the handle is dead.</summary>
        public float Target => TryGet(out SpringObject obj) ? obj.target : 0f;

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
            return SpringManager.Instance.AllocateSpring(from, target);
        }

        /// <summary>
        /// Cancel the spring if it is still running. Returns an invalid handle.
        /// </summary>
        public Spring Cancel()
        {
            if (version != 0 && SpringManager.Instance != null)
            {
                SpringManager.Instance.CancelSpring(this);
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
            if (TryGet(out SpringObject obj, nameof(SetTarget))) obj.target = target;
            return this;
        }

        /// <summary>Teleport the current value without touching velocity.</summary>
        public Spring SetValue(float value)
        {
            if (TryGet(out SpringObject obj, nameof(SetValue))) obj.value = value;
            return this;
        }

        /// <summary>Set the velocity in units per second. Use this to throw the spring, e.g. flicks and impacts.</summary>
        public Spring SetVelocity(float velocity)
        {
            if (TryGet(out SpringObject obj, nameof(SetVelocity))) obj.velocity = velocity;
            return this;
        }

        /// <summary>Add to the velocity. Repeated impulses stack.</summary>
        public Spring AddVelocity(float velocity)
        {
            if (TryGet(out SpringObject obj, nameof(AddVelocity))) obj.velocity += velocity;
            return this;
        }

        /// <summary>
        /// How hard the spring pulls toward the target. Higher is faster and snappier. Default 170.
        /// </summary>
        public Spring SetStiffness(float stiffness)
        {
            if (TryGet(out SpringObject obj, nameof(SetStiffness))) obj.stiffness = stiffness;
            return this;
        }

        /// <summary>
        /// How hard the spring resists motion. Low values bounce, high values ooze. Default 26, which is critically
        /// damped for the default stiffness. For a given stiffness k, <c>damping = 2 * sqrt(k)</c> is critically damped;
        /// below it overshoots, above it crawls in without overshoot.
        /// </summary>
        public Spring SetDamping(float damping)
        {
            if (TryGet(out SpringObject obj, nameof(SetDamping))) obj.damping = damping;
            return this;
        }

        /// <summary>
        /// Set stiffness and damping from a more intuitive pair: how many oscillations per second the undamped spring
        /// would make, and the damping ratio (0 = bounce forever, 1 = critically damped, above 1 = overdamped).
        /// </summary>
        public Spring SetFrequency(float frequencyHz, float dampingRatio)
        {
            if (TryGet(out SpringObject obj, nameof(SetFrequency)))
            {
                float omega = 2f * Mathf.PI * frequencyHz;
                obj.stiffness = omega * omega;
                obj.damping = 2f * dampingRatio * omega;
            }
            return this;
        }

        /// <summary>
        /// The spring is considered settled once both the distance to the target and the velocity are below this. Default 0.001.
        /// Raise it for springs on large ranges (pixels, world units) so they complete sooner.
        /// </summary>
        public Spring SetRestThreshold(float threshold)
        {
            if (TryGet(out SpringObject obj, nameof(SetRestThreshold))) obj.restThreshold = threshold;
            return this;
        }

        public Spring SetUpdateType(SpringUpdateType updateType)
        {
            if (TryGet(out SpringObject obj, nameof(SetUpdateType))) obj.deltaTimeType = updateType;
            return this;
        }

        public Spring SetOnUpdate(System.Action<float> onUpdate)
        {
            if (TryGet(out SpringObject obj, nameof(SetOnUpdate))) obj.onUpdate = onUpdate;
            return this;
        }

        public Spring SetOnComplete(System.Action onComplete)
        {
            if (TryGet(out SpringObject obj, nameof(SetOnComplete))) obj.onComplete = onComplete;
            return this;
        }

        public Spring SetOnCancel(System.Action onCancel)
        {
            if (TryGet(out SpringObject obj, nameof(SetOnCancel))) obj.onCancel = onCancel;
            return this;
        }

        // * helpers

        private bool TryGet(out SpringObject obj)
        {
            if (version != 0 && SpringManager.Instance != null)
            {
                return SpringManager.Instance.TryGetSpringObject(this, out obj);
            }
            obj = null;
            return false;
        }

        private bool TryGet(out SpringObject obj, string operation)
        {
            if (TryGet(out obj)) return true;
            Debug.LogWarning($"Spring: {operation} failed to find SpringObject for id {id} version {version}");
            return false;
        }

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
            return SpringManager.Instance.AllocateSpring(from, target);
        }
    }
}
