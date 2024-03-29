﻿using KuramotoModel;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[ExecuteInEditMode]
public class Particle3Simulator : MonoBehaviour {
    public static readonly int P_ParticlesCount = Shader.PropertyToID("_ParticlesCount");
    public static readonly int P_Particles = Shader.PropertyToID("_Particles");

    public static readonly int P_Phases = Shader.PropertyToID("_Phases");
    public static readonly int P_Size = Shader.PropertyToID("_Size");

    public static readonly int P_Highlight = Shader.PropertyToID("_Highlight");
    public static readonly int P_Shadow = Shader.PropertyToID("_Shadow");
    public static readonly int P_CohPhi = Shader.PropertyToID("_CohPhi");

    public const float CIRCLE_IN_RADIAN = 2f * Mathf.PI;
    public const float RADIAN_TO_NORMALIZED = 1f / CIRCLE_IN_RADIAN;


    public Presets presets = new();
    public Kuramoto_Particle3.Tuner tuner = new();
    public Kuramoto_Particle3.Presets settings = new();

    bool invalid;
    Rect windowRect;

    Random rand;
    Kuramoto_Particle3 particle3;

    #region Unity
    private void OnEnable() {
        invalid = true;
        rand = new Random((uint)GetInstanceID());
    }
    private void Update() {
        if (invalid) {
            invalid = false;
            Reset();
        }

#if true
        particle3.Update(tuner);
#else
        particle3.CurrTuner = tuner;
        particle3.Next();
#endif
    }
    private void OnGUI() {
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, WindowFunc, name,
            GUILayout.MinWidth(200f));
    }
    private void OnRenderObject() {
        var mat = presets.mat;
        var n = presets.n;
        if (!enabled || particle3 == null || mat == null) return;

        mat.SetInt(P_ParticlesCount, particle3.particles.count);
        mat.SetBuffer(P_Particles, particle3.particles);

        mat.SetBuffer(P_Phases, particle3.phases);
        mat.SetBuffer(P_CohPhi, particle3.coherencePhases);

        mat.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, 1, n);
    }
    private void OnValidate() {
        invalid = true;
    }
    private void OnDisable() {
        Release();
    }
#endregion

    #region methods
    protected void WindowFunc(int id) {
        var rotator = presets.rotator;

        GUILayout.BeginVertical();

        if (rotator != null) {
            GUILayout.Label("Rotation");
            rotator.enabled = GUILayout.Toggle(rotator.enabled, "Rotator");
        }

        GUILayout.Label(string.Format("Coherence : {0}", tuner.coupling));
        tuner.coupling = GUILayout.HorizontalSlider(tuner.coupling, 0f, 1f);
        GUILayout.Label(string.Format("Range : {0}", tuner.couplingRange));
        tuner.couplingRange = GUILayout.HorizontalSlider(tuner.couplingRange, 0f, 10f);

        GUILayout.EndVertical();
        GUI.DragWindow();
    }
    protected void Release() {
        if (particle3 != null) {
            particle3.Dispose();
            particle3 = null;
        }
    }
    protected void Reset() {
        Release();

        var n = presets.n;
        particle3 = new((uint)n, settings);

        var field = presets.field;
        switch (presets.alignmentMode) {
            default: {
                for (var i = 0; i < n; i++) {
                    var pos = rand.NextFloat3(-0.5f, 0.5f);
                    pos = field.TransformPoint(pos);
                    particle3[i] = new() {
                        activity = 1,
                        pos = pos,
                    };
                }
                break;
            }
            case AlignmentMode.Grid2: {
                var m = (int)math.ceil(math.sqrt(n));
                var stride = 1f / m;
                for (var i = 0; i < n; i++) {
                    var x = ((i % m) + 0.5f) * stride;
                    var y = ((i / m) + 0.5f) * stride;
                    var pos = new float3(x - 0.5f, y - 0.5f, 0f);
                    pos = field.TransformPoint(pos);
                    particle3[i] = new() {
                        activity = 1,
                        pos = pos,
                    };
                }
                break;
            }
        }
    }
    #endregion

    #region declarations
    public enum AlignmentMode {
        RandomInField = 0,
        Grid2
    }
    [System.Serializable]
    public class Presets {
        public Material mat;
        public Transform field;
        public Rotator rotator;

        public AlignmentMode alignmentMode;

        [Range(1, 10000)]
        public int n = 100;
    }
    #endregion
}

