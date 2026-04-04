using UnityEngine;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Samples
{
    /// <summary>
    /// 포트폴리오 Case 4: GPU 파티클 오버드로 병목 시뮬레이션
    /// 50개의 독립 ParticleSystem이 각각 고유 Material로 100개씩 알파블렌드 파티클을 방출합니다.
    /// 개별 머티리얼 → 배칭 불가, 알파블렌드 → 과도한 오버드로, GPU Instancing 미사용으로
    /// Fill Rate 병목과 Draw Call 폭증을 의도적으로 유발합니다.
    ///
    /// AI의 목표:
    /// 1. 머티리얼 공유 (Draw Call 감소)
    /// 2. GPU Instancing 활성화
    /// 3. ParticleSystem 통합 + Custom Vertex Stream + 거리 기반 LOD
    /// </summary>
    public class ParticleOverdrawSimulator : MonoBehaviour
    {
        [Header("Particle Overdraw Settings")]
        [Tooltip("독립 ParticleSystem 개수 (각각 고유 머티리얼 사용)")]
        public int systemCount = 50;

        [Tooltip("각 시스템당 최대 파티클 수")]
        public int particlesPerSystem = 100;

        [Tooltip("파티클 스폰 영역 반경")]
        public float spawnRadius = 15f;

        // [Bottleneck 1] 각 시스템이 개별 Material을 가지므로 Dynamic Batching 불가
        private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
        private List<Material> uniqueMaterials = new List<Material>();

        void Start()
        {
            for (int i = 0; i < systemCount; i++)
            {
                CreateParticleSystem(i);
            }
        }

        private void CreateParticleSystem(int index)
        {
            // [Bottleneck 2] 각 시스템마다 별도의 GameObject 생성
            GameObject psObj = new GameObject("ParticleEmitter_" + index);
            psObj.transform.SetParent(this.transform);
            psObj.transform.position = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                Random.Range(0f, 5f),
                Random.Range(-spawnRadius, spawnRadius)
            );

            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();

            // 메인 모듈 설정
            var main = ps.main;
            main.maxParticles = particlesPerSystem;
            main.startLifetime = 3f;
            main.startSpeed = 2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(
                Random.Range(0.2f, 1f),
                Random.Range(0.2f, 1f),
                Random.Range(0.2f, 1f),
                0.4f // [Bottleneck 3] 반투명 알파 → 모든 파티클이 오버드로 유발
            );

            // 방출 설정 (지속 방출)
            var emission = ps.emission;
            emission.rateOverTime = particlesPerSystem / main.startLifetime.constant;

            // 형상: 구체 방출
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.5f;

            // 크기 애니메이션 (GPU 부하 추가)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.5f, 1.5f),  // 중간에 크기 최대 → 오버드로 극대화
                    new Keyframe(1f, 0f)
                ));

            // [Bottleneck 4] 각 시스템마다 고유 Material 인스턴스 생성
            // → 동일 쉐이더여도 Material이 다르면 배칭 불가, Draw Call 분리
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetFloat("_Mode", 3); // Transparent (Fade)
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000 + index; // [Bottleneck 5] 각기 다른 renderQueue → 정렬 분리
            mat.enableInstancing = false;   // [Bottleneck 6] GPU Instancing 비활성화

            // 고유 색상으로 머티리얼을 차별화 (배칭 완전 방지)
            mat.SetColor("_Color", new Color(
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                0.5f
            ));

            uniqueMaterials.Add(mat);

            // 렌더러 설정
            var renderer = psObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = index; // [Bottleneck 7] 개별 정렬 순서
            // GPU Instancing 미사용 (의도적 비활성화)
            renderer.enableGPUInstancing = false;

            particleSystems.Add(ps);
        }

        void Update()
        {
            // [Bottleneck 8] 매 프레임 모든 시스템의 Transform을 동적으로 이동
            // → GPU가 새 위치에서 파티클을 다시 렌더링해야 함
            for (int i = 0; i < particleSystems.Count; i++)
            {
                Transform t = particleSystems[i].transform;
                float time = Time.time + i * 0.5f;
                t.position = new Vector3(
                    Mathf.Sin(time * 0.3f) * spawnRadius,
                    Mathf.Abs(Mathf.Sin(time * 0.7f)) * 5f,
                    Mathf.Cos(time * 0.3f) * spawnRadius
                );
            }
        }
    }
}
