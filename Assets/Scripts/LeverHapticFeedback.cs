using UnityEngine;

// Short pulses describe lever travel; the end stop has a separate, hysteretic click.
public sealed class LeverHapticFeedback
{
    public const float StartTravel = 0.55f;
    public const float LimitTravel = 0.96f;
    public const float ReleaseTravel = 0.88f;
    bool atLimit;
    float untilPulse, untilLimitClick;

    public void Reset()
    {
        atLimit = false;
        untilPulse = untilLimitClick = 0;
    }

    public bool Step(float travel, bool held, float dt, out float amplitude, out float duration)
    {
        amplitude = duration = 0;
        if (!held) { Reset(); return false; }
        if (dt <= 0) return false;
        travel = Mathf.Clamp01(travel);
        untilPulse -= dt;
        untilLimitClick = Mathf.Max(0, untilLimitClick - dt);
        if (travel < ReleaseTravel) atLimit = false;
        if (!atLimit && travel >= LimitTravel)
        {
            atLimit = true;
            if (untilLimitClick <= 0)
            {
                amplitude = 0.65f; duration = 0.085f;
                untilLimitClick = 0.2f; untilPulse = 0.24f;
                return true;
            }
        }
        if (travel < StartTravel) { untilPulse = 0; return false; }
        if (untilPulse > 0) return false;
        amplitude = atLimit ? 0.22f : Mathf.Lerp(0.06f, 0.24f, Mathf.InverseLerp(StartTravel, LimitTravel, travel));
        duration = atLimit ? 0.035f : 0.045f;
        untilPulse = atLimit ? 0.24f : 0.08f;
        return true;
    }
}
