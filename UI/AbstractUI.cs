namespace Code.UI
{
    using System;
    using Contracts;
    using Interfaces;
    using Services;
    using UnityEngine;
    using UnityEngine.UI;

    public class AbstractUI : MonoBehaviour
    {
        protected Action onFinish;
        protected IInput inputContext;
        protected bool isMute;

        private RectTransform rectTransform;
        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }

                return rectTransform;
            }
        }

        public virtual void Show() => Visible = true;
        public virtual void Hide()
        {
            if (!Visible) return;
            TryDismissInputContext();
            Visible = false;
            onFinish?.Invoke();
            onFinish = null;
        }
        public void Toggle()
        {
            if (Visible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }
        public bool Visible
        {
            get => gameObject.activeSelf;
            private set => gameObject.SetActive(value);
        }
        
        /// <summary> Returns column count of dynamic grid layout </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
        protected int GetItemsPerRow(GridLayoutGroup grid)
        {
            var rect = (RectTransform)grid.transform;

            float availableWidth = rect.rect.width - grid.padding.left - grid.padding.right;

            float cellWidth = grid.cellSize.x;
            float spacing = grid.spacing.x;

            return Mathf.Max(1, Mathf.FloorToInt((availableWidth + spacing) / (cellWidth + spacing)));
        }
        protected void PlayAudio(Audio audio)
        {
            if (isMute) return;
            Access.Get<IAudioPlayer>().Play(audio);
        }
        protected void TakeInputContext(string owner = "")
        {
            if (inputContext != null)
            {
                Debug.LogError($"InputContext is not null and {name} {owner} tries to take it again");
                return;
            }

            inputContext = Access.Get<IInputRouter>().GetNewContext();
            inputContext.Owner = $"{name} : ({owner})";
        }
        protected virtual void RemoveListenersFromContext() { }
        private void TryDismissInputContext()
        {
            RemoveListenersFromContext();
            inputContext?.Dismiss();
            inputContext = null;
        }
    }
}