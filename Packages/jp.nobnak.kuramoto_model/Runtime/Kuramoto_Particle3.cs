using Codice.CM.Common;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Xorwow;
using Xorwow.Extension;
using static UnityEngine.ParticleSystem;
using Random = Unity.Mathematics.Random;

namespace KuramotoModel {

    public class Kuramoto_Particle3 : System.IDisposable {

        ComputeShader cs;
        int k_next, k_coherence;
        uint3 next_gs, coherence_gs;

        Random rand;
        XorwowService xorwow;

        bool invalid_particles;
        Particle3[] particles_src;

        #region properties
        public Tuner CurrTuner { get; set; }

        public uint capacity { get; protected set; }
        public Presets settings { get; protected set; }

        public GraphicsBuffer particles { get; protected set; }
        public GraphicsBuffer phases { get; protected set; }
        public GraphicsBuffer speeds { get; protected set; }
        public GraphicsBuffer coherencePhis { get; protected set; }
        public GraphicsBuffer coherenceRadiuses { get; protected set; }
        public GraphicsBuffer coherencePhases { get; protected set; }
        #endregion

        public Kuramoto_Particle3(uint capacity, Presets settings) {
            var seed = GenerateSeed();

            this.capacity = capacity;
            this.settings = settings;
            rand = new (seed);

            cs = Resources.Load<ComputeShader>(SHADER_NAME);
            k_next = cs.FindKernel("Next");
            k_coherence = cs.FindKernel("Coherence");
            cs.GetKernelThreadGroupSizes(k_next, out next_gs.x, out next_gs.y, out next_gs.z);
            cs.GetKernelThreadGroupSizes(k_coherence, out coherence_gs.x, out coherence_gs.y, out coherence_gs.z);

            Reset(capacity);
        }

        #region interface
        public Particle3 this[int index] {
            get => particles_src[index];
            set {
                particles_src[index] = value;
                invalid_particles = true;
            }
        }
        public void Update(Tuner tuner) {
            this.CurrTuner = tuner;
            Coherence();
            Next();
        }
        public void Next() {
            Validate();

            var n = (int)capacity;

            cs.SetFloat(P_Time_Delta, Time.deltaTime);
            cs.SetVector(P_P0, P0);

            cs.SetInt(P_Particles_Length, particles.count);
            cs.SetBuffer(k_next, P_Particles, particles);

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
            Validate();

            var n = (int)capacity;

            cs.SetVector(P_P0, P0);

            cs.SetInt(P_Particles_Length, particles.count);
            cs.SetBuffer(k_coherence, P_Particles, particles);

            cs.SetBuffer(k_coherence, P_Phases, phases);
            cs.SetBuffer(k_coherence, P_Coherence_Phis, coherencePhis);
            cs.SetBuffer(k_coherence, P_Coherence_Radiuses, coherenceRadiuses);

            cs.Dispatch(k_coherence, 
                (int)math.ceil((float)n / coherence_gs.x),
                1, 1);
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

        protected void Validate() {
            if (invalid_particles) {
                invalid_particles = false;
                particles.SetData(particles_src);
            }
        }
        protected void Reset(uint capacity) {
            Release();

            var n = (int)capacity;
            xorwow = new(n, GenerateSeed());

            invalid_particles = true;
            particles_src = new Particle3[n];
            particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<Particle3>());

            phases = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            speeds = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            coherencePhis = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            coherenceRadiuses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());
            coherencePhases = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf<float>());

            var speed = settings.speed;
            var speedVariation = math.abs(settings.speedVariation);
            phases.SetData(Enumerable.Range(0, n).Select(i => rand.NextFloat()).ToArray());
            speeds.SetData(Enumerable.Range(0, n).Select(i => speed * rand.NextFloat(1f, 1f + speedVariation)).ToArray());
        }
        protected void Release() {
            if (xorwow != null) {
                xorwow.Dispose();
                xorwow = null;
            }
            if (particles != null) {
                particles.Dispose();
                particles = null;
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
        public const string SHADER_NAME = "Kuramoto_Particle3";

        public static readonly int P_P0 = Shader.PropertyToID("_P0");
        public static readonly int P_Time_Delta = Shader.PropertyToID("_Time_Delta");

        public static readonly int P_Particles_Length = Shader.PropertyToID("_Particles_Length");
        public static readonly int P_Particles = Shader.PropertyToID("_Particles");

        public static readonly int P_Phases = Shader.PropertyToID("_Phases");
        public static readonly int P_Speeds = Shader.PropertyToID("_Speeds");
        public static readonly int P_Coherence_Phis = Shader.PropertyToID("_Coherence_Phis");
        public static readonly int P_Coherence_Radiuses = Shader.PropertyToID("_Coherence_Radiuses");

        [System.Serializable]
        public struct Presets {
            public float speed;
            public float speedVariation;
        }
        [System.Serializable]
        public struct Tuner {
            public float coupling;
            public float couplingRange;
            public float noise;
        }
        #endregion
    }
}