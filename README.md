# Digital Twin-Pyro Process Mockup

디지털 트윈 기반 초저습 드라이목업을 활용한 **파이로프로세싱(Pyroprocessing) 실험실 자동화 및 VR 제어** 프로젝트입니다. 이 시스템은 이슬점 -40 °C 이하의 초저습 환경을 모사하며, 실제 PLC와 Unity 가상 환경을 양방향으로 연동하여 실시간 제어 및 모니터링을 지원합니다.

## 🎥 Demo
[![YouTube](https://img.shields.io/badge/YouTube-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://youtu.be/N6-VXnxK1QI)

---

## ⚙️ Environment

| 항목 | 버전 / 정보 |
| :--- | :--- |
| **OS** | Windows 11 |
| **VR/HMD** | Meta Quest 2 / 3S (Hand Tracking 지원) |
| **Unity** | 2022.3.62f1 (Articulation Body 물리 엔진 활용) |
| **PLC** | LS ELECTRIC XG5000 (Vagabond Protocols 연동) |
| **Modeling** | Blender (메시 분리 및 피벗 정렬) |

### 📦 Unity Packages
- **Meta All-In-One SDK**: VR 환경 구축 및 핸드 트래킹 기술 구현
- **CoACD (Collision-Aware Convex Decomposition)**: 복합 메시의 물리 연산 최적화 및 정밀 충돌 구현
- **VagabondK.Protocols.LSElectric**: LS ELECTRIC PLC와의 이더넷(FEnet) 통신 및 데이터 미러링

---

## 🚀 Usage

### 1. PLC-VR 실시간 연동 (Vagabond Protocols)
- **통신 설정**: `Vagabond Protocols` 라이브러리를 사용하여 Unity와 실제 PLC 간의 이더넷 통신(TCP Port: 2004)을 연결합니다.
- **데이터 미러링**: I/O 맵(M, D, P 영역 등) 주소 테이블을 정의하여 가상 환경과 실제 하드웨어의 상태를 실시간으로 동기화합니다.
- **HMI 투영**: 실제 XP-Builder 화면을 VR 내 UI에 1:1로 매핑하여 가상 공간에서도 실제 제어 패널과 동일한 조작 및 모니터링이 가능합니다.

### 2. 가상 공정 시뮬레이션 및 제어
- **수동 제어**: VR 핸드 트래킹 기술을 활용해 갠트리 로봇을 직접 호출하거나 가상 UI 좌표값을 입력하여 조작합니다.
- **자동 시퀀스**: UB/USC 바스켓 조립·분해 등 총 8개의 공정 절차를 가상 시나리오로 실행하고 실제 하드웨어와 연동합니다.
- **충돌 예지**: 로봇 이동 경로 상의 간섭을 실시간 체크하여 충돌 위험 시 UI 메시지와 색상 전환을 통해 작업자에게 경고를 표시합니다.

### 3. 물리 엔진 최적화 (CoACD & Articulation Body)
- **충돌 최적화**: `CoACD` 알고리즘을 활용해 오목한 장비 메시를 여러 개의 볼록한 조각(Convex Hull)으로 분할하여 물리 연산 효율과 충돌 정밀도를 확보합니다.
- **물리 튜닝**: `Articulation Drive`의 Force limit, Damping 값을 실제 서보 모터 사양에 맞춰 조정하여 실제 장비의 가감속 및 토크 거동을 모사합니다.

---

## 🛠️ Key Research Findings

- **양방향 제어 루프**: 가상 환경의 명령이 PLC 태그를 세트하고, 실제 장비의 물리적 응답이 다시 가상 시퀀스로 피드백되는 양방향 연동을 구현했습니다.
- **물리적 안정성 확보**: 그리퍼 결합 직후의 정렬 루틴과 비가시적 Articulation Body 체인 설계를 통해 도킹 시점의 순간이동 및 진동 현상을 해결했습니다.
- **환경 통합 관제**: 운전 장비 외에도 온도, 습도, HVAC 시스템 등 실험실 전반의 환경 데이터를 통합 모니터링할 수 있는 플랫폼을 구축했습니다.

---

## 📚 Reference

- [VagabondK.Protocols: LS ELECTRIC FEnet Protocol Library (GitHub)](https://github.com/Vagabond-K/VagabondK.Protocols)
