# Xeri Window와 View Source 연결하기

Xeri Window는 창의 위치·크기·상태와 정렬을 관리하고, 실제 화면 내용은 `IXeriUIViewSource`가 생성합니다. 저장 가능한 Window와 프로젝트 UI를 연결하려면 Window 상태와 View Session을 분리하는 것이 핵심입니다.

## 목적

창의 위치·크기·최소화 상태와 View-local 작업 상태를 서로 다른 소유권으로 분리하고, stable `ViewSourceID`를 통해 저장된 Window Record에서 UI를 다시 구성합니다.

## 역할 분리

```text
XeriWindowRecord
├─ 창 ID / 위치 / 크기 / 상태
├─ ViewSourceID
├─ ViewDataKey
└─ IXeriUISession
        ↓
IXeriUIViewResolver
        ↓
IXeriUIViewSource
        ↓
VisualElement
```

Window 시스템은 프로젝트 화면의 업무 상태를 알지 않고, View Source는 Window 이동·최소화 같은 상태를 소유하지 않습니다.

## 1. UI Session 정의

View가 재생성되어도 유지할 상태가 있으면 `IXeriUISession` 구현을 만듭니다.

```csharp
using System;
using inonego.Xeri.UI.Window;

[Serializable]
public sealed class SampleViewSession : IXeriUISession
{
    public string SearchText = string.Empty;
    public int SelectedIndex = -1;
}
```

Session에는 선택·검색·편집 중 값처럼 View 재생성 후 이어갈 상태를 두고, Window 위치나 크기는 넣지 않습니다.
## 2. View Source 구현

`IXeriUIViewSource`는 stable ID로 View를 생성하고 Session을 저장·복원합니다.

```csharp
using UnityEngine.UIElements;
using inonego.Xeri.UI.Window;

public sealed class SampleViewSource : IXeriUIViewSource
{
    public string ID => "sample.view";

    public VisualElement CreateView(XeriUIViewScope scope)
    {
        var root = new VisualElement();
        root.Add(new Label("Sample"));
        return root;
    }

    public void LoadSession(XeriUIViewScope scope)
    {
        if (scope.UISession is not SampleViewSession session) return;
        // Session 값을 다음 CreateView/Presenter가 사용할 상태로 준비한다.
    }

    public void SaveSession(XeriUIViewScope scope)
    {
        if (scope.UISession is not SampleViewSession session) return;
        // 현재 View 상태를 session에 반영한다.
    }
}
```

`LoadSession`은 View 생성 전에 호출되고, Window가 상태를 보존해야 할 때 `SaveSession`이 호출됩니다.
## 3. Resolver와 Canvas 연결

```csharp
using UnityEngine;

using inonego.Xeri.UI.Window;

var resolver = new XeriUIViewResolver();
resolver.Register(new SampleViewSource());

var canvas = new XeriWindowCanvas
(
    registry: null,
    viewResolver: resolver
);

var record = new XeriWindowRecord
{
    ID = "sample.window",
    Title = "Sample Window",
    Pos = new Vector2(80f, 80f),
    Size = new Vector2(480f, 320f),
    NormalPos = new Vector2(80f, 80f),
    NormalSize = new Vector2(480f, 320f),
    ViewSourceID = "sample.view",
    ViewDataKey = "sample-window-view",
    UISession = new SampleViewSession(),
};

XeriWindowHandle handle = canvas.AddWindow(record);
```

Canvas가 `ViewSourceID`를 Resolver로 해석하고 Session을 로드한 뒤 View를 생성합니다.
## 4. Window 제거

Window 수명이 끝나면 Canvas에서 제거합니다.

```csharp
canvas.RemoveWindow(handle);
```

Window 상태와 View Session을 외부에 저장해야 한다면 `XeriWindowRecord`를 프로젝트 저장 모델에 포함할 수 있지만, 실제 디스크 저장 형식과 저장 시점은 프로젝트가 소유합니다.

## 관련 문서

- [Xeri Window](../modules/window.md)
- [Xeri Window View](../modules/window-view.md)
- [Xeri 통합 패턴](https://inonego-unity.github.io/Xeri/docs/concepts/integration-patterns.html)