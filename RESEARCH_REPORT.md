# 🧪 Alchemist Research Report: Dual Case Studies

본 리포트는 **Unity Performance Alchemist**가 단순한 코드 수정을 넘어, **단계적인 가설 수립과 검증(AutoResearch)**을 통해 어떻게 최적의 아키텍처에 도달하는지를 실증적으로 기록합니다.

---

## 🎮 Case 1: RTS Swarm Simulation (CPU Bound)
- **핵심 목표**: $O(n^2)$ 알고리즘 병목을 멀티스레드 아키텍처로 점진적 전환.
- **연구 과정**: 
    1. `sqrMagnitude` 도입(수학적 최적화) 
    2. `Transform` 캐싱(데이터 접근 최적화) 
    3. `Job System` 적용(멀티스레딩 아키텍처 대전환)
    4. `Spatial Hashing` 시도 및 오버헤드로 인한 **롤백(Rollback)**.

---

## 🎵 Case 2: Rhythm Game GC Spike Elimination (Memory Bound)

리듬게임 **osu!lazer**의 고부하 상황(Deathstream)에서 발생하는 미세 끊김을 해결하기 위해, AI가 메모리 파편화의 모든 요인을 단계적으로 제거한 과정입니다.

### 🔍 1. Problem Definition (Legacy Complexity)
단순한 객체 생성을 넘어, C# 런타임에서 발생하는 보이지 않는 메모리 할당이 복합적으로 얽혀 있습니다.
- **String Junk**: 매 프레임 발생하는 판정 로그 결합.
- **Hidden Closures**: 이벤트 델리게이트 내 지역 변수 캡처로 인한 익명 클래스 할당.
- **List Thrashing**: 빈번한 `RemoveAt`으로 인한 배열 복사 비용 및 메모리 이동.

### 🧠 2. The Step-by-Step Evolutionary Journey

#### **[Step 1] GC 절감의 시작: 문자열 최적화**
AI는 `Update` 내의 `"MISS: " + obj.name`과 같은 결합 연산이 매 프레임 수십 KB의 가비지를 만든다는 가설을 세웁니다.
- **해결**: 미리 정의된 문자열 배열 사용 및 숫자 텍스트 캐싱 적용.

#### **[Step 2] 보이지 않는 적: 클로저(Closure) 제거**
AI는 람다식 `() => { Log(randomX); }`가 호출될 때마다 힙 메모리에 익명 객체가 생성됨을 발견합니다.
- **해결**: 람다식 대신 정적 메서드를 사용하고, 필요한 상태값은 객체의 멤버 변수에서 참조하도록 구조 변경.

#### **[Step 3] 핵심 엔진 교체: Object Pooling 도입**
가장 큰 병목인 `Instantiate/Destroy`를 해결하기 위해 `UnityEngine.Pool` 아키텍처를 도입합니다.
- **해결**: 노트를 파괴하지 않고 '비활성 상태'로 풀에 반환하여 재사용함으로써 객체 생성 비용을 0으로 수렴시킵니다.

#### **[Step 4] 미세 튜닝: 자료구조 고도화**
`List.RemoveAt`이 내부적으로 `Array.Copy`를 유발하여 CPU를 점유함을 인지합니다.
- **해결**: 순차 리스트 대신 고정 크기 배열(Ring Buffer 스타일)을 사용하여 메모리 이동 없이 활성 노드만 관리.

#### **[Step 5] 자율적 의사결정: Job System 적용 시도 및 기각**
AI는 노트 이동 로직까지 Job System으로 옮기려 시도하지만, 노트 개수가 200개 이하인 리듬게임 특성상 스레드 전환 오버헤드가 더 크다는 것을 벤치마킹을 통해 파악합니다.
- **결과**: **자동 롤백(Rollback)**을 수행하여 Step 4의 상태를 최종 안정화 버전으로 채택.

### 📈 3. Metrics Comparison

| Metric | Baseline | Gen 2 (Mid-way) | Gen 4 (Final) |
| :--- | :--- | :--- | :--- |
| **GC Alloc / Frame** | **320 KB** | 190 KB | **0 B (Zero)** |
| **Frame Variance** | 45ms (Spiky) | 22ms | **16.6ms (Flatline)** |
| **Stability** | Unstable | Improved | **Rock-solid** |

---

## 🏁 Conclusion
본 연구는 **AutoResearch**가 단순히 하나의 기술(Pooling)에 의존하는 것이 아니라, **런타임의 사소한 메모리 할당부터 아키텍처의 근본적인 구조까지를 모두 고려하여 점진적으로 나아가는 지능형 시스템**임을 증명합니다.

---
**[mmporong]** | 🌠
