using UnityEngine;

namespace Less3.Springs
{
    /// <summary>
    /// A float spring handle. The handle carries its tuning, callbacks and a resume point, so it can be configured
    /// before anything runs and reused after the spring settles: SetTarget on a settled (or cancelled) handle allocates a
    /// fresh spring from where the last one stopped, with the same settings. Every call returns the updated handle, so
    /// always assign the result: <c>s = s.SetTarget(x)</c>.
    /// While a spring is moving the handle is "active"; SetTarget then just moves the goal and the motion keeps its momentum.
    /// </summary>
    public readonly struct Spring
    {
        private const byte DIMENSIONS = 1;

        public readonly ushort id;
        public readonly ushort version;// 0 = nothing allocated
        private readonly SpringConfig config;
        private readonly System.Action<float> onUpdate;
        private readonly float resume;// where the next spring starts when this handle is not active

        public Spring(ushort id, ushort version) : this(id, version, default, null, 0f) { }

        private Spring(ushort id, ushort version, SpringConfig config, System.Action<float> onUpdate, float resume)
        {
            this.id = id;
            this.version = version;
            this.config = config;
            this.onUpdate = onUpdate;
            this.resume = resume;
        }

        public int key { get { return (version << 16) | id; } }

        /// <summary>
        /// True while the spring this handle points to is still moving.
        /// </summary>
        public bool IsActive => SpringHandle.TryGet(id, version, DIMENSIONS, out _);

        /// <summary>Current value while moving, otherwise the point the next spring will start from.</summary>
        public float Value => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? obj.value.x : resume;
        /// <summary>Current velocity in units per second, or zero when not moving.</summary>
        public float Velocity => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? obj.velocity.x : 0f;
        /// <summary>Current target while moving, otherwise the resume point.</summary>
        public float Target => SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj) ? obj.target.x : resume;

        // * creation

        /// <summary>
        /// A configured but idle handle resting at <c>value</c>. Nothing is allocated until the first SetTarget / SetVelocity.
        /// This is the one to keep in a field: configure it once, then <c>s = s.SetTarget(x)</c> whenever the goal changes.
        /// </summary>
        public static Spring At(float value)
        {
            return new Spring(0, 0, SpringConfig.Default, null, value);
        }

        /// <summary>
        /// Start a spring from zero toward the target with default settings.
        /// </summary>
        public static Spring Create(float target)
        {
            return At(0f).SetTarget(target);
        }

        /// <summary>
        /// Start a spring at <c>from</c> that settles on <c>target</c> with default settings.
        /// </summary>
        public static Spring Create(float from, float target)
        {
            return At(from).SetTarget(target);
        }

        /// <summary>
        /// Start a new spring from zero toward the target, keeping this handle's settings and callbacks.
        /// Does not touch a spring this handle may still be driving; Cancel first if you want that: <c>s = s.Cancel().New(x)</c>
        /// </summary>
        public Spring New(float target)
        {
            return New(0f, target);
        }

        /// <summary>
        /// Start a new spring at <c>from</c> toward <c>target</c>, keeping this handle's settings and callbacks.
        /// </summary>
        public Spring New(float from, float target)
        {
            return new Spring(0, 0, config, onUpdate, from).SetTarget(target);
        }

