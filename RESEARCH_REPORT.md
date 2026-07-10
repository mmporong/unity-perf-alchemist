# 🧪 Alchemist Research Report: 4대 병목 시나리오 카탈로그

본 리포트는 **Unity Performance Alchemist**의 실험 픽스처인 4개 병목 시나리오의 **메커니즘**, 각 시나리오에 적용 가능한 **최적화 기법의 이론적 사다리**, 그리고 **실측 산출 절차**를 기록합니다.

> **수치 규율**: 이 문서의 성능 수치는 두 종류뿐입니다.
> - **Measured** — 이 도구가 기록한 `Artifacts/research_run_*.json` 경로를 인용한 실측값 (editor-proxy 명시)
> - **Projected** — 문헌·엔진 문서 기반의 이론적 기대 (수치 없이 방향성만 서술)
>
> 이전 버전 문서에 있던 구체적 개선 수치 테이블은 해당 지표를 측정할 수 없는 하네스에서 작성된 것이었으므로 전면 삭제했습니다. 현 하네스가 실제로 수집하는 지표는 **프레임타임(ms)** 이며, 그 외 지표(GC Alloc, Draw Calls, GPU Time)는 수집 경로가 구현·검증되기 전까지 어떤 수치도 제시하지 않습니다.

---

## 🎮 Case 1: RTS Swarm Simulation (CPU Bound)

**픽스처**: `Samples/RtsSwarmSimulation.cs` — 1,500 유닛이 매 프레임 서로 거리를 계산하는 O(n²) 충돌 회피.

### 병목 메커니즘
- 1,500² = 2,250,000회/프레임의 브루트포스 이중 루프
- `Vector3.Distance`의 매 호출 제곱근 연산 (`sqrMagnitude` 비교로 대체 가능한 지점)
- 루프 내부의 반복적인 `transform.position` 프로퍼티 접근 (네이티브 경계 호출)
- 전부 메인 스레드 단일 코어에 집중

### 최적화 사다리 (Projected — 문헌 기반 이론)
1. **sqrMagnitude 치환**: 제곱근 제거. 반경 비교는 제곱 비교와 동치.
2. **Transform 접근 캐싱**: 위치 배열을 프레임당 1회 스냅샷.
3. **공간 분할(Spatial Hashing/Grid)**: O(n²) → O(n·k). 알고리즘 개선이 가장 큰 축.
4. **Job System + Burst**: 병렬화 + SIMD. 단, 현 패키지는 Burst를 의존성으로 선언하지 않으며, 적용 시 `Transform` 접근을 `TransformAccessArray`로 재설계해야 함 (메인 스레드 제약).

### 실측 절차
1. `Window > Alchemist > 1. Setup Swarm Test Scene`
2. Performance Researcher에서 티어 선택 후 SETTINGS RESEARCH 또는 DOE EXPERIMENT 실행
3. 결과는 `Artifacts/research_run_*.json`에 기록 — 이 시나리오는 CPU 바운드이므로 품질 세팅(해상도·그림자 등)의 효과가 제한적일 것으로 예상됨(projected). **이 대비가 곧 실험의 가치**: 게이트가 "효과 없음(Rollback/Inconclusive)"을 정직하게 판정하는지 확인하는 네거티브 컨트롤로 활용 가능.

---

## 🎵 Case 2: Rhythm Game GC Spike (Memory Bound)

**픽스처**: `Samples/OsuGCSimulator.cs` — 초당 120 노트 생성/파괴, 문자열 결합, 클로저 캡처.

### 병목 메커니즘
- `Instantiate`/`Destroy` 반복 → 힙 할당·해제 폭주 → GC 스파이크 (프레임타임 톱니)
- `"MISS: " + obj.name + ...` 문자열 연결 → 판정마다 새 string 할당
- 지역 변수를 캡처하는 람다 → 컴파일러가 클로저 클래스를 힙에 할당
- `List.RemoveAt` 중간 삭제 → 배열 시프트 비용

### 최적화 사다리 (Projected)
1. **Object Pooling** (`UnityEngine.Pool.ObjectPool<T>`): Instantiate/Destroy 제거 — 표준 라이브러리 해법.
2. **문자열 캐싱/StringBuilder**: 판정 문자열 사전 생성 또는 지연 포맷.
3. **클로저 제거**: 정적 메서드 + 상태 인자 전달.
4. **자료구조 교체**: 스왑-백 삭제 또는 링 버퍼.

