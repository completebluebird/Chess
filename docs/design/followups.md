# 후속 작업 메모

리뷰에서 나왔지만 지금 고치지 않기로 한 항목을 모아 둡니다.

## 공격 후보가 한 종류만 남는 튜닝에서 보스가 멈춤

- 위치: `Assets/_Project/Scripts/Core/BossAttacks.cs`, `PickBasicAttack`
- 내용: 같은 공격을 세 번 연속 쓰지 않도록 두 번 연속 나온 종류를 후보에서 뺍니다. 튜닝으로 한 종류의 가중치만 남기면(예: 레이저만 0보다 큼) 세 번째 공격부터 후보가 비어 예외가 납니다.
- 현재 기본값(레이저 3, 운석 3, 느린 브레스 2, 물기 6)에서는 생기지 않습니다.
- 고치는 법: 다른 종류가 남아 있을 때만 뺍니다. `if (_sameBasicAttackInARow >= 2 && options.Any(kind => kind != _lastBasicAttack))`
- 고칠 때: 한 종류만 가중치를 준 설정으로 실패하는 Core 테스트를 먼저 씁니다.
