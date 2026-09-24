/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationModal.cs
수정일 : 2026-10-07

# 설명
Validation Lab과 Application Window가 production UITKModal 경로로 Modal을 열 수 있게 하는 공통 helper.
Modal visual은 Validation 전용 style을 사용하지만 stack, focus와 lifetime은 Xeri UI ModalController가 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Modal 닫기 입력과 종료 알림을 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationModalHandle : IDisposable
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 실행 세션.
        /// </summary>
        // ------------------------------------------------------------
        public ModalSession Session { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 Modal 세션의 종료 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => Session == null || Session.IsDisposed;

        private readonly Button closeButton = null;

    #endregion

    #region 입력 연결과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Modal 닫기 입력과 종료 알림을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationModalHandle
        (
            ModalSession session,
            Button closeButton
        ) : base()
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            this.closeButton = closeButton ?? throw new ArgumentNullException(nameof(closeButton));
            this.closeButton.clicked += Dispose;
            Session.RegisterChild(new Lease(() => this.closeButton.clicked -= Dispose));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구독과 소유 수명을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            Session.Dispose();
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// Validation Modal 콘텐츠를 생성한다.
    /// </summary>
    // ============================================================
    internal static class XeriValidationModal
    {

    #region Modal 콘텐츠 생성

        // ------------------------------------------------------------
        /// <summary>
        /// Modal을 열고 닫기 알림을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static XeriValidationModalHandle Open
        (
            UIContext context,
            PresentationTarget target,
            XeriValidationAssets assets,
            XeriValidationAppearance appearance,
            string title,
            string description,
            Action openNested = null
        )
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            var root = new VisualElement
            {
                name = "XeriValidationModal",
            };
            root.AddToClassList("xeri-validation-modal");
            assets.ApplyAppStyles(root);

            var card = new VisualElement();
            card.AddToClassList("xeri-validation-modal__card");

            var eyebrow = new Label("Xeri");
            eyebrow.AddToClassList("xeri-validation-modal__eyebrow");

            var titleLabel = new Label(title ?? "Modal");
            titleLabel.AddToClassList("xeri-validation-modal__title");

            var copy = new Label(description ?? string.Empty);
            copy.AddToClassList("xeri-validation-modal__copy");

            var actions = new VisualElement();
            actions.AddToClassList("xeri-validation-modal__actions");

            if (openNested != null)
            {
                var nestedButton = new Button(openNested)
                {
                    text = "Open Nested",
                };
                nestedButton.AddToClassList("xeri-validation-button");
                nestedButton.AddToClassList("xeri-validation-button--secondary");
                actions.Add(nestedButton);
            }

            var closeButton = new Button
            {
                text = "Close",
            };
            closeButton.AddToClassList("xeri-validation-button");
            closeButton.AddToClassList("xeri-validation-button--primary");
            actions.Add(closeButton);

            card.Add(eyebrow);
            card.Add(titleLabel);
            card.Add(copy);
            card.Add(actions);
            root.Add(card);

            var session = UITKModal.OpenWithFocus
            (
                context,
                target,
                root,
                new XeriValidationVisualFocusScope(root, closeButton)
            );
            try
            {
                session.RegisterChild(appearance.Bind(root));
                return new XeriValidationModalHandle(session, closeButton);
            }
            catch (Exception exception)
            {
                // 외관이나 입력 연결이 실패해도 열린 Modal 소유권을 남기지 않는다.
                try
                {
                    session.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(exception, cleanupException);
                }

                throw;
            }
        }

    #endregion

    }
}
