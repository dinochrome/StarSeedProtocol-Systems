namespace Code.Shared.ViewModels
{
    using System;
    using System.Collections.Generic;
    using Code.Core;
    using Contracts;
    using Code.Core.Utils;
    using Interfaces;
    using Code.Services;
    using CoreFlexible.Gameplay;
    using Models.Config;
    using Services;
    using UnityEngine.InputSystem;
    using UnityEngine.Localization;

    public class SaveSlotsViewModel : ISaveSlotsViewModel
    {
        private const string SAVE_KEY_OVERALL = "dialog.saveSlots.saveOverall",
            SAVE_KEY_EMPTY = "dialog.saveSlots.saveEmpty",
            SAVE_KEY_TITLE = "dialog.saveSlots.title",
            SAVE_KEY_LAST_SAVED = "dialog.saveSlots.lastSaved",
            BUTTON_DELETE = "dialog.saveSlots.deleteButtonTitle",
            BUTTON_BACK = "main.back",
            DELETE_CONFIRM = "dialog.saveSlots.deleteConfirmMessage",
            SLOT_ACCUSATIVE = "dialog.saveSlots.deleteConfirmMessageCell.accusativeCase";
        public event Action OnChanged;

        private bool dirty, requireRestart;

        public List<SaveSlotData> GetList()
        {
            List<SaveSlotData> saveSlots = new List<SaveSlotData>();
            var slotsData = SaveLoadManager.GetSaveSlotsData();

            for (var i = 0; i < slotsData.Length; i++)
            {
                var save = slotsData[i];
                var slotNumber = i + 1;
                TooltipHelper.TryGetLocalizedString(Const.Loc.ui, SAVE_KEY_TITLE, out var locTitle, slotNumber);
                if (save == null)
                {
                    TooltipHelper.TryGetLocalizedString(Const.Loc.ui, SAVE_KEY_EMPTY, out var locEmpty);

                    saveSlots.Add(new SaveSlotData(i, locTitle.GetLocalizedString(), locEmpty.GetLocalizedString(),
                        string.Empty));
                }
                else
                {
                    TooltipHelper.TryGetLocalizedString(Const.Loc.ui, SAVE_KEY_OVERALL, out var ls, save.metaData.day);

                    DateTimeOffset localTime = DateTimeOffset.FromUnixTimeSeconds(save.lastSavedUnixTime).ToLocalTime();
                    string text = $"{localTime:HH:mm dd/MM/yy}";

                    TooltipHelper.TryGetLocalizedString(Const.Loc.ui, SAVE_KEY_LAST_SAVED, out var locLastSaved, text);

                    saveSlots.Add(new SaveSlotData(i, locTitle.GetLocalizedString(), locLastSaved.GetLocalizedString(),
                        ls.GetLocalizedString()));
                }
            }

            return saveSlots;
        }
        public void Delete(int id)
        {
            SaveLoadManager.DeleteMetaSlot(id);
            OnChanged?.Invoke();
            dirty = requireRestart = true;
        }
        public void Select(int id)
        {
            SaveLoadManager.SetActiveSaveSlot(id);
            dirty = requireRestart = true;
        }
        public void Apply()
        {
            if (dirty)
            {
                SaveLoadManager.LoadMeta(Access.Get<ConfigMaster>().PlayerData.PlayerMetaData,
                    Access.Get<ConfigMaster>().PlayerData.SurfaceData);

                Access.Get<ChallengeService>().Precache();
                Access.Get<GuidanceService>().Precache();

                dirty = false;
            }
        }

        #region Get/Set
        public int GetSelectedId()
        {
            PlayerPrefsHelper.TryGetInt(PlayerPrefsHelper.Key.SaveSlot, out var slotIndex);
            return slotIndex;
        }
        public LocalizedString LocBack
        {
            get
            {
                TooltipHelper.TryGetLocalizedString(Const.Loc.ui, BUTTON_BACK, out var result);
                return result;
            }
        }

        public LocalizedString LocDelete
        {
            get
            {
                string buttonName = InputControlPath.ToHumanReadableString("<Keyboard>/Ctrl",
                    InputControlPath.HumanReadableStringOptions.OmitDevice
                );

                TooltipHelper.TryGetLocalizedString(Const.Loc.ui, BUTTON_DELETE, out var result, buttonName);
                return result;
            }
        }
        public string LocConfirmMessage
        {
            get
            {
                TooltipHelper.TryGetLocalizedString(Const.Loc.ui, SLOT_ACCUSATIVE, out var slot,
                    SaveLoadManager.ActiveSaveSlot + 1);

                TooltipHelper.TryGetLocalizedString(Const.Loc.ui, DELETE_CONFIRM, out var result,
                    TooltipHelper.WrapColor(slot.GetLocalizedString(), TooltipHelper.NewItem));
                return result.GetLocalizedString();
            }
        }
        public bool ConsumeRestartRequest()
        {
            if (requireRestart)
            {
                requireRestart = false;
                return true;
            }
            return false;
        }
        
        private SaveLoadManager SaveLoadManager => Access.Get<SaveLoadManager>();
        #endregion
    }
}