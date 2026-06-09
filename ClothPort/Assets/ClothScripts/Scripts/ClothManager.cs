using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class ClothManager : MonoBehaviour
{
    public static ClothManager instance { get; private set; }

    private List<GPUCloth> mClothList = new();

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
    }

    public void RegisterCloth(GPUCloth cloth) => mClothList.Add(cloth);
    public void UnregisterCloth(GPUCloth cloth) => mClothList.Remove(cloth);

    void FixedUpdate()
    {
        foreach (var cloth in mClothList)
            cloth.Simulate(Time.fixedDeltaTime);

    }


}
