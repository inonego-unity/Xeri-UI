# Xeri UI Bar

Bar 모듈은 값과 진행 상태를 UGUI와 UI Toolkit 표시로 연결합니다.

`BarState`가 표시 값을 관리하고 `UGUIBar`, `UITKBar`가 backend 표현을 담당합니다.
application Screen/Modal lifecycle이 필요하지 않은 표시 primitive지만 UI rendering 자체가 목적이므로 Xeri UI package에서 소유합니다.
