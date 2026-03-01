## Digital Twin-Pyro Process Mockup

디지털 트윈 기반 초저습 드라이목업을 활용한 **파이로프로세싱(Pyroprocessing) 실험실 자동화 및 VR 제어** 프로젝트

## 🎥 Demo
[![Unity 디지털 트윈](https://img.shields.io/badge/Unity_디지털_트윈-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://www.youtube.com/watch?v=DQgke5uXlOg)
[![Unity 디지털 트윈+VR](https://img.shields.io/badge/Unity_디지털_트윈+VR-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://youtu.be/N6-VXnxK1QI)

---

## ⚙️ Environment

| 항목 | 버전 / 정보 |
| :--- | :--- |
| **VR/HMD** | Meta Quest 3S |
| **Unity** | 2022.3.62f1 (Articulation Body 물리 엔진 활용) |
| **PLC** | LS ELECTRIC XG5000 (Vagabond Protocols 연동) |

### 📦 Unity Packages
- **Meta All-In-One SDK**: VR 환경 구축 및 핸드 트래킹 기술 구현
- **CoACD (Collision-Aware Convex Decomposition)**: 복합 메시의 물리 연산 최적화 및 정밀 충돌 구현
- **VagabondK.Protocols.LSElectric**: LS ELECTRIC PLC와의 이더넷(FEnet) 통신 및 데이터 미러링

---

## 🚀 Usage

### 1. PLC-VR 실시간 연동 (Vagabond Protocols)
- **통신 설정**: `Vagabond Protocols` 라이브러리를 사용하여 Unity와 실제 PLC 간의 이더넷 통신(TCP Port: 2004)을 연결
- **데이터 미러링**: I/O 맵(M, D, P 영역 등) 주소 테이블을 정의하여 가상 환경과 실제 하드웨어의 상태를 실시간으로 동기화
- **HMI 투영**: 실제 XP-Builder 화면을 VR 내 UI에 1:1로 매핑하여 가상 공간에서도 실제 제어 패널과 동일한 조작 및 모니터링

### 2. 가상 공정 시뮬레이션 및 제어
- **수동 제어**: VR 핸드 트래킹 기술을 활용해 갠트리 로봇을 직접 호출하거나 가상 UI 좌표값을 입력하여 조작
- **자동 시퀀스**: UB/USC 바스켓 조립·분해 등 총 8개의 공정 절차를 가상 시나리오로 실행하고 실제 하드웨어와 연동
- **충돌 예지**: 로봇 이동 경로 상의 간섭을 실시간 체크하여 충돌 위험 시 UI 메시지와 색상 전환을 통해 작업자에게 경고를 표시

---

## 📚 Reference

- [VagabondK.Protocols: LS ELECTRIC FEnet Protocol Library (GitHub)](https://github.com/Vagabond-K/VagabondK.Protocols)
