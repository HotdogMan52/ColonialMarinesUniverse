using Content.Client.Trigger.Systems;
using Robust.Client.Animations;
using Robust.Shared.Audio;

namespace Content.Client.Trigger.Components;

[RegisterComponent]
[Access(typeof(TimerTriggerVisualizerSystem))]
public sealed partial class TimerTriggerVisualsComponent : Component
{
    /// <summary>
    /// The key used to index the priming animation.
    /// </summary>
    [ViewVariables]
    public const string AnimationKey = "priming_animation";

    /// <summary>
    /// The RSI state used while the device has not been primed.
    /// </summary>
    [DataField]
    public string UnprimedSprite = "icon";

    /// <summary>
    /// The RSI state used when the device is primed.
    /// </summary>
    [DataField]
    public string PrimingSprite = "primed";

    /// <summary>
    /// The sound played when the device is primed.
    /// Not VVWrite-able because it's only used at component init to construct the priming animation.
    /// </summary>
    [DataField, ViewVariables]
    public SoundSpecifier? PrimingSound;

    /// <summary>
    /// Keeps track of the primed state and plays its optional sound.
    /// The looping sprite state is handled by <see cref="TimerTriggerVisualizerSystem"/>.
    /// </summary>
    [ViewVariables]
    public Animation PrimingAnimation = default!;
}