### 실측 절차
Case 1과 동일한 실행 경로. **주의**: 현 하네스는 GC Alloc을 수집하지 않으므로 이 시나리오의 개선은 프레임타임 중앙값과 p95/p99 꼬리(GC 스파이크가 여기 나타남)로만 관찰됩니다. GC Alloc 카운터 수집은 `ProfilerSampling`의 가용성 검증 후 추가되는 업그레이드 경로입니다.

---

## 🌶️ Case 3: UGUI Canvas Rebuild (UI/Rendering Bound)

**픽스처**: `Samples/UiGridRebuildSimulator.cs` — 500셀 GridLayoutGroup + ContentSizeFitter, 매 프레임 20셀 강제 갱신.

### 병목 메커니즘
- 텍스트/크기 변경 → `SetLayoutDirty` → GridLayoutGroup+ContentSizeFitter 연쇄 → **캔버스 전체 지오메트리 재계산** (`Canvas.SendWillRenderCanvases`)
- 모든 요소의 `raycastTarget=true` → GraphicRaycaster 부하
- 단일 캔버스 → 부분 갱신 불가

### 최적화 사다리 (Projected)
1. **raycastTarget 해제**: 비인터랙티브 요소 이벤트 부하 제거 — 가장 싼 개선.
2. **캔버스 분리**: 정적/동적 UI를 서브 캔버스로 격리 → 리빌드 범위 축소.
3. **LayoutGroup 제거 + 수학적 배치**: 연쇄 리빌드의 근원 제거.
4. **가상화(Virtual Scroll)**: 보이는 N개만 풀링·재활용.

### 실측 절차
Case 1과 동일. UI 리빌드는 메인 스레드 프레임타임에 직접 반영되므로 현 하네스의 프레임타임 지표로 관찰 가능합니다.

---

## 🎆 Case 4: Particle GPU Overdraw (GPU Bound)

**픽스처**: `Samples/ParticleOverdrawSimulator.cs` — 50개 독립 ParticleSystem × 고유 Material × 알파블렌드.

### 병목 메커니즘
- 시스템마다 고유 Material 인스턴스 + 개별 renderQueue → 배칭 완전 차단
- 알파블렌드 반투명 파티클 중첩 → 픽셀당 다중 셰이딩 (fill rate 소모)
- GPU Instancing 비활성 → 드로우콜 분리

### 최적화 사다리 (Projected)
1. **머티리얼 공유**: 단일 shared material → 배칭 허용.
2. **GPU Instancing 활성화**.
3. **시스템 통합 + Custom Vertex Stream**: 색상 다양성을 유지하며 단일 시스템으로.
4. **거리 기반 LOD/사이즈 감쇠**: 원거리 오버드로 절감.

### 실측 절차
Case 1과 동일. **주의**: GPU 바운드 병목은 메인 스레드 프레임타임에 간접 반영됩니다(`Gfx.WaitForPresent` 대기). 현 하네스는 GPU Time·Draw Calls를 직접 수집하지 않으므로, 이 시나리오에서 도출된 세팅 효과는 프레임타임 관점으로만 해석해야 하며 그 한계를 리포트에 병기합니다.

---

## 📈 Measured Results

> 이 절은 실측 실행이 이루어지면 `Artifacts/research_run_*.json` 인용으로 채워집니다.
> **현재 상태: 미측정** — 커밋된 실측 아티팩트가 없으므로 어떤 개선 수치도 제시하지 않습니다.
> 실측 런 기록 절차는 [docs/RUN_LOG_TEMPLATE.md](./docs/RUN_LOG_TEMPLATE.md)를 따릅니다.

| 시나리오 | 티어 | 도출 config | 중앙값 개선 | 확인 런 | 아티팩트 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| (미측정) | — | — | — | — | — |

---

## 🏁 방법론 요약

측정당 N회 독립 런(런당 중앙값), Mann-Whitney U + Cliff's delta 이중 조건 채택 게이트, 티어 예산 출하 게이트, 종료 시 fresh ABAB 챔피언 확인 런 — 전체 정의는 [QA_METHODOLOGY.md](./QA_METHODOLOGY.md), 실험 설계(OFAT/Factorial/스크리닝)와 패치별 세팅 도출 파이프라인은 [DEVICE_TIER_OPTIMIZATION.md](./DEVICE_TIER_OPTIMIZATION.md) 참조.

---
**[mmporong](https://github.com/mmporong)** | 🌠
