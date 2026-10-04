# 0005. 운전 상태 모델은 TcHit FB_StateMachine(PackML 축약형)을 따르고, C#에서는 순수 상태 모델과 모듈 베이스 두 층으로 나눈다

상태: 승인됨
날짜: 2026-10-03


## 배경

장비를 돌리려면 Abort/Home/Run/Pause/Stop 계열의 운전 상태가 필요하다. 거의 모든 장비에 공통이므로 Core에 둔다.
사양서 5.4는 Load Lock의 진공 여부와 펌핑/벤팅 동작(Unknown, AtAtmosphere, Pumping, AtVacuum, Venting, Error)을 상태머신 상태로 정의했다.
그러나 그 상태들만으로는 자동 운전을 정의할 수 없고, 진공 여부는 LL·TM·PM이 공통으로 갖는 챔버의 속성이다.
따라서 상태머신은 운전 상태 하나로 두고, 진공 상태는 챔버 속성으로 Cluster 쪽에 둔다. Run 시퀀스는 진공 상태를 조건으로 확인만 한다.
각 챔버는 단독으로 운전될 수 있어야 한다(LL 에러로 정상 PM이 멈추면 안 된다).
개발자가 만든 TwinCAT 표준 HitTcLibStateMachine 2.0이 참고 대상으로 있다.


## 선택지

### 안 1: PackML 상태 모델을 그대로 채택

장점: 국제 표준(ISA-TR88.00.02) 그대로라 설명이 쉽다.
단점: 17개 상태(Starting, Completing, Suspending, Unsuspending, Clearing 등)의 대부분이 클러스터 툴에 필요 없다. 구현과 테스트 부담만 커진다.

### 안 2: TcHit FB_StateMachine 모델을 채택

PackML을 축약한 모델이다. HOMING≈Resetting, HOMED≈Idle, RUNNING≈Execute, PAUSE≈Hold, Stop/Abort 동일. 모드 MANUAL/MAINT/AUTO는 PackML Manual/Maintenance/Production에 대응한다.
장점: 실장비에서 검증되었다. 명령 수락 즉시 하위 전파, 하위를 보고 완료 판정, 에러 상위 합산이라는 계층 규칙이 명확하다. Machine과 Module이 같은 부모를 가지므로 모듈 단독 운전이 구조적으로 가능하다.
단점: PLC 제약에서 나온 관습(매 스캔 등록, 고정 크기 조건 배열, 매 스캔 모드 복사, 상속 한 덩어리)이 섞여 있다.


## 결정

안 2를 택한다. 상태 집합과 명령 수락 규칙표는 TcHit 2.0 README를 따른다. README에 PackML 대응표를 남긴다.
C#에서는 다음을 바꾼다.

1. 두 층으로 나눈다. 층 1 순수 상태 모델은 명령 수락, 전이, 이력, 타임아웃을 맡고 완료 조건(Permissive)을 매 스캔 보고 스스로 전이한다. 외부는 명령만 보낸다. 층 2 모듈 베이스는 시퀀스를 구동하고 완료 조건의 내용(시퀀스 완료, 하위 상태)을 층 1에 등록한다(2026-10-04 개정: 외부 Complete() 통보 방식 폐기).
2. 하위 등록은 생성 시 한 번. 모드는 ModeSelector 객체를 참조로 공유한다(FollowMode 복사 없음). 챔버별/공통 모드는 조립 시 결정한다.
3. 조건 배열은 Permissive로 표현한다.
4. 명령 스레드와 Scan 스레드의 동시 접근은 스캔 사이클이 가진 lock 하나로 직렬화한다. 명령은 스캔과 스캔 사이에만 반영된다(PLC와 동일한 의미). 실행용 스캔 사이클 작성 시 구현한다.
5. 사양서 5.4 요구를 층 1에 적용한다. 단 전이 타임아웃은 전이마다 주인이 다르다: Aborting은 층 1 필수, Homing/Pausing은 하위 장치 타임아웃이 주인이고 층 1은 선택(백업 상한), Stopping은 공정/레시피 타임아웃이 주인이라 층 1은 보통 감시하지 않는다(TimeSpan? null). 불허 전이는 false로 거부, 전이 상태(Aborting/Homing/Pausing/Stopping)별 타임아웃 시 에러 래칭, 전이 이력을 record로 기록, 장치 없이 테스트.
모드 변경 규칙은 ModeSelector 안에 고정한다(Manual: 전원 Aborted/Aborting, Maint/Auto: 전원 Aborted/Homed). 안전 관련이라 장비별로 바꿀 수 없고, 장비는 모드별 Permissive(Manual/Maint/AutoPermissive)로 더 엄격하게만 할 수 있다. 상태머신은 생성 시 ModeSelector에 자신을 등록한다.
하위 장치와의 계약은 TcHit의 기능별 인터페이스(ErrorReset → StandStill → Abortable)를 C# 인터페이스로 옮긴다(층 2 작업 시).


## 결과

수락 규칙표 전체를 모듈 없이 단위 테스트할 수 있다.
타임아웃 시 상태는 바꾸지 않고 에러만 세운다. 완료 조건에 의한 자동 전이는 에러와 무관하게 진행한다. 에러는 완료 여부가 아니라 새 명령(Home/Run/SemiAuto) 거부로만 작동한다. 에러 대응(Abort)은 층 2 시퀀스가 한다. 사양서 5.4의 "초과 시 Error로 간다"는 Error 플래그로 충족한다.
층 1과 층 2 사이 완료 통보가 한 스캔 늦을 수 있다(등록 순서). 실사용에서 문제되면 다시 본다.
재검토 조건: PackML의 Suspended(상류/하류 대기)나 Complete(배치 종료) 개념이 필요해지면 상태 추가를 검토한다.
구현 가이드: docs/guides/01_운전상태모델_구현가이드.md
