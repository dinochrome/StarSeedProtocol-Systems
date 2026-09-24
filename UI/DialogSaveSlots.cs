namespace Code.UI
{
    using System;
    using Contracts;
    using Core.Utils;
    using Elements;
    using Interfaces;
    using UnityEngine;
    using Utils;

    public class DialogSaveSlots : AbstractUI, IDisposable
    {
        [SerializeField] private ElementSaveSlot prefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private SimpleButton confirm;
        [SerializeField] private SimpleButton delete;
        
        public event Action<string, int, Action<int>> OnRequireModalConfirm;

        private SelectionGroup<ElementSaveSlot> group = new();
        private HighlightGroup<ElementSaveSlot> highlightGroup = new();
        
        private ISaveSlotsViewModel viewModel;

        public void InitAndHide(ISaveSlotsViewModel viewModel)
        {
            isMute = true;
            this.viewModel = viewModel;
            viewModel.OnChanged += OnChanged;

            confirm.OnTrigger += Hide;
            confirm.SetLocalizedString(viewModel.LocBack);
            
            delete.OnTrigger += OnDelete;
            delete.SetLocalizedString(viewModel.LocDelete);

            var list = viewModel.GetList();
            foreach (var data in list)
            {
                var elementSaveSlot = Instantiate(prefab, container);
                elementSaveSlot.Init(data.id);
                elementSaveSlot.OnTriggerId += OnTriggerId;
                group.Add(elementSaveSlot);
                highlightGroup.Add(elementSaveSlot);
            }

            Hide();
            isMute = false;
        }

        void UpdateFields()
        {
            var list = viewModel.GetList();
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                group.Selectables[i].UpdateFields(data.name, data.lastSave, data.info);
            }

            group.Select(viewModel.GetSelectedId());
            highlightGroup.SetCurrent(group.Current).Deselect();
        }

        #region Overrides
        public override void Show()
        {
            UpdateFields();

            TakeInputContext();
            inputContext.OnEsc += Hide;
            inputContext.OnUiNavigation += InputOnUiNavigation;
            inputContext.OnUiAccept += OnConfirm;
            inputContext.OnUiEnter += OnConfirm;
            inputContext.OnUiSpecial += OnDelete;

            PlayAudio(Audio.OpenDialog);
            base.Show();
        }
        protected override void RemoveListenersFromContext()
        {
            if (inputContext == null) return;

            inputContext.OnEsc -= Hide;
            inputContext.OnUiNavigation -= InputOnUiNavigation;
            inputContext.OnUiAccept -= OnConfirm;
            inputContext.OnUiEnter -= OnConfirm;
            inputContext.OnUiSpecial -= OnDelete;
        }

        public override void Hide()
        {
            PlayAudio(Audio.CloseDialog);
            viewModel.Apply();
            base.Hide();
        }
        #endregion

        #region Events
        private void OnChanged() => UpdateFields();
        private void InputOnUiNavigation(Vector2 direction)
        {
            if (direction.IsUp())
            {
                highlightGroup.Previous();
            }
            else if (direction.IsDown())
            {
                highlightGroup.Next();
            }
        }
        private void OnConfirm()
        {
            OnTriggerId(highlightGroup.Current.Id);
            // OnTriggerId(group.Selected.Id);
        }
        private void OnTriggerId(int id)
        {
            group.Select(id);
            viewModel.Select(id);
        }
        private void OnDelete()
        {
            OnRequireModalConfirm?.Invoke(viewModel.LocConfirmMessage, group.Current.Id, viewModel.Delete);
        }
        #endregion

        #region Interface
        public void Dispose()
        {
            foreach (var button in group.Selectables)
            {
                button.OnTriggerId -= OnTriggerId;
            }

            confirm.OnTrigger -= Hide;
            delete.OnTrigger -= OnDelete;
            viewModel.OnChanged -= OnChanged;
            group.Clear();
            highlightGroup.Dispose();
        }
        #endregion

        public bool ConsumeRestartRequest() => viewModel.ConsumeRestartRequest();
    }
}