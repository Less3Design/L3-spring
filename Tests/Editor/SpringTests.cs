using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Less3.Springs.Tests
{
    public class SpringTests : SpringTestFixture
    {
        private const float FRAME = 1f / 60f;

        [Test]
        public void Spring_SettlesOnTarget_AndCompletesOnce()
        {
            float last = float.NaN;
            int completes = 0;
            Spring s = Spring.Create(0f, 1f).SetOnUpdate(v => last = v).SetOnComplete(() => completes++);

            Step(FRAME, 600);// 10 seconds, far more than the default spring needs

            Assert.AreEqual(1f, last, "final onUpdate must land exactly on the target");
            Assert.AreEqual(1, completes);
            Assert.IsFalse(s.IsActive);
            AssertNoLeak();
        }

        [Test]
        public void Spring_AlreadyAtTarget_CompletesOnFirstTick()
        {
            int completes = 0;
            Spring.Create(5f, 5f).SetOnComplete(() => completes++);

            Step(FRAME);

            Assert.AreEqual(1, completes);
            AssertNoLeak();
        }

        [Test]
        public void CriticallyDamped_NeverOvershoots()
        {
            float max = float.MinValue;
            Spring.Create(0f, 1f).SetOnUpdate(v => max = Mathf.Max(max, v));

            Step(FRAME, 600);

            Assert.LessOrEqual(max, 1f + 1e-4f, "default spring should be critically damped");
        }

        [Test]
        public void Underdamped_Overshoots_ThenSettles()
        {
            float max = float.MinValue;
            int completes = 0;
            Spring.Create(0f, 1f).SetStiffness(170f).SetDamping(5f)
                .SetOnUpdate(v => max = Mathf.Max(max, v))
                .SetOnComplete(() => completes++);

            Step(FRAME, 600);

            Assert.Greater(max, 1.1f, "low damping should overshoot");
            Assert.AreEqual(1, completes, "should still settle eventually");
            AssertNoLeak();
        }

        [Test]
        public void SetTarget_KeepsTheSameHandle_AndMomentum()
        {
            float last = 0f;
            Spring s = Spring.Create(0f, 1f).SetOnUpdate(v => last = v);
            Step(FRAME, 10);
            float velocityBefore = s.Velocity;
            Assert.Greater(velocityBefore, 0f);

            s.SetTarget(-1f);
            Assert.IsTrue(s.IsActive);
            Assert.AreEqual(velocityBefore, s.Velocity, "retargeting must not reset velocity");

            Step(FRAME, 600);
            Assert.AreEqual(-1f, last);
            Assert.IsFalse(s.IsActive);
        }

        [Test]
        public void Cancel_FiresOnCancel_NotOnComplete_AndStopsUpdates()
        {
            int updates = 0, completes = 0, cancels = 0;
            Spring s = Spring.Create(1f)
                .SetOnUpdate(_ => updates++)
                .SetOnComplete(() => completes++)
                .SetOnCancel(() => cancels++);

            Step(FRAME, 2);
            s = s.Cancel();
            Step(FRAME, 600);

            Assert.AreEqual(2, updates);
            Assert.AreEqual(0, completes);
            Assert.AreEqual(1, cancels);
            Assert.IsFalse(s.IsActive);
            AssertNoLeak();
        }

        [Test]
        public void HugeDeltaTime_DoesNotExplode()
        {
            float last = float.NaN;
            Spring.Create(0f, 1f).SetStiffness(5000f).SetDamping(20f).SetOnUpdate(v => last = v);

            Step(1f, 20);// 1 second hitches against a very stiff spring

            Assert.IsFalse(float.IsNaN(last) || float.IsInfinity(last));
            Assert.Less(Mathf.Abs(last), 100f, "integration blew up");
        }

        [Test]
        public void Pool_Grows_UpToMax_ThenFails()
        {
            CreateManager(2, 4);
            var springs = new List<Spring>();
            for (int i = 0; i < 4; i++) springs.Add(Spring.Create(1f));
            Assert.AreEqual(4, manager.PoolSize);
            Assert.AreEqual(4, manager.ActiveCount);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No free spring slots"));
            Spring overflow = Spring.Create(1f);
            Assert.IsFalse(overflow.IsActive);

            foreach (Spring s in springs) s.Cancel();
            AssertNoLeak();
        }
    }
}
