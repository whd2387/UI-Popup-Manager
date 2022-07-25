# UI-Popup-Manager

Modeless가 포함된 UI 팝업 구조입니다.
UITopMostCanvas는 기본 UI canvas보다 앞에 띄워지는 캔버스입니다. 공지사항 등 기본 UI canvas에 영향을 주지 않는 범위에서 보다 먼저 보여지게 하기 위해 사용합니다.
팝업 처리과정을 Queue에 담아 팝업의 Init과 Release 간에 동기화 처리를 합니다.
3가지 이벤트가 존재합니다.
- **OnHighest**     : 해당 팝업이 최상단에 위치할 때 불리는 이벤트
- **OnBelow**        : 해당 팝업이 최상단에서 최상단이 아닌 상태로 바뀔 때 불리는 이벤트
- **OnNotification** : UIInstance - PostPopupNotification(Type ~~)을 호출하면 모든 팝업에 해당 이벤트 타입을 전달해주는 이벤트
Android Escape button 처리를 추가 하였습니다.
안드로이드 기기에서 뒤로가기 버튼을 누를 시 가장 최근의 팝업부터 1개씩 닫힙니다.
뒤로가기 버튼을 눌러도 닫히게 하고싶지 않은 팝업을 위해 CanCloseByBackButton 플래그를 추가하였습니다.
