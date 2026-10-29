using UnityEngine;
using System;

namespace Less3.Springs
{
    /// <summary>
    /// The pooled state behind a Spring handle. A damped harmonic oscillator with unit mass:
    /// every step the spring is pulled toward its target by stiffness and slowed by damping.
    /// </summary>
    public class SpringObject
    {
        public ushort id;// maps to the index in the spring pool
        public ushort version;// increments each time the spring is released, so stale handles stop matching. never 0.
        public bool active;
        public int activeIndex = -1;// position in the manager's active list, -1 when not running

        public float value;
        public float velocity;
        public float target;
        public float stiffness;// how hard the spring pulls toward the target (k)
        public float damping;// how hard it resists motion (c)
        public float restThreshold;// at rest once both |value - target| and |velocity| are under this
        public SpringUpdateType deltaTimeType;

        public Action<float> onUpdate;
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

        public void Allocate(float from, float target)
        {
            active = true;
            this.value = from;
            this.target = target;
            this.velocity = 0f;

            // default values
            this.stiffness = DEFAULT_STIFFNESS;
            this.damping = DEFAULT_DAMPING;
            this.restThreshold = DEFAULT_REST_THRESHOLD;
            this.deltaTimeType = SpringUpdateType.DeltaTime;
            onUpdate = null;
            onComplete = null;
            onCancel = null;
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
                    float acceleration = -stiffness * (value - target) - damping * velocity;
                    velocity += acceleration * h;
                    value += velocity * h;
                }
            }

            bool atRest = Mathf.Abs(value - target) < restThreshold && Mathf.Abs(velocity) < restThreshold;
            if (atRest)
            {
                value = target;
                velocity = 0f;
            }

            try
            {
                onUpdate?.Invoke(value);
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
            onUpdate = null;
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
}
