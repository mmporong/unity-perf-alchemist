using UnityEngine;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Samples
{
    /// <summary>
    /// 포트폴리오용 병목 시뮬레이션: RTS 게임의 대규모 유닛 충돌 회피 알고리즘
    /// 이 코드는 의도적으로 O(n^2) 연산과 캐싱 부족을 유발하여 프레임 드랍을 일으킵니다.
    /// 목표: AI가 이 스크립트를 분석하고 Job System + Burst Compiler 구조로 최적화해야 합니다.
    /// </summary>
    public class RtsSwarmSimulation : MonoBehaviour
    {
        [Header("Swarm Settings")]
        [Tooltip("1000개가 넘어가면 싱글스레드에서 극심한 프레임 드랍이 발생합니다.")]
        public int unitCount = 1500; 
        public float moveSpeed = 3f;
        public float avoidanceRadius = 2.0f;
        public float mapBounds = 25f;

        // [Bottleneck 1] 구시대적 자료구조 (배열과 List 혼용)
        private GameObject[] units;
        private Vector3[] targetPositions;

        void Start()
        {
            units = new GameObject[unitCount];
            targetPositions = new Vector3[unitCount];

            for (int i = 0; i < unitCount; i++)
            {
                // [Bottleneck 2] 매번 CreatePrimitive 호출 (원래는 프리팹 풀링을 해야 함)
                GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                unit.name = "Unit_" + i;
                
                // 그림자 비활성화 (GPU 병목 방지, 순수 CPU 병목 유도)
                var renderer = unit.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                unit.GetComponent<Collider>().enabled = false; // 물리 엔진 연산 제외

                Vector3 startPos = GetRandomPosition();
                unit.transform.position = startPos;
                unit.transform.SetParent(this.transform);

                units[i] = unit;
                targetPositions[i] = GetRandomPosition();
            }
        }

        void Update()
        {
            // [Bottleneck 3] O(n^2) 브루트포스 거리 연산
            // 1500개일 경우 매 프레임 2,250,000 번의 반복문 수행
            for (int i = 0; i < unitCount; i++)
            {
                // [Bottleneck 4] 매 프레임 GetComponent에 준하는 transform 프로퍼티 연속 호출
                Transform currentTransform = units[i].transform;
                Vector3 currentPos = currentTransform.position;
                Vector3 avoidVector = Vector3.zero;

                for (int j = 0; j < unitCount; j++)
                {
                    if (i == j) continue;

                    // [Bottleneck 5] 매우 무거운 Vector3.Distance 연산 (내부적으로 제곱근 Mathf.Sqrt 사용)
                    float dist = Vector3.Distance(currentPos, units[j].transform.position);

                    if (dist < avoidanceRadius)
                    {
                        avoidVector += (currentPos - units[j].transform.position).normalized;
                    }
                }

                // 목표 지점으로의 방향 벡터 계산
                Vector3 dirToTarget = (targetPositions[i] - currentPos).normalized;
                
                // 최종 이동 벡터 산출 및 적용
                Vector3 finalMove = (dirToTarget + avoidVector).normalized * moveSpeed * Time.deltaTime;
                currentTransform.position += finalMove;

                // 목표 도착 시 새로운 목표 지점 설정
                if (Vector3.Distance(currentPos, targetPositions[i]) < 1.0f)
                {
                    targetPositions[i] = GetRandomPosition();
                }
            }
        }

        private Vector3 GetRandomPosition()
        {
            return new Vector3(
                Random.Range(-mapBounds, mapBounds),
                0,
                Random.Range(-mapBounds, mapBounds)
            );
        }
    }
}
