# 실측 런 로그 템플릿

> 외부 프로젝트(또는 이 리포의 샘플 씬)에 Alchemist를 적용한 실측 기록을 남길 때 이 템플릿을 사용합니다.
> **원칙**: 모든 수치는 `Artifacts/research_run_*.json`에서 복사하고 해당 파일을 리포에 커밋합니다. 아티팩트 없는 수치는 기록하지 않습니다.

---

## 런 메타데이터

| 항목 | 값 |
| :--- | :--- |
| 대상 프로젝트 | (이름 + 커밋/버전) |
| 날짜 | (Artifacts JSON의 derivedAtUtc 복사) |
| 티어 | low / mid / high |
| 측정 소스 | editor-proxy / on-device (기기 모델 명기) |
| 모드 | SETTINGS RESEARCH (LLM: 모델명) / DOE (OFAT·Factorial, 시드) |
| N (런/조건) | (기본 10) |
| 아티팩트 | `Artifacts/research_run_<타임스탬프>.json` (커밋 필수) |

## 베이스라인

- 런 중앙값들(ms): (JSON `baseline.runMediansMs` 복사)
- 중앙값 / p95 / p99: (JSON `baseline.stats` 복사)

## 세대별 기록

| Gen | 가설(strategy) | 변경 세팅 | 중앙값(ms) | p값 | Cliff's d | 판정 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | (JSON steps[0] 복사) | | | | | Adopt / Rollback / Inconclusive |

## 챔피언 최종 확인 런 (fresh ABAB)

| 항목 | 값 |
| :--- | :--- |
| fresh 베이스라인 중앙값 | (JSON confirmation 복사) |
| fresh 챔피언 중앙값 | |
| 재검정 p / d | |
| p95 델타 / p99 델타 | |
| confirmed | true / false |
| budgetSatisfied | true / false |

> 확인 런이 실패(confirmationFailed)한 경우 이 런의 도출 config는 사용하지 않습니다. 실패 자체를 기록으로 남기세요 — 그것이 시스템이 작동한다는 증거입니다.

## 도출 config

```json
(JSON derivedConfig 블록 복사 — derivedAgainst 포함)
```

## 직전 패치 대비 diff (해당 시)

(ReportExporter의 패치 간 diff 섹션 또는 ConfigDerivation.Diff 결과)

## 관찰·특이사항

- (열 상태, 워치독 발동, 파서 실패 세대, Inconclusive 사유 등 정직하게)
