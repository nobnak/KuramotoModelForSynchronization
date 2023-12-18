using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Xorwow;
using Xorwow.Extension;
using Random = Unity.Mathematics.Random;

namespace KuramotoModel {

    public class Kuramoto_Grid2 : System.IDisposable {

        ComputeShader cs;
        int k_next, k_coherence;
        uint2 next_gs, coherence_gs;

        Random rand;
        XorwowService xorwow;

        #region properties
        public Tuner CurrTuner { get; protected set; }

        public uint2 grid_size { get; protected set; }
        public Presets settings { get; protected set; }

        public GraphicsBuffer phases { get; protected set; }
        public GraphicsBuffer speeds { get; protected set; }
        public GraphicsBuffer coherencePhis { get; protected set; }
        public GraphicsBuffer coherenceRadiuses { get; protected set; }
        public GraphicsBuffer coherencePhases { get; protected set; }
        #endregion

        public Kuramoto_Grid2(uint2 grid_size, Presets settings) {
            var seed = GenerateSeed();
            var n = grid_size.x * grid_size.y;

            this.grid_size = grid_size;
            this.settings = settings;
            rand = new (seed);

            cs = Resources.Load<ComputeShader>(SHADER_NAME);
            k_next = cs.FindKernel("Next");
            k_coherence = cs.FindKernel("Coherence");
            cs.GetKernelThreadGroupSizes(k_next, out next_gs.x, out next_gs.y, out _);
            cs.GetKernelThreadGroupSizes(k_coherence, out coherence_gs.x, out coherence_gs.y, out _);

            Reset(grid_size);
        }

        #region interface
        public void Update(Tuner tuner) {
            this.CurrTuner = tuner;

            Coherence();
            Next();
        }
        public void Next() {
            var n = phases.count;

            cs.SetFloat(P_Time_Delta, Time.deltaTime);
            cs.SetVector(P_P0, P0);

            cs.SetInt(P_Phases_Length, n);
            cs.SetBuffer(k_next, P_Phases, phases);
            cs.SetBuffer(k_next, P_Coherence_Phis, coherencePhis);
            cs.SetBuffer(k_next, P_Coherence_Radiuses, coherenceRadiuses);
            cs.SetBuffer(k_next, P_Speeds, speeds);
            cs.SetXorwowStateBuf(k_next, xorwow);

            cs.Dispatch(k_next,
                (int)math.ceil((float)n / next_gs.x),
                1, 1);
        }
        public void Coherence() {
            cs.SetInts(P_Grid_Size, (int)grid_size.x, (int)grid_size.y);
            cs.SetVector(P_P0, P0);

            cs.SetBuffer(k_coherence, P_Phases, phases);
            cs.SetBuffer(k_coherence, P_Coherence_Phis, coherencePhis);
            cs.SetBuffer(k_coherence, P_Coherence_Radiuses, coherenceRadiuses);

            cs.Dispatch(k_coherence, 
                (int)math.ceil((float)grid_size.x / coherence_gs.x),
                (int)math.ceil((float)grid_size.y / coherence_gs.y), 
                1);
        }
        #endregion

        #region IDisposable
        public void Dispose() {
            Release();
        }
        #endregion

        #region methods
        protected uint GenerateSeed() => (uint)System.DateTime.Now.Ticks;
        protected float4 P0 => 
            new float4(CurrTuner.coupling, CurrTuner.couplingRange, CurrTuner.noise, 0f);
        protected void Reset(uint2 grid_size) {
            Release();

            var n = (int)(grid_size.x * grid_size.y);

            xorwow = new((int)n, GenerateSeed());

            phases = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            speeds = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            coherencePhis = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            coherenceRadiuses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            coherencePhases = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());

            var speed = settings.speed;
            var speedVariation = settings.speedVariation;
            phases.SetData(Enumerable.Range(0, n).Select(i => rand.NextFloat()).ToArray());
            speeds.SetData(Enumerable.Range(0, n).Select(i => speed * rand.NextFloat(1f - speedVariation, 1f + speedVariation)).ToArray());
        }
        protected void Release() {
            if (xorwow != null) {
                xorwow.Dispose();
                xorwow = null;
            }
            if (phases != null) {
                phases.Dispose();
                phases = null;
            }
            if (speeds != null) {
                speeds.Dispose();
                speeds = null;
            }
            if (coherencePhis != null) {
                coherencePhis.Dispose();
                coherencePhis = null;
            }
            if (coherenceRadiuses != null) {
                coherenceRadiuses.Dispose();
                coherenceRadiuses = null;
            }
            if (coherencePhases != null) {
                coherencePhases.Dispose();
                coherencePhases = null;
            }
        }
        #endregion

        #region declarations
        public const string SHADER_NAME = "Kuramoto_Grid2";

        public static readonly int P_Grid_Size = Shader.PropertyToID("_Grid_Size");
        public static readonly int P_P0 = Shader.PropertyToID("_P0");
        public static readonly int P_Time_Delta = Shader.PropertyToID("_Time_Delta");

        public static readonly int P_Phases_Length = Shader.PropertyToID("_Phases_Length");
        public static readonly int P_Phases = Shader.PropertyToID("_Phases");
        public static readonly int P_Speeds = Shader.PropertyToID("_Speeds");
        public static readonly int P_Coherence_Phis = Shader.PropertyToID("_Coherence_Phis");
        public static readonly int P_Coherence_Radiuses = Shader.PropertyToID("_Coherence_Radiuses");

        public struct Presets {
            public float speed;
            public float speedVariation;
        }
        public struct Tuner {
            public float coupling;
            public float couplingRange;
            public float noise;
        }
        #endregion
    }
}