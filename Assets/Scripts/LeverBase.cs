using UnityEngine;

public abstract class LeverBase : MonoBehaviour
{
    [Range(-1, 1)] public float Forward;
    [Range(-1, 1)] public float Side;
}