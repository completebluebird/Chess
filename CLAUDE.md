# CLAUDE.md — 체스 보스전 (1페이즈)

폰 하나로 8각형 체스판 중앙의 드래곤을 쓰러뜨리는 3D 실시간 칸 이동 보스전.
기획서: `docs/design/phase1.md` (규칙의 원본. 코드와 기획서가 다르면 기획서를 따르고, 기획서가 모호하면 묻는다)
로드맵: `docs/roadmap.md` (현재 마일스톤과 완료 기준)

## 범위
- 1페이즈만 만든다. 2페이즈, 잡몹, 앙파상은 만들지 않는다.
- 미정 수치는 `docs/design/tuning.md`의 임시값을 쓰고, 코드에 숫자를 박지 않는다.

## 환경
- Unity 6000.6.0f1, URP 17.6, Input System, Test Framework, Cinemachine 6.6 (3.x의 후속, `Unity.Cinemachine` API 동일), uGUI
- 엔진 버전·렌더 파이프라인을 바꾸지 않는다. 새 패키지는 PR 설명에 이유를 적는다.
- 에셋스토어 패키지는 쓰지 않는다 (트윈은 `ChessBoss.Game/Tween`의 자체 구현).

## 코드 구조
- `Assets/_Project/Scripts/Core/` — `ChessBoss.Core` asmdef, `noEngineReferences: true`.
  게임 규칙 전부: 보드, 좌표(`Cell` 정수 구조체), 이동·공격 규칙, 각성, 착지 보정, 보스 패턴 타임라인,
  브레스 안전 경로, 체력·화상·무적·게이지, 보스 상태 머신. 시간은 `Tick(float dt)`로 주입받는다.
- `Assets/_Project/Scripts/Game/` — `ChessBoss.Game` asmdef. MonoBehaviour, ScriptableObject, 입력,
  카메라, 칸 색, 연출, 사운드. 규칙을 직접 계산하지 않고 Core를 호출해 결과를 그린다.
- `Assets/_Project/Tests/EditMode/` — Core 단위 테스트. `Assets/_Project/Tests/PlayMode/` — 씬 통합 테스트.
- `tools/CoreTests/` — Core 소스를 링크하는 .NET 테스트 프로젝트 (Unity 없이 실행).
- 데이터: `Assets/_Project/Data/` (`BossData`, `AttackPatternData` 등 ScriptableObject).

## 규칙
- Core에 `UnityEngine`을 import하지 않는다. 필요한 수학은 Core 안에 작게 만든다.
- 모든 오브젝트 위치는 `Cell` 좌표가 기준이고, 월드 위치는 `BoardView.CellToWorld`로만 계산한다.
- 공격 패턴은 "어느 칸을 언제 칠지" 데이터만 정의하고, 예고(빨강)→판정 흐름은 공통 시스템이 처리한다.
- 초록·빨강이 한 칸에 겹치면 빨강이 우선이다.
- 슬로모션은 `Time.timeScale`, 카메라·UI 연출은 unscaled time.
- 프로토타입 단계에서는 캡슐·큐브·칸 색만 쓴다. 아트는 M7 이후.

## 작업 방식
- 한 PR = 한 기능. 로드맵의 작업 번호(예: `M2-3`)를 브랜치명과 PR 제목에 넣는다.
- Core를 바꾸면 먼저 실패하는 테스트를 쓰고, 통과시킨 뒤 커밋한다.
- 기획서의 예시는 그대로 테스트 케이스로 만든다 (예: 비숍 보스(1,1)·내 위치(0,0) → 착지 (2,0)).
- 커밋 전 실행: `dotnet test tools/CoreTests` (클라우드), 가능하면 Unity EditMode 테스트.
- Unity 에디터가 필요한 작업(씬·프리팹 배치, 시각 확인)은 PR 설명의 "수동 확인" 항목에 단계로 적는다.
- `.meta` 파일을 새 파일과 함께 커밋한다. 씬·프리팹 YAML을 손으로 크게 고치지 말고 코드로 생성한다.
- 바이너리 에셋(fbx, png, wav)은 Git LFS로 올라간다.

## 자주 쓰는 명령
- .NET SDK 설치 (Unity 없는 환경): `bash tools/setup-dotnet.sh`
- Core 테스트: `dotnet test tools/CoreTests`
- Unity 테스트 (로컬): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
