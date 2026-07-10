# Changelog

## [0.2.0] - 2026-07-10

### 구조 개편 (설정-레벨 autoresearch)

- 어셈블리 6분할: `UPA.Core`(순수 C#, 엔진 비의존) / `UPA.Runtime` / `UPA.Editor` / `UPA.Samples` / `UPA.Tests.Core` / `UPA.Tests.Editor`
- Core에 통계 게이트 도입: Mann-Whitney U + Cliff's delta 기반 AdoptionGate, ShippingGate, ChampionConfirmation(확인 런)
- 설정-레벨 연구 루프(SettingsResearchRunner)가 기본 경로 — 파일 재작성/도메인 리로드 루프는 opt-in으로 격리
- OFAT/Full factorial 실험 계획(ExperimentPlan)과 티어별 설정 도출(ConfigDerivation) 추가
- CI 도입: dotnet test(Core 테스트) + tools/validate.py 정적 검증(.meta, GUID, asmdef 그래프, Core 순수성, 문서 수치)
