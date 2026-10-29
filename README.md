<img width="4096" height="819" alt="springCard" src="https://github.com/user-attachments/assets/42c6b25e-199e-44a9-aabd-657a45504933" />
A bare bones spring animation system for Unity. Sibling of [L3 Tween](https://github.com/Less3Design/L3-tween).

## Install
Add as a [git package](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-ui-giturl.html):
```
https://github.com/Less3Design/L3-spring.git
```

## Usage
```c#
private Spring3 moveSpring;

void Start()
{
    moveSpring = Spring3.At(transform.position)
        .SetFrequency(3f, 0.6f)
        .SetRestThreshold(0.01f)
        .SetOnUpdate(p => transform.position = p)
        .SetOnComplete(() => Debug.Log("arrived"));
}

void MoveTo(Vector3 point) => moveSpring = moveSpring.SetTarget(point);
```
- `Spring`, `Spring2`, `Spring3`, `Spring4` spring a `float`, `Vector2`, `Vector3`, `Vector4`.
- Handles are readonly structs. **Always assign the result** of every call.
- `SetTarget` on a moving spring changes its goal and keeps its momentum. On an idle spring it starts a new one from where the last stopped, with the same settings.
- A spring is done when it is within `restThreshold` of the target and nearly stopped. It snaps to the target, calls `onUpdate` once more, then `onComplete`.
- Idle springs hold no pool slot. Nothing to clean up.

## Tuning
```
acceleration = -stiffness * (value - target) - damping * velocity
```
`SetFrequency(hz, ratio)` is the easy way to set both:
- **hz**: how fast. Oscillations per second if there were no damping.
- **ratio**: how bouncy. `0` bounces forever, `1` no overshoot, `>1` sluggish.

Default is 170 stiffness, 26 damping (ratio 1).

`restThreshold` defaults to 0.001, which suits 0..1 values. Raise it for world units.

## Pool
256 springs, doubles as needed up to 4096. Change it in **Project Settings > Less3 > Spring**, or in code before the first spring:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ConfigureSprings() => SpringManager.Configure(initialPoolSize: 512, maxPoolSize: 8192);
```