        /// <summary>
        /// Stop the spring if it is moving. The returned handle keeps its settings and resumes from the value it was
        /// cancelled at, so a later SetTarget continues from there.
        /// </summary>
        public Spring Cancel()
        {
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj))
            {
                float at = obj.value.x;
                SpringManager.Instance.CancelSpring(id, version);
                return new Spring(0, 0, config, onUpdate, at);
            }
            return new Spring(0, 0, config, onUpdate, resume);
        }

        // * motion

        /// <summary>
        /// Move the goal. While the spring is moving it keeps its value and velocity and simply heads for the new target.
        /// When it is not moving a new spring is allocated from the resume point with this handle's settings.
        /// </summary>
        public Spring SetTarget(float target)
        {
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj))
            {
                obj.target = new Vector4(target, 0f, 0f, 0f);
                return new Spring(id, version, config, onUpdate, target);
            }
            return Start(resume, target, Vector4.zero);
        }

        /// <summary>Teleport the current value without touching velocity. On an idle handle this moves the resume point.</summary>
        public Spring SetValue(float value)
        {
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj))
            {
                obj.value = new Vector4(value, 0f, 0f, 0f);
                return this;
            }
            return new Spring(id, version, config, onUpdate, value);
        }

        /// <summary>
        /// Set the velocity in units per second. Use this to throw the spring, e.g. flicks and impacts.
        /// On an idle handle this starts a spring at the resume point that flies off and comes back.
        /// </summary>
        public Spring SetVelocity(float velocity)
        {
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj))
            {
                obj.velocity = new Vector4(velocity, 0f, 0f, 0f);
                return this;
            }
            return Start(resume, resume, new Vector4(velocity, 0f, 0f, 0f));
        }

        /// <summary>Add to the velocity. Repeated impulses stack. On an idle handle this behaves like SetVelocity.</summary>
        public Spring AddVelocity(float velocity)
        {
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj))
            {
                obj.velocity += new Vector4(velocity, 0f, 0f, 0f);
                return this;
            }
            return Start(resume, resume, new Vector4(velocity, 0f, 0f, 0f));
        }

        private Spring Start(float from, float target, Vector4 velocity)
        {
            SpringObject obj = SpringHandle.Allocate(new Vector4(from, 0f, 0f, 0f), new Vector4(target, 0f, 0f, 0f), DIMENSIONS, config);
            if (obj == null) return this;
            obj.velocity = velocity;
            obj.onUpdate1 = onUpdate;
            return new Spring(obj.id, obj.version, config.Resolved, onUpdate, target);
        }

        // * tuning. each writes the handle's config and, if a spring is moving, the live object too.

        /// <summary>
        /// How hard the spring pulls toward the target. Higher is faster and snappier. Default 170.
        /// </summary>
        public Spring SetStiffness(float stiffness)
        {
            SpringConfig c = config.Resolved;
            c.stiffness = stiffness;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.stiffness = stiffness;
            return new Spring(id, version, c, onUpdate, resume);
        }

        /// <summary>
        /// How hard the spring resists motion. Low values bounce, high values ooze. Default 26, which is critically
        /// damped for the default stiffness. For a given stiffness k, <c>damping = 2 * sqrt(k)</c> is critically damped;
        /// below it overshoots, above it crawls in without overshoot.
        /// </summary>
        public Spring SetDamping(float damping)
        {
            SpringConfig c = config.Resolved;
            c.damping = damping;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.damping = damping;
            return new Spring(id, version, c, onUpdate, resume);
        }

        /// <summary>
        /// Set stiffness and damping from a more intuitive pair: how many oscillations per second the undamped spring
        /// would make, and the damping ratio (0 = bounce forever, 1 = critically damped, above 1 = overdamped).
        /// </summary>
        public Spring SetFrequency(float frequencyHz, float dampingRatio)
        {
            SpringHandle.ToFrequency(frequencyHz, dampingRatio, out float stiffness, out float damping);
            SpringConfig c = config.Resolved;
            c.stiffness = stiffness;
            c.damping = damping;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj))
            {
                obj.stiffness = stiffness;
                obj.damping = damping;
            }
            return new Spring(id, version, c, onUpdate, resume);
        }

        /// <summary>
        /// The spring is considered settled once both the distance to the target and the speed are below this. Default 0.001.
        /// Raise it for springs on large ranges (pixels, world units) so they complete sooner.
        /// </summary>
        public Spring SetRestThreshold(float threshold)
        {
            SpringConfig c = config.Resolved;
            c.restThreshold = threshold;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.restThreshold = threshold;
            return new Spring(id, version, c, onUpdate, resume);
        }

        public Spring SetUpdateType(SpringUpdateType updateType)
        {
            SpringConfig c = config.Resolved;
            c.updateType = updateType;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.deltaTimeType = updateType;
            return new Spring(id, version, c, onUpdate, resume);
        }

        /// <summary>Called every frame the spring moves, and once more exactly on the target when it settles.</summary>
        public Spring SetOnUpdate(System.Action<float> onUpdate)
        {
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.onUpdate1 = onUpdate;
            return new Spring(id, version, config, onUpdate, resume);
        }

        /// <summary>Called each time the spring settles on its target, after the final onUpdate.</summary>
        public Spring SetOnComplete(System.Action onComplete)
        {
            SpringConfig c = config.Resolved;
            c.onComplete = onComplete;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.onComplete = onComplete;
            return new Spring(id, version, c, onUpdate, resume);
        }

        public Spring SetOnCancel(System.Action onCancel)
        {
            SpringConfig c = config.Resolved;
            c.onCancel = onCancel;
            if (SpringHandle.TryGet(id, version, DIMENSIONS, out SpringObject obj)) obj.onCancel = onCancel;
            return new Spring(id, version, c, onUpdate, resume);
        }
    }
}
