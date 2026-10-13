using System.Collections.Generic;
using UnityEngine;

public class BucketInside : MonoBehaviour
{
    public Transform spawnPoint;   // tuščias objektas kaušo viduje
    public int capacity = 60;      // kiek blokų telpa kaušas

    readonly HashSet<DirtBlock> inside = new HashSet<DirtBlock>();

    public bool Contains(DirtBlock b) => inside.Contains(b);

    public bool IsFull
    {
        get
        {
            inside.RemoveWhere(b => !b || !b.gameObject.activeInHierarchy);
            return inside.Count >= capacity;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var b = other.GetComponent<DirtBlock>();
        if (b) inside.Add(b);
    }

    void OnTriggerExit(Collider other)
    {
        var b = other.GetComponent<DirtBlock>();
        if (b) inside.Remove(b);
    }
}