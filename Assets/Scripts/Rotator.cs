using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Rotator : MonoBehaviour {

    public Tuner tuner = new();

    #region unity
    void Update() {
        var dt = Time.deltaTime;
        var rot = quaternion.Euler(tuner.rotationSpeed * (CIRCLE_IN_RADIAN * dt));
        transform.localRotation *= rot;
    }
    #endregion

    #region declarations
    public static readonly float CIRCLE_IN_RADIAN = 2f * math.PI;
    [System.Serializable]
    public class Tuner {
        public float3 rotationSpeed;
    }
    #endregion
}