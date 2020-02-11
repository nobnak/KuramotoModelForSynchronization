using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteInEditMode]
public class KuramotoModel : MonoBehaviour {
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
    ComputeBuffer particleModelMatricesBuffer;

    float[] phases;
    ComputeBuffer phasesBuffer;
    
    float[] speeds;
    float[] coherencePhis;
    float[] coherenceRadiuses;
    ComputeBuffer coherencePhaseBuffer;

    #region Unity
    private void OnEnable() {
        invalid = true;
    }
    private void Update() {
        if (invalid) {
            invalid = false;
            Reset();
        }
        
        Coherence();

        var dt = Time.deltaTime;
        for (var i = 0; i < phases.Length; i++) {
            var p = phases[i];
            var cphi = coherencePhis[i];
            var crad = coherenceRadiuses[i];
            p += dt * (speeds[i]
                + noise * Noise()
                + coupling * crad * Mathf.Sin((cphi - p) * CIRCLE_IN_RADIAN));
            p -= (int)p;
            phases[i] = p;
        }
        phasesBuffer.SetData(phases);
    }
    private void OnGUI() {
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, WindowFunc, name,
            GUILayout.MinWidth(200f));
    }
    private void OnRenderObject() {
        var n = nOnLine * nOnLine;

        float h, s, v;
        Color.RGBToHSV(mat.GetColor(PROP_COLOR_HIGHLIGHT), out h, out s, out v);
        h += shadowColorHsvOffset.x;
        s += shadowColorHsvOffset.y;
        v += shadowColorHsvOffset.z;
        h = h - Mathf.Floor(h);
        s = Mathf.Clamp01(s);
        v = Mathf.Clamp01(v);
        mat.SetColor(PROP_COLOR_SHADOW, Color.HSVToRGB(h, s, v));

        mat.SetBuffer(PROP_PARTICLE_MODEL_MATRIX, particleModelMatricesBuffer);
        mat.SetBuffer(PROP_PHASES, phasesBuffer);
        mat.SetBuffer(PROP_COH_PHI, coherencePhaseBuffer);

        mat.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, 6, n);
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
    protected float Noise() {
        return 2f * Random.value - 1f;
    }
    protected void Release() {
        if (particleModelMatricesBuffer != null) {
            particleModelMatricesBuffer.Dispose();
            particleModelMatricesBuffer = null;
        }
        if (phasesBuffer != null) {
            phasesBuffer.Dispose();
            phasesBuffer = null;
        }
        if (coherencePhaseBuffer != null) {
            coherencePhaseBuffer.Dispose();
            coherencePhaseBuffer = null;
        }
    }
    protected void Reset() {
        Release();

        var n = nOnLine * nOnLine;
        var size = sizeGain / nOnLine;
        particleModelMatrices = new Matrix4x4[n];
        particleModelMatricesBuffer = new ComputeBuffer(n, Marshal.SizeOf(typeof(Matrix4x4)));

        phases = new float[n];
        phasesBuffer = new ComputeBuffer(n, Marshal.SizeOf(typeof(float)));

        speeds = new float[n];
        coherencePhis = new float[n];
        coherenceRadiuses = new float[n];
        coherencePhaseBuffer = new ComputeBuffer(n, Marshal.SizeOf(typeof(float)));

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

        for (var i = 0; i < phases.Length; i++) {
            phases[i] = Random.value;
            speeds[i] = speed * Random.Range(1f - speedVariation, 1f + speedVariation);
        }
        phasesBuffer.SetData(phases);
    }
    protected void Coherence() {
        for (var y = 0; y < nOnLine; y++) {
            for (var x = 0; x < nOnLine; x++) {
                var i = x + y * nOnLine;

                var sumx = 0f;
                var sumy = 0f;
                for (var h = -couplingRange; h <= couplingRange; h++) {
                    for (var w = -couplingRange; w <= couplingRange; w++) {
                        var y1 = y + h;
                        var x1 = x + w;
                        if ((x1 == x && y1 == y) 
                            || x1 < 0 || nOnLine <= x1 
                            || y1 < 0 || nOnLine <= y1)
                            continue;

                        var j = (x + w) + (y + h) * nOnLine;
                        var theta = phases[j] * CIRCLE_IN_RADIAN;
                        sumx += Mathf.Cos(theta);
                        sumy += Mathf.Sin(theta);
                    }
                }

                var bandwidth = 2 * couplingRange + 1;
                var counter = bandwidth * bandwidth - 1;
                sumx /= counter;
                sumy /= counter;
                coherencePhis[i] = Mathf.Atan2(sumy, sumx) * RADIAN_TO_NORMALIZED;
                coherenceRadiuses[i] = Mathf.Sqrt(sumx * sumx + sumy * sumy);
            }
        }
        coherencePhaseBuffer.SetData(coherencePhis);
    }

}

