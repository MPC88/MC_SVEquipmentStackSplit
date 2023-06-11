using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.Button;

namespace MC_SVEquipmentStackSplit
{
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class Main : BaseUnityPlugin
	{
        // Plugin
		public const string pluginGuid = "mc.starvalor.equipmentstacksplit";
		public const string pluginName = "SV Equipment Stack Split";
		public const string pluginVersion = "1.0.1";

        // Mod
        private const int hangerPanelCode = 3;
        private static ShipInfo shipInfo;
        private static SpaceShip ss;
        private static InstalledEquipment selectedEquipment;
        private static ActiveEquipment selectedAE;

        private static GameObject btnDockUISplit;
        private static GameObject dlgInputAsset;
        private static GameObject dlgInput;
        private static InputField inputField;

        private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        public void Awake()
		{
            LoadAssets();
			Harmony.CreateAndPatchAll(typeof(Main));
        }

        internal void LoadAssets()
        {
            string pluginfolder = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);

            // Load assets
            string bundleName = "mc_svequipmentstacksplit";
            AssetBundle assets = AssetBundle.LoadFromFile($"{pluginfolder}\\{bundleName}");
            GameObject pack = assets.LoadAsset<GameObject>("Assets/mc_eqstacksplit.prefab");

            dlgInputAsset = pack.transform.Find("mc_eqstacksplitInput").gameObject;
        }

        [HarmonyPatch(typeof(DockingUI), nameof(DockingUI.OpenPanel))]
        [HarmonyPostfix]
        private static void DocingUIOpenPanel_Post(ShipInfo ___shipInfo, Inventory ___inventory, int code)
        {
            if (code == hangerPanelCode)
            {
                shipInfo = ___shipInfo;

                if (btnDockUISplit == null)
                    CreateUI(___inventory);

                btnDockUISplit.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(DockingUI), nameof(DockingUI.CloseDockingStation))]
        [HarmonyPrefix]
        private static void DockingUICloseDockingStation_Pre()
        {
            if (btnDockUISplit != null)
                btnDockUISplit.SetActive(false);

            CloseInputDialog();
        }

