using NUnit.Framework;
using UnityEngine;

namespace Less3.Springs.Tests
{
    /// <summary>
    /// Base for EditMode tests. Builds a private SpringManager with a small pool, installs it as the
    /// instance the Spring API talks to, and steps it by hand so every test is deterministic.
    /// </summary>
    public abstract class SpringTestFixture
    {
        protected SpringManager manager;
        private GameObject go;
        private SpringManager previousInstance;

        protected virtual int InitialPoolSize => 8;
        protected virtual int MaxPoolSize => 64;

        [SetUp]
        public void SetUpManager()
        {
            previousInstance = SpringManager.Instance;
            CreateManager(InitialPoolSize, MaxPoolSize);
        }

        [TearDown]
        public void TearDownManager()
        {
            DestroyManager();
            SpringManager.InstallInstance(previousInstance);
        }

        protected void CreateManager(int initial, int max)
        {
            DestroyManager();
            go = new GameObject("Spring Test Manager") { hideFlags = HideFlags.HideAndDontSave };
            manager = go.AddComponent<SpringManager>();
            manager.Initialize(initial, max);
            SpringManager.InstallInstance(manager);
        }

        private void DestroyManager()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
                go = null;
                manager = null;
            }
        }

        /// <summary>One frame where scaled and unscaled time advance by the same amount.</summary>
        protected void Step(float deltaTime) => manager.Step(deltaTime, deltaTime);

        protected void Step(float deltaTime, int times)
        {
            for (int i = 0; i < times; i++) Step(deltaTime);
        }

        /// <summary>Every slot is back in the free ring and nothing is ticking.</summary>
        protected void AssertNoLeak()
        {
            Assert.AreEqual(0, manager.ActiveCount, "springs still active");
            Assert.AreEqual(manager.PoolSize, manager.FreeCount, "free ring does not account for every slot");
        }
    }
}
