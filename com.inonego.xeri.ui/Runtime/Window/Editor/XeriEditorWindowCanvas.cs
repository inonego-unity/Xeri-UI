/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriEditorWindowCanvas.cs
수정일 : 2026-09-20

# 설명
EditorWindow 내부에 XeriWindowCanvas를 배치하는 기본 Editor host.
========================================================================= BLOCK_HEADER_END */

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window.Editor
{
    // ============================================================
    /// <summary>
    /// EditorWindow 내부에서 XeriWindowCanvas를 바로 사용할 수 있게 하는 host.
    /// </summary>
    // ============================================================
    public sealed class XeriEditorWindowCanvas : EditorWindow
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// EditorWindow root에 배치된 Xeri window canvas.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowCanvas Canvas => canvas;

        private XeriWindowCanvas canvas = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Xeri window canvas 예제 host를 연다.
        /// </summary>
        // ------------------------------------------------------------
        [MenuItem("Window/Xeri/UI/Window Canvas")]
        public static XeriEditorWindowCanvas Open()
        {
            var window = GetWindow<XeriEditorWindowCanvas>("Xeri Windows");
            window.Show();

            return window;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// EditorWindow UI를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;

            canvas = new XeriWindowCanvas();
            canvas.style.flexGrow = 1f;

            rootVisualElement.Add(canvas);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트와 외부 host에서 직접 canvas를 주입할 수 있게 한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetCanvas(XeriWindowCanvas canvas)
        {
            this.canvas = canvas;

            rootVisualElement.Clear();
            if (canvas != null)
            {
                rootVisualElement.Add(canvas);
            }
        }

    #endregion

    }
}