        [HarmonyPatch(typeof(ShipInfo), nameof(ShipInfo.SetItemKey))]
        [HarmonyPrefix]
        private static bool ShipInfoSetItemKey_Pre(KeyCode key)
        {
            try
            {
                if ((int)AccessTools.Field(typeof(ShipInfo), "selItemType").GetValue(shipInfo) ==
                    (int)SVUtil.SVUtil.GlobalItemType.equipment)
                {
                    int selItemIndex = (int)AccessTools.Field(typeof(ShipInfo), "selItemIndex").GetValue(shipInfo);
                    int selSlotIndex = (int)AccessTools.Field(typeof(ShipInfo), "selSlotIndex").GetValue(shipInfo);

                    int equipmentID = ss.shipData.equipments[selItemIndex].equipmentID;
                    int rarity = ss.shipData.equipments[selItemIndex].rarity;
                    int qnt = ss.shipData.equipments[selItemIndex].qnt;
                    ss.shipData.equipments[selItemIndex].buttonCode = key;

                    // Which item stack instance?
                    int stackInstance = -1;
                    for (int i = 0; i < ss.shipData.equipments.Count; i++)
                    {
                        InstalledEquipment ie = ss.shipData.equipments[i];
                        if (ie.equipmentID == equipmentID && ie.rarity == rarity && ie.qnt == qnt)
                        {
                            stackInstance++;
                            if (i == selItemIndex)
                                break;
                        }
                    }

                    // If we didn't find a stack, let the game handle it as normal
                    if (stackInstance < 0)
                        return true;

                    // Set key for approriate active equipment stack
                    if (ss.activeEquips != null)
                    {
                        int aeStack = 0;

                        for (int i = 0; i < ss.activeEquips.Count; i++)
                        {
                            if (ss.activeEquips[i].id == equipmentID && ss.activeEquips[i].rarity == rarity && ss.activeEquips[i].qnt == qnt)
                            {
                                if (aeStack == stackInstance)
                                {
                                    ss.activeEquips[i].key = key;
                                    break;
                                }
                                else
                                    aeStack++;
                            }
                        }
                    }

                    ((Transform)AccessTools.Field(typeof(ShipInfo), "itemPanel").GetValue(shipInfo)).GetChild(selSlotIndex).transform.GetChild(0).Find("ButtonName").GetComponent<Text>().text = PChar.ModifyKey(key.ToString());
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                return true;
            }
        }

        private static void CreateUI(Inventory inventory)
        {            
            Transform itemMainPanel = ((GameObject)AccessTools.Field(typeof(ShipInfo), "shipDataScreen").GetValue(shipInfo)).transform;
            GameObject templateBtn = ((Transform)AccessTools.Field(typeof(ShipInfo), "equipGO").GetValue(shipInfo)).Find("BtnRemove").gameObject;
            GameObject btnRemoveAll = GameObject.Find("BtnRemoveAll");

            btnDockUISplit = Instantiate(templateBtn);
            btnDockUISplit.name = "BtnEqSplitStack";
            btnDockUISplit.SetActive(true);
            btnDockUISplit.GetComponentInChildren<Text>().text = "Split Stack";
            btnDockUISplit.GetComponentInChildren<Text>().fontSize--;
            btnDockUISplit.SetActive(false);
            ButtonClickedEvent btnDockUISplitClickEvent = new Button.ButtonClickedEvent();
            btnDockUISplitClickEvent.AddListener(BtnDockUISplit_Click);
            btnDockUISplit.GetComponentInChildren<Button>().onClick = btnDockUISplitClickEvent;
            btnDockUISplit.transform.SetParent(btnRemoveAll.transform.parent);
            btnDockUISplit.layer = btnRemoveAll.layer;
            btnDockUISplit.transform.localPosition = new Vector3(btnRemoveAll.transform.localPosition.x,
                btnRemoveAll.transform.localPosition.y - ((btnRemoveAll.GetComponent<RectTransform>().rect.height * 1.5f) * 2f),
                btnRemoveAll.transform.localPosition.z); ;
            btnDockUISplit.transform.localScale = btnRemoveAll.transform.localScale;

            dlgInput = GameObject.Instantiate(dlgInputAsset);
            dlgInput.transform.SetParent(itemMainPanel.parent.parent, false);
            dlgInput.layer = itemMainPanel.gameObject.layer;
            dlgInput.SetActive(false);
            inputField = dlgInput.transform.GetComponentInChildren<InputField>();
                        
            ButtonClickedEvent btnOKClickEvent = new Button.ButtonClickedEvent();
            btnOKClickEvent.AddListener(BtnOK_Click);
            dlgInput.transform.GetChild(0).GetChild(3).GetComponent<Button>().onClick = btnOKClickEvent;
                        
            ButtonClickedEvent btnCancelClickEvent = new Button.ButtonClickedEvent();
            btnCancelClickEvent.AddListener(BtnOK_Click);
            dlgInput.transform.GetChild(0).GetChild(2).GetComponent<Button>().onClick = btnCancelClickEvent;
        }

        private static void BtnDockUISplit_Click()
        {
            ss = null;

            if ((int)AccessTools.Field(typeof(ShipInfo), "selItemType").GetValue(shipInfo) !=
                (int)SVUtil.SVUtil.GlobalItemType.equipment)
            {
                InfoPanelControl.inst.ShowWarning("Can only split equipment stacks.", 1, false);
                return;
            }

            int selItemIndex = (int)AccessTools.Field(typeof(ShipInfo), "selItemIndex").GetValue(shipInfo);
            ss = (SpaceShip)AccessTools.Field(typeof(ShipInfo), "ss").GetValue(shipInfo);
            if ((int)AccessTools.Field(typeof(ShipInfo), "selSlotIndex").GetValue(shipInfo) < 0 || selItemIndex < 0 || selItemIndex > ss.shipData.equipments.Count)
            {
                InfoPanelControl.inst.ShowWarning("Selected equipment index not valid.", 1, false);
                return;
            }
            selectedEquipment = ss.shipData.equipments[selItemIndex];

            if (selectedEquipment.qnt <= 1)
            {
                InfoPanelControl.inst.ShowWarning("Cannot split a single item.", 1, false);
                return;
            }

            foreach (ActiveEquipment ae in ss.activeEquips)
            {
                if (ae.id == selectedEquipment.equipmentID && ae.rarity == selectedEquipment.rarity)
                    selectedAE = ae;                    
            }

            if (selectedAE != null)
            {
                selectedAE.ActivateDeactivate(false, null);
            }
            else
            {
                InfoPanelControl.inst.ShowWarning("Can only split activatable equipment stacks.", 1, false);
                return;
            }

            if (dlgInput == null)
            {
                InfoPanelControl.inst.ShowWarning("UI is not initialised.  Try redocking.", 1, false);
                return;
            }
            
            GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerControl>().blockKeyboard = true;
            dlgInput.SetActive(true);
        }

        private static void BtnOK_Click()
        {
            if (inputField == null)
            {
                InfoPanelControl.inst.ShowWarning("UI is not intialised.  Try redocking.", 1, false);
                CloseInputDialog();
                return;
            }

            if (ss == null || selectedEquipment == null)
            {
                InfoPanelControl.inst.ShowWarning("Error finding space ship or selected equipment.", 1, false);
                CloseInputDialog();
                return;
            }

            int newStack = -1;
            Int32.TryParse(inputField.text, out newStack);
            if(newStack <=0)
            {
                InfoPanelControl.inst.ShowWarning("Can only create stacks of positive whole numbers.", 1, false);
                CloseInputDialog();
                return;
            }

            if (newStack >= selectedEquipment.qnt)
            {
                InfoPanelControl.inst.ShowWarning("New stack size must be less than existing stack and greater than 0.", 1, false);
                CloseInputDialog();
                return;
            }

            selectedEquipment.qnt -= newStack;
            ss.shipData.equipments.Add(new InstalledEquipment()
            {
                equipmentID = selectedEquipment.equipmentID,
                rarity = selectedEquipment.rarity,
                qnt = newStack,
                buttonCode = selectedEquipment.buttonCode
            });

            selectedAE.qnt -= newStack;
            ActiveEquipment.AddActivatedEquipment(EquipmentDB.GetEquipment(selectedEquipment.equipmentID),
                ss,
                selectedAE.key,
                selectedAE.rarity,
                newStack);

            Inventory.instance.DeselectItems();
            Inventory.instance.RefreshIfOpen(null, true, true);
            CloseInputDialog();
        }

        private static void BtnCancel_Click()
        {
            CloseInputDialog();
        }

        private static void CloseInputDialog()
        {
            if (dlgInput != null)
            {
                GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerControl>().blockKeyboard = false;
                dlgInput.SetActive(false);
            }
        }
    }
}
