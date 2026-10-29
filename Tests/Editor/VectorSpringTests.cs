using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Less3.Springs.Tests
{
    /// <summary>
    /// Spring2 / Spring3 / Spring4 share the pool with Spring. One slot, one onUpdate per frame and one onComplete per motion.
    /// </summary>
    public class VectorSpringTests : SpringTestFixture
    {
        private const float FRAME = 1f / 60f;

        [Test]
        public void Spring3_SettlesOnTarget_WithOneCallbackPerFrame_AndOneComplete()
        {
            Vector3 target = new Vector3(1f, 2f, 3f);
            var values = new List<Vector3>();
            int completes = 0;
            Spring3 s = Spring3.Create(Vector3.zero, target).SetOnUpdate(values.Add).SetOnComplete(() => completes++);

            Assert.AreEqual(1, manager.ActiveCount, "a vector spring must take exactly one pool slot");
            Step(FRAME, 10);
            Assert.AreEqual(10, values.Count, "exactly one onUpdate per frame");

            Step(FRAME, 590);
            Assert.AreEqual(target, values[values.Count - 1], "final onUpdate must land exactly on the target");
            Assert.AreEqual(1, completes);
            Assert.IsFalse(s.IsActive);
            AssertNoLeak();
        }

        [Test]
        public void Spring3_MovesInAStraightLine_WhenStartedFromRest()
        {
            // shared stiffness and damping across components means every axis follows the same curve,
            // so the value stays on the segment between from and target
            Vector3 from = new Vector3(-1f, 4f, 0.5f);
            Vector3 target = new Vector3(3f, -2f, 2f);
            Vector3 dir = (target - from).normalized;
            float maxOffLine = 0f;
            Spring3.Create(from, target).SetOnUpdate(v =>
            {
                Vector3 rel = v - from;
                Vector3 onLine = Vector3.Dot(rel, dir) * dir;
                maxOffLine = Mathf.Max(maxOffLine, (rel - onLine).magnitude);
            });

            Step(FRAME, 300);

            Assert.Less(maxOffLine, 1e-4f);
        }

        [Test]
        public void Spring2_SetTarget_KeepsHandleAndMomentum()
        {
            Vector2 last = Vector2.zero;
            Spring2 s = Spring2.Create(Vector2.zero, Vector2.right).SetOnUpdate(v => last = v);
            Step(FRAME, 10);
            Vector2 velocityBefore = s.Velocity;
            Assert.Greater(velocityBefore.x, 0f);

            s.SetTarget(Vector2.up * 2f);
            Assert.IsTrue(s.IsActive);
            Assert.AreEqual(velocityBefore, s.Velocity, "retargeting must not reset velocity");

            Step(FRAME, 600);
            Assert.AreEqual(Vector2.up * 2f, last);
            Assert.IsFalse(s.IsActive);
            AssertNoLeak();
        }

        [Test]
        public void Spring4_AddVelocity_ThrowsAllComponents()
        {
            Vector4 last = Vector4.zero;
            Spring4 s = Spring4.Create(Vector4.zero, Vector4.zero).SetOnUpdate(v => last = v);
            s.AddVelocity(new Vector4(1f, 2f, 3f, 4f) * 10f);

            Step(FRAME);

            Assert.Greater(last.x, 0f);
            Assert.Greater(last.w, last.x, "components scale with their velocity");
            Step(FRAME, 600);
            Assert.AreEqual(Vector4.zero, last);
            Assert.IsFalse(s.IsActive);
        }

        [Test]
        public void RestThreshold_IsOnTheVectorMagnitude_NotPerAxis()
        {
            // 0.0008 on each of three axes is 0.0014 in magnitude, which is over the 0.001 default
            Spring3 s = Spring3.Create(Vector3.one * 0.0008f, Vector3.zero).SetStiffness(0f).SetDamping(0f);
            Step(FRAME);
            Assert.IsTrue(s.IsActive, "per axis the spring would be at rest, by magnitude it is not");

            Spring s1 = Spring.Create(0.0008f, 0f).SetStiffness(0f).SetDamping(0f);
            Step(FRAME);
            Assert.IsFalse(s1.IsActive, "a single axis at 0.0008 is at rest");
        }

        [Test]
        public void MixedDimensions_ShareThePool_AndHandlesDoNotCrossOver()
        {
            Spring a = Spring.Create(0f, 1f);
            Spring3 b = Spring3.Create(Vector3.zero, Vector3.one);
            Assert.AreEqual(2, manager.ActiveCount);

            // a Spring3 handle forged from the float spring's id must not resolve
            Spring3 forged = new Spring3(a.id, a.version);
            Assert.IsFalse(forged.IsActive);
            Assert.IsTrue(a.IsActive);
            Assert.IsTrue(b.IsActive);

            Step(FRAME, 600);
            AssertNoLeak();
        }

        [Test]
        public void Cancel_FiresOnCancel_AndFreesTheSlot()
        {
            int cancels = 0, completes = 0;
            Spring3 s = Spring3.Create(Vector3.zero, Vector3.one).SetOnCancel(() => cancels++).SetOnComplete(() => completes++);
            Step(FRAME, 2);
            s = s.Cancel();
            Step(FRAME, 100);

            Assert.AreEqual(1, cancels);
            Assert.AreEqual(0, completes);
            Assert.IsFalse(s.IsActive);
            AssertNoLeak();
        }
    }
}
