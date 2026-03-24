using UnityEngine;
using System.Collections.Generic;
using System;

namespace UnityPerformanceAlchemist.Samples
{
    /// <summary>
    /// 포트폴리오 Case 2: 리듬게임(osu!lazer 스타일) GC 스파이크 시뮬레이션
    /// 의도적으로 매 프레임 다량의 객체 생성(Instantiate), 문자열 결합, 익명 델리게이트 캡처를 수행하여
    /// 프로파일러에서 '톱니바퀴' 형태의 치명적인 GC Alloc을 유발합니다.
    /// 목표: AI가 이를 인지하고 Object Pooling 및 Zero-Allocation 아키텍처로 리팩토링해야 합니다.
    /// </summary>
    public class OsuGCSimulator : MonoBehaviour
    {
        [Header("Deathstream Settings")]
        public float spawnRatePerSecond = 120f; // 초당 120개의 노트 생성 (고부하)
        public float noteFallSpeed = 10f;
        public float hitLineY = -4f;

        // 구식 리스트 방식 (매번 Add/Remove 발생)
        private List<GameObject> activeHitObjects = new List<GameObject>();
        private float spawnTimer = 0f;

        void Update()
        {
            // 1. [Bottleneck] 매 프레임 객체 생성 검사 및 다량의 메모리 할당
            spawnTimer += Time.deltaTime;
            float spawnInterval = 1f / spawnRatePerSecond;

            while (spawnTimer >= spawnInterval)
            {
                SpawnHitObject();
                spawnTimer -= spawnInterval;
            }

            // 2. [Bottleneck] for문 내에서 배열 수정 및 소멸
            for (int i = activeHitObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = activeHitObjects[i];
                obj.transform.position += Vector3.down * noteFallSpeed * Time.deltaTime;

                // 타격 판정선 통과 시 (Miss 또는 Hit)
                if (obj.transform.position.y < hitLineY)
                {
                    // 3. [Bottleneck] 문자열 가비지 (String Allocation)
                    // 매 판정마다 새로운 string 객체가 힙에 할당됨
                    ShowJudgement("MISS: " + obj.name + " at " + Time.time.ToString("F2"));

                    // 4. [Bottleneck] 가장 치명적인 문제: Instantiate / Destroy 반복
                    Destroy(obj);
                    activeHitObjects.RemoveAt(i);
                }
            }
        }

        private void SpawnHitObject()
        {
            // [Bottleneck] 매 생성마다 무거운 프리미티브 2개 생성 (HitCircle, ApproachCircle 묘사)
            GameObject hitCircle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject approachCircle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            
            // 계층 구조 할당 (추가적인 GC 발생)
            approachCircle.transform.SetParent(hitCircle.transform);
            
            // 랜더러 그림자 끄기
            hitCircle.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            approachCircle.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 랜덤 x 좌표에서 스폰
            float randomX = UnityEngine.Random.Range(-5f, 5f);
            hitCircle.transform.position = new Vector3(randomX, 6f, 0f);
            
            // [Bottleneck] Closure Allocation (숨겨진 메모리 누수)
            // 람다식 내에서 지역 변수(randomX)를 캡처하면 내부적으로 컴파일러가 클래스를 생성하여 힙에 할당함
            Action onHit = () => {
                Debug.Log("Hit on lane: " + randomX); 
            };

            // 편의상 이름에 델리게이트 해시를 넣어 가비지 유발
            hitCircle.name = "Note_" + onHit.GetHashCode();

            activeHitObjects.Add(hitCircle);
        }

        private void ShowJudgement(string text)
        {
            // 이 시뮬레이션에서는 UI 텍스트 업데이트 대신 콘솔로 대체하지만, 
            // string 매개변수 자체가 힙에 쌓여 GC 스파이크를 발생시키는 원인으로 작용합니다.
        }
    }
}
