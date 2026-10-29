using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Less3.Springs.Tests
{
    /// <summary>
    /// A handle carries its settings, callbacks and a resume point, so it keeps working after its spring settles or is cancelled.
    /// </summary>
    public class HandleReuseTests : SpringTestFixture
    {
        private const float FRAME = 1f / 60f;

        [Test]
        public void At_AllocatesNothing_UntilSetTarget()
        {
            Spring3 s = Spring3.At(Vector3.one).SetFrequency(2f, 1f).SetOnUpdate(_ => { });
            Assert.AreEqual(0, manager.ActiveCount);
            Assert.IsFalse(s.IsActive);
            Assert.AreEqual(Vector3.one, s.Value, "idle handle reports its resume point");

            s = s.SetTarget(Vector3.zero);
            Assert.AreEqual(1, manager.ActiveCount);
            Assert.IsTrue(s.IsActive);
        }

        [Test]
        public void SetTarget_AfterSettle_ReallocatesFromTheResumePoint_WithSameCallbacks()
        {
            var values = new List<float>();
            int completes = 0;
            Spring s = Spring.At(0f).SetOnUpdate(values.Add).SetOnComplete(() => completes++);

            s = s.SetTarget(1f);
            Step(FRAME, 600);
            Assert.AreEqual(1, completes);
            Assert.IsFalse(s.IsActive);
            Assert.AreEqual(1f, s.Value, "resume point is the target it settled on");

            values.Clear();
            s = s.SetTarget(2f);
            Assert.IsTrue(s.IsActive, "SetTarget on a settled handle allocates a new spring");
            Step(FRAME);
            Assert.AreEqual(1, values.Count, "the saved onUpdate is wired into the new spring");
            Assert.Greater(values[0], 1f);
            Assert.Less(values[0], 2f, "the new spring starts from where the old one settled");

            Step(FRAME, 600);
            Assert.AreEqual(2, completes, "onComplete fires once per arrival");
            Assert.AreEqual(2f, values[values.Count - 1]);
            AssertNoLeak();
        }

        [Test]
        public void Settings_SetBeforeAllocation_Apply()
        {
            float max = float.MinValue;
            Spring s = Spring.At(0f).SetStiffness(170f).SetDamping(5f).SetOnUpdate(v => max = Mathf.Max(max, v));
            s = s.SetTarget(1f);
            Step(FRAME, 600);
            Assert.Greater(max, 1.1f, "low damping configured before allocation should overshoot");
        }

        [Test]
        public void Settings_Survive_Reallocation()
        {
            float max = float.MinValue;
            Spring s = Spring.At(0f).SetStiffness(170f).SetDamping(5f).SetOnUpdate(v => max = Mathf.Max(max, v));
            s = s.SetTarget(1f);
            Step(FRAME, 600);
            Assert.IsFalse(s.IsActive);

            max = float.MinValue;
            s = s.SetTarget(2f);
            Step(FRAME, 600);
            Assert.Greater(max, 2.1f, "second spring should overshoot too");
        }

        [Test]
        public void Tuning_WhileMoving_WritesBothTheLiveSpringAndTheHandle()
        {
            Spring s = Spring.At(0f).SetDamping(5f);
            s = s.SetTarget(1f);
            s = s.SetDamping(26f);
            Assert.IsTrue(SpringManager.Instance.TryGetSpringObject(s.id, s.version, out SpringObject live));
            Assert.AreEqual(26f, live.damping, "live spring updated");

            Step(FRAME, 600);
            float max = float.MinValue;
            s = s.SetOnUpdate(v => max = Mathf.Max(max, v)).SetTarget(2f);
            Step(FRAME, 600);
            Assert.LessOrEqual(max, 2f + 1e-4f, "handle kept the critically damped setting for the next spring");
        }

        [Test]
        public void Cancel_KeepsSettings_AndResumesFromTheCancelledValue()
        {
            float last = float.NaN;
            int cancels = 0;
            Spring s = Spring.At(0f).SetOnUpdate(v => last = v).SetOnCancel(() => cancels++);
            s = s.SetTarget(1f);
            Step(FRAME, 10);
            float mid = last;
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);

            s = s.Cancel();
            Assert.AreEqual(1, cancels);
            Assert.IsFalse(s.IsActive);
            Assert.AreEqual(mid, s.Value, "resume point is where it was cancelled");
            AssertNoLeak();

            s = s.SetTarget(0f);
            Step(FRAME);
            Assert.Less(last, mid, "continues from the cancelled value, heading back to 0");
            Assert.Greater(last, 0f);
        }

        [Test]
        public void SetVelocity_OnIdleHandle_ThrowsAndReturns()
        {
            float max = float.MinValue;
            int completes = 0;
            Spring2 s = Spring2.At(Vector2.zero).SetOnUpdate(v => max = Mathf.Max(max, v.y)).SetOnComplete(() => completes++);
            s = s.AddVelocity(Vector2.up * 20f);
            Assert.IsTrue(s.IsActive);

            Step(FRAME, 600);
            Assert.Greater(max, 0.1f, "flew off in the velocity direction");
            Assert.AreEqual(Vector2.zero, s.Value, "and came back to the resume point");
            Assert.AreEqual(1, completes);
        }

        [Test]
        public void SetValue_OnIdleHandle_MovesTheResumePoint()
        {
            Spring s = Spring.At(0f).SetValue(5f);
            Assert.AreEqual(5f, s.Value);
            float first = float.NaN;
            s = s.SetOnUpdate(v => { if (float.IsNaN(first)) first = v; }).SetTarget(6f);
            Step(FRAME);
            Assert.Greater(first, 5f);
            Assert.Less(first, 6f);
        }

        [Test]
        public void DefaultHandle_SetTarget_StartsFromZero_WithDefaults()
        {
            Spring s = default;
            s = s.SetTarget(1f);
            Assert.IsTrue(s.IsActive);
            Assert.IsTrue(SpringManager.Instance.TryGetSpringObject(s.id, s.version, out SpringObject live));
            Assert.AreEqual(SpringObject.DEFAULT_STIFFNESS, live.stiffness);
            Assert.AreEqual(0f, live.value.x);
        }

        [Test]
        public void ForgottenAssignment_LeavesTheFieldIdle()
        {
            // documents the one rule: the result must be assigned
            Spring s = Spring.At(0f);
            s.SetTarget(1f);
            Assert.IsFalse(s.IsActive, "the field never learned about the allocation");
            Assert.AreEqual(1, manager.ActiveCount, "but the spring is running, orphaned");
        }
    }
}
