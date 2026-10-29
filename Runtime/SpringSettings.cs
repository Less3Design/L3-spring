using UnityEngine;

namespace Less3.Springs
{
    /// <summary>
    /// Optional per project configuration for the spring pool.
    /// Lives in the consuming project (not the package) at <c>Assets/.../Resources/L3 Spring Settings.asset</c>
    /// so it survives package updates. Create it from Project Settings > Less3 > Spring.
    /// When no asset exists the defaults on SpringManager are used.
    /// </summary>
    public class SpringSettings : ScriptableObject
    {
        public const string RESOURCE_NAME = "L3 Spring Settings";

        [Tooltip("Number of springs allocated up front. Growth at runtime allocates a batch of objects, so size this to your usual peak.")]
        [Min(1)]
        public int initialPoolSize = SpringManager.DEFAULT_INITIAL_POOL_SIZE;

        [Tooltip("The pool doubles when it runs out, up to this many springs. Allocations past this fail with an error. Hard limit is 65536.")]
        [Min(1)]
        public int maxPoolSize = SpringManager.DEFAULT_MAX_POOL_SIZE;

        private void OnValidate()
        {
            maxPoolSize = Mathf.Clamp(maxPoolSize, 1, SpringManager.ABSOLUTE_MAX_POOL_SIZE);
            initialPoolSize = Mathf.Clamp(initialPoolSize, 1, maxPoolSize);
        }

        /// <summary>
        /// The settings asset in Resources, or null when the project has not created one.
        /// </summary>
        public static SpringSettings Load()
        {
            return Resources.Load<SpringSettings>(RESOURCE_NAME);
        }
    }
}
