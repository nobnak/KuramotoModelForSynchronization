using KuramotoModel;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class Grid2Simulator : MonoBehaviour {
    public const string PROP_PARTICLE_MODEL_MATRIX = "_ParticleModelMatrices";
    public const string PROP_PHASES = "_Phases";
    public const string PROP_COLOR_HIGHLIGHT = "_Highlight";
    public const string PROP_COLOR_SHADOW = "_Shadow";
    public const string PROP_COH_PHI = "_CohPhi";

    public const float CIRCLE_IN_RADIAN = 2f * Mathf.PI;
    public const float RADIAN_TO_NORMALIZED = 1f / CIRCLE_IN_RADIAN;

    [SerializeField] protected Material mat;
    [SerializeField] protected int nOnLine = 100;
    [Range(0.01f, 10f)]
    [SerializeField] protected float sizeGain = 10f;

    [Range(0f, 5f)]
    [SerializeField] protected float coupling = 0f;
    [Range(1, 10)]
    [SerializeField] protected int couplingRange = 1;
    [Range(0f, 1f)]
    [SerializeField] protected float speed = 1f;
    [Range(0f, 1f)]
    [SerializeField]
    protected float speedVariation = 0.1f;
    [Range(0f, 10f)]
    [SerializeField] protected float noise = 0.1f;

    [SerializeField] protected Vector3 shadowColorHsvOffset;

    bool invalid;
    Rect windowRect;

    Matrix4x4[] particleModelMatrices;
    GraphicsBuffer particleModelMatricesBuffer;

    Kuramoto_Grid2 grid2;

    #region Unity
    private void OnEnable() {
        invalid = true;
    }
    private void Update() {
        if (invalid) {
            invalid = false;
            Reset();
        }

        var tuner = new Kuramoto_Grid2.Tuner() {
            coupling = coupling,
            couplingRange = couplingRange,
        };
        grid2.Update(tuner);
    }
    private void OnGUI() {
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, WindowFunc, name,
            GUILayout.MinWidth(200f));
    }
    private void OnRenderObject() {
        if (!enabled || grid2 == null) return;

        var n = nOnLine * nOnLine;

        mat.SetBuffer(PROP_PARTICLE_MODEL_MATRIX, particleModelMatricesBuffer);
        mat.SetBuffer(PROP_PHASES, grid2.phases);
        mat.SetBuffer(PROP_COH_PHI, grid2.coherencePhases);

        mat.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, n);
    }
    private void OnValidate() {
        invalid = true;
    }
    private void OnDisable() {
        Release();
    }
#endregion

    protected void WindowFunc(int id) {
        GUILayout.BeginVertical();

        GUILayout.Label(string.Format("Coherence : {0}", coupling));
        GUILayout.BeginHorizontal();
        coupling = GUILayout.HorizontalSlider(coupling, 0f, 10f);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUI.DragWindow();
    }
    protected void Release() {
        if (grid2 != null) {
            grid2.Dispose();
            grid2 = null;
        }
        if (particleModelMatricesBuffer != null) {
            particleModelMatricesBuffer.Dispose();
            particleModelMatricesBuffer = null;
        }
    }
    protected void Reset() {
        Release();

        var grid_size = new uint2((uint)nOnLine, (uint)nOnLine);
        var n = grid_size.x * grid_size.y;
        var size = sizeGain / nOnLine;
        var settings = new Kuramoto_Grid2.Presets {
            speed = speed,
            speedVariation = speedVariation,
        };

        grid2 = new(grid_size, settings);

        particleModelMatrices = new Matrix4x4[n];
        particleModelMatricesBuffer = new(GraphicsBuffer.Target.Structured,
            (int)n, Marshal.SizeOf<Matrix4x4>());

        var offset = new Vector3(-0.5f * nOnLine * size, -0.5f * nOnLine * size, 0f);
        for (var y = 0; y < nOnLine; y++) {
            for (var x = 0; x < nOnLine; x++) {
                var i = x + y * nOnLine;
                var pos = new Vector3(x * size, y * size, 0f) + offset;
                var m = Matrix4x4.TRS(pos, Quaternion.identity, size * Vector3.one);
                particleModelMatrices[i] = m;
            }
        }
        particleModelMatricesBuffer.SetData(particleModelMatrices);
    }
}

