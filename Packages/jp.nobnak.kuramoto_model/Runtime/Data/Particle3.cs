using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuramotoModel {

    [GenerateHLSL(needAccessors:false)]
    public struct Particle3 {
        public int activity;
        public float3 pos;
    }
}
