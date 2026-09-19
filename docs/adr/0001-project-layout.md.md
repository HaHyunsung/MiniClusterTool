# 0001. 솔루션을 7개 프로젝트로 분리하고 장비 무관 영역을 별도 어셈블리로 뺀다

상태: 제안됨
날짜: 2026-09-07


## 배경

프로젝트 구조를 7개로 쪼개고 각 프로젝트 간의 참조 구조를 나눈다.
크게 자동화 장비 설계 시 공통으로 사용할 수 있는 Core 프로젝트,
장비에 맞게 설계되는 MiniCluster 프로젝트 등으로 나뉜다.
향후 새로운 장비 설계 시 이 프로젝트 구조는 유지하되, 프로젝트명과 코드를 변경해 사용할 수 있다.


## 선택지

### 안 1: 7개 프로젝트 구조

설명.

1. Hhs.Automation.Core
 - 자동화 장비 등 설계 시 공통으로 사용할 수 있는 부분
 - StateMachine, 시퀀스 엔진, IValve/ICylinder 등 특정 장비에 국한되지 않는 파츠, 시퀀스 로직 작성을 위한 엔진 등
 - 제일 하위 요소 어떤 프로젝트도 참조하지 않음

2. MiniCluster.Domain
 - 반도체 Cluster를 위한 모듈 및 개념
 - Cassette, LoadLock, TransferModule 등 정말 반도체 장비에서만 사용하는 요소들을 정의
 - Hhs.Automation.Core 프로젝트를 참조함.

3. MiniCluster.Infrastructure
 - 실제 외부 세계(ex. IO/통신/DB 등 무언가 실제 하드웨어 또는 소프트웨어)를 제어하는 곳
 - IOValve, TcpIpHeater, DBEditer 등 실제 외부 세계의 영향을 끼치는 요소를 정의
 - Hhs.Automation.Core와 MiniCluster.Domain 프로젝트를 참조함.

4. MiniCluster.App
 - 실제 Model(파츠, 모듈, DB 등)과 View(XMAL - UI 프로젝트)를 연결하는 곳
 - ex. UI에서 밸브 동작 버튼을 누르면 바로 Infrastructure의 인스턴스를 건드리는 게 아니라 이 프로젝트를 통해서 명령됨.
 - Hhs.Automation.Core와 MiniCluster.Domain 프로젝트를 참조함.

5. MiniCluster.Simulator
 - 시뮬레이션을 위한 요소를 정의
 - 실제 통신 파츠, 장비 신호 등 실제 장비 구동에 필요한 외부 세계를 시뮬레이션
 - 아무도 참조하지 않음. 우리 프로젝트와 통신 등으로만 연결됨.

6. MiniCluster.UI
 - Xmal 코드 및 그 cs가 존재하는 곳
 - 여기서 무언가 직접 파츠나 머신 등을 호출하는 건 금한다. App을 통해서 할 것
 - 실제 인스턴스가 선언되는 곳.
 - MiniCluster.Infrastructure MiniCluster.App 프로젝트를 참조함.

7. MiniCluster.Test
 - MiniCluster.Simulator 또는 외부 세계 없이 시퀀스, 구조 등을 빠르게 테스트 해보기 위함.



## 결정
Core를 Hhs.Automation.Core와 MiniCluster.Domain 둘로 쪼갬


## 결과
Core에 너무 많은 기능이 집중되는 것을 막고, Cluster 장비에서 필요한 부분과 모든 장비에서 공통으로 사용할 부분을 쪼갬.