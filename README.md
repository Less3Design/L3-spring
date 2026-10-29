# L3 Spring

A bare bones spring animation system for Unity. Sibling of [L3 Tween](https://github.com/Less3Design/L3-tween) with the same handle, pool and callback layout.

## Install
Add to your Unity project as a [git package using the package manager](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-ui-giturl.html).

## Usage
A spring handle carries its own settings, so you configure it once and then just move the target:
```c#
private Spring3 moveSpring;

void Start()
{
    // configured, idle, nothing allocated yet
    moveSpring = Spring3.At(transform.position)
        .SetFrequency(3f, 0.6f)// 3 Hz with a bit of bounce
        .SetRestThreshold(0.01f)
        .SetOnUpdate(p => transform.position = p)
        .SetOnComplete(() => Debug.Log("arrived"));
}

void MoveTo(Vector3 point) => moveSpring = moveSpring.SetTarget(point);
```
`SetTarget` does the right thing either way. If the spring is still moving it keeps its value and velocity and heads for the new point, which is why interruptions look natural. If it has settled (or was cancelled) a fresh spring is allocated from where the last one stopped, with the same settings and callbacks. Nothing is allocated while the spring is idle, so there is nothing to clean up.

`Spring`, `Spring2`, `Spring3` and `Spring4` are springs for `float`, `Vector2`, `Vector3` and `Vector4`, with the same API.

The handles are readonly structs and every call returns the updated handle, so **always assign the result**: `s = s.SetTarget(x)`, `s = s.Cancel()`. If you forget, the spring still runs but your field never learns about it, and the next `SetTarget` starts a second spring instead of retargeting the first.

`spring.IsActive` is true while the spring is moving. `Create(from, target)` is a shortcut for `At(from).SetTarget(target)` when you just want a one shot with default settings.

A spring differs from a tween in two ways:
- There is no duration. The spring runs until its value and velocity are both within `restThreshold` of the target, then snaps exactly onto the target, calls `onUpdate` one last time, and calls `onComplete`.
- A running spring can be retargeted with `SetTarget` and pushed with `SetVelocity` / `AddVelocity` without being restarted. That is the whole point of springs: interruptions look natural because the motion carries its momentum into the new goal.

Here is all the state we maintain:
```csharp
public class SpringObject
{
    public ushort id;
    public ushort version;
    public bool active;
    public int activeIndex;

    public byte dimensions;// 1..4, picks the onUpdate
    public Vector4 value;
    public Vector4 velocity;
    public Vector4 target;
    public float stiffness;
    public float damping;
    public float restThreshold;
    public SpringUpdateType deltaTimeType;

    public Action<float> onUpdate1;
    public Action<Vector2> onUpdate2;
    public Action<Vector3> onUpdate3;
    public Action<Vector4> onUpdate4;
    public Action onComplete;
    public Action onCancel;
}
```
Every spring is backed by a Vector4; a float spring just leaves three components at zero. The integrator does not branch on the dimension, only the callback dispatch does.

## How a spring works
Every step the spring applies one rule: acceleration is proportional to how far it is from the target, pointing back at the target, minus a drag proportional to its velocity.

```
acceleration = -stiffness * (value - target) - damping * velocity
velocity    += acceleration * dt
value       += velocity * dt
```

- **stiffness** (k) is how hard it pulls. Higher is faster and snappier.
- **damping** (c) is how hard it resists motion. The important number is the ratio `c / (2 * sqrt(k))`:
  - below 1, **underdamped**: it overshoots and bounces before settling.
  - exactly 1, **critically damped**: the fastest approach with no overshoot. This is the default (170 / 26).
  - above 1, **overdamped**: no overshoot but it crawls in.

`SetFrequency(hz, ratio)` sets both from the two numbers people actually think in: how many bounces per second the undamped spring would make, and the damping ratio above.

The integrator is semi implicit Euler in sub steps of at most 1/120 s, and a frame longer than 16 sub steps (133 ms) is clamped, so a hitch against a very stiff spring slows the motion instead of blowing it up. There is no closed form or job system because the goal is to keep the code as simple as possible.

## Rest threshold
A spring never mathematically reaches its target, so completion is a threshold check on the distance to the target and the speed (the vector magnitudes, for vector springs). The default of 0.001 suits 0..1 values. If you spring pixels or world units, raise it with `SetRestThreshold` so the spring completes as soon as the motion is invisible.

## Pool size
The pool starts at 256 springs and doubles whenever it runs out, up to a max of 4096 (hard limit 65536, ids are ushorts). Configure it from **Project Settings > Less3 > Spring**, which creates `Assets/Resources/L3 Spring Settings.asset` in your project. Or from code, before the first spring:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ConfigureSprings() => SpringManager.Configure(initialPoolSize: 512, maxPoolSize: 8192);
```
