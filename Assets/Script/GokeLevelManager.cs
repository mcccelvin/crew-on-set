using PlayerPrefs = GameSavePrefs;
using System.Collections;
using System.Collections.Generic;
using Player.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class GokeLevelManager : MonoBehaviour
{
    public static GokeLevelManager Instance;

    private enum GokeLevelStep
    {
        Recap,
        PrepareNextLevel,
        IntroduceAlmanac,
        OpenAlmanac,
        CloseAlmanac,
        IntroduceCamera,
        BuyCamera,
        BuySDCard,
        Checkout,
        CloseShop,
        IntroducePickup,
        PickUpCamera,
        IntroduceSDCardPickup,
        PickUpSDCard,
        IntroduceSDInsert,
        InsertSDCard,
        IntroduceCameraView,
        OpenCameraView,
        InspectCameraFeatures,
        ExplainCameraFeatures,
        IntroduceEquipmentAlmanac,
        OpenEquipmentAlmanac,
        CloseEquipmentAlmanac,
        IntroduceContract,
        IntroduceLightPurchase,
        BuyLights,
        LightCheckout,
        CloseLightShop,
        IntroduceLightPickup,
        PickUpLights,
        IntroduceLightingSetup,
        PlaceKeyLight,
        ExplainLightingPlacement,
        PlaceFillLight,
        ExplainLightingSettings,
        PlaceBackLight,
        ObserveLightingSetup,
        LightingPracticeComplete,
        OfferContract,
        IntroduceTechniques,
        OpenTechniquesAlmanac,
        CloseTechniquesAlmanac,
        OpenQualifications,
        CloseQualifications,
        ExplainStage,
        ExplainComposition,
        ExplainLighting,
        ExplainPostProduction,
        ContractBriefing,
        LevelActive
    }

    private bool isLevelStarted = false;
    private bool isBriefingOpen = false;
    private GuidedPracticeLesson practiceLesson;
    private Transform cameraPracticeMarker;
    private bool cameraPracticeViewOpen;
    private GokeLevelStep currentStep;
    private TutorialManager tutorialManager;
    private ContractUIManager contractUIManager;
    private int level2CameraItemIndex = -1;
    private int sdCardItemIndex = -1;
    private int lightItemIndex = -1;
    private int lightsAddedToCart = 0;
    private bool hasPickedUpLevel2Camera = false;
    private bool hasPickedUpSDCard = false;
    private HashSet<int> pickedUpPracticeLights = new HashSet<int>();
    private HashSet<int> placedPracticeLights = new HashSet<int>();
    private List<FilmLightItem> practiceLights = new List<FilmLightItem>();
    private GameObject lightingPracticeRoot;
    private GameObject lightingPracticeWall;
    private DirectorTerminal lightingPracticeDirector;
    private Transform lightingPracticeTarget;
    private Transform keyPlacementMarker;
    private Transform fillPlacementMarker;
    private Transform backPlacementMarker;
    private List<TextMeshPro> practiceMarkerLabels = new List<TextMeshPro>();
    private bool hasCompletedRuleOfThirdsPractice = false;
    private bool awaitingFramingAcknowledgement;
    private float ruleOfThirdsPracticeTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void OnDestroy()
    {
        practiceLesson?.Release();
        CleanUpLightingPractice();
        if (Instance == this) Instance = null;
    }

    public void DisableForDevTesting()
    {
        if (!isLevelStarted) return;
        StopAllCoroutines();
        practiceLesson?.Release();
        practiceLesson = null;
        awaitingFramingAcknowledgement = false;
        if (cameraPracticeMarker != null) cameraPracticeMarker.gameObject.SetActive(false);
        CleanUpLightingPractice();
        StartContract();
        enabled = false;
        if (PlayerPrefs.GetInt("GokeContractAccepted", 0) == 0) OfferContract();
    }

    private void LateUpdate()
    {
        practiceLesson?.Tick();
        CampaignGuidance.Update(tutorialManager, isBriefingOpen || (practiceLesson != null && practiceLesson.IsExplaining) ? "" : currentStep.ToString(), 2);
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        foreach (TextMeshPro markerLabel in practiceMarkerLabels)
        {
            if (markerLabel == null) continue;
            markerLabel.transform.forward = mainCamera.transform.position - markerLabel.transform.position;
        }
    }

    public void BeginLevel(TutorialManager tutorialManager)
    {
        if (isLevelStarted) return;

        this.tutorialManager = tutorialManager;
        isLevelStarted = true;
        isBriefingOpen = true;
        currentStep = GokeLevelStep.Recap;

        CampaignProgression.SetCurrentLevel(2);

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        bool restartLevelIntroduction = CampaignProgression.ConsumeCheatIntroduction(2);
        if (restartLevelIntroduction)
        {
            PlayerPrefs.DeleteKey("Level2CameraPurchased");
            PlayerPrefs.Save();
        }

        CleanUpStudio();
        SetupLevel2Camera();
        SetupContractUI();

        bool contractAlreadyAccepted = PlayerPrefs.GetInt("GokeContractAccepted", 0) == 1;

        if (AlmanacManager.Instance != null)
        {
            if (!contractAlreadyAccepted || restartLevelIntroduction) AlmanacManager.Instance.PrepareLevelIntroduction(2);
        }

        if (contractAlreadyAccepted && !restartLevelIntroduction)
        {
            if (CareerManager.Instance != null) CareerManager.Instance.currentActiveJob = "Goke Cola";
            if (contractUIManager != null) contractUIManager.UnlockQualifications();
            StartContract();
            if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("You did it! Your first commercial, from the empty stage to the final cut. Take a breath; we've got a new client coming in.", TutorialUIManager.Instance.poseHappy, true, false);
        }

        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        StartCoroutine(UnlockPlayerAfterSpace());
    }

    public void CloseBriefing()
    {
        if (awaitingFramingAcknowledgement)
        {
            var ui = TutorialUIManager.Instance;
            if (ui != null && !ui.CanAdvanceBossDialogue()) return;
            awaitingFramingAcknowledgement = false;
            isBriefingOpen = false;
            if (ui != null)
            {
                ui.HideBossDialogue();
                ui.SetupTasks(new[] { "[Left Click] Leave the viewfinder" });
            }
            if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
            if (!cameraPracticeViewOpen) OnCameraViewExited("Level 2 Camera");
            return;
        }
        if (practiceLesson != null) { practiceLesson.Continue(); return; }
        if (!isBriefingOpen) return;

        if (currentStep == GokeLevelStep.Recap)
        {
            ShowNextLevelPreparation();
            return;
        }

        if (currentStep == GokeLevelStep.PrepareNextLevel)
        {
            ShowAlmanacIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceAlmanac)
        {
            StartAlmanacIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceCamera)
        {
            StartCameraPurchase();
            return;
        }

        if (currentStep == GokeLevelStep.IntroducePickup)
        {
            StartCameraPickup();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceSDCardPickup)
        {
            StartSDCardPickup();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceSDInsert)
        {
            StartSDCardInsertion();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceCameraView)
        {
            StartCameraFeatureInspection();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainCameraFeatures)
        {
            ShowEquipmentAlmanacIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceEquipmentAlmanac)
        {
            StartEquipmentAlmanacReview();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceContract)
        {
            OfferContract();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceLightPurchase)
        {
            StartLightPurchase();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceLightPickup)
        {
            StartLightPickup();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceLightingSetup)
        {
            StartKeyLightPractice();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainLightingPlacement)
        {
            StartFillLightPractice();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainLightingSettings)
        {
            StartBackLightPractice();
            return;
        }

        if (currentStep == GokeLevelStep.LightingPracticeComplete)
        {
            ShowCameraIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.OfferContract)
        {
            AcceptContract();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceTechniques)
        {
            StartTechniquesAlmanacReview();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainStage)
        {
            ShowCompositionTutorial();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainComposition)
        {
            ShowLightingTutorial();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainLighting)
        {
            ShowPostProductionTutorial();
            return;
        }

        if (currentStep == GokeLevelStep.ExplainPostProduction)
        {
            ShowContractBriefing();
            return;
        }

        if (currentStep == GokeLevelStep.ContractBriefing)
        {
            StartContract();
        }
    }

    public bool IsBriefingActive()
    {
        return isBriefingOpen || (practiceLesson != null && practiceLesson.IsExplaining);
    }

    public bool IsEquipmentIntroductionActive()
    {
        return isLevelStarted && currentStep != GokeLevelStep.LevelActive;
    }

    public bool CanOpenAlmanac()
    {
        return currentStep == GokeLevelStep.OpenAlmanac ||
               currentStep == GokeLevelStep.CloseAlmanac ||
               currentStep == GokeLevelStep.OpenEquipmentAlmanac ||
               currentStep == GokeLevelStep.CloseEquipmentAlmanac ||
               currentStep == GokeLevelStep.OpenTechniquesAlmanac ||
               currentStep == GokeLevelStep.CloseTechniquesAlmanac ||
               currentStep == GokeLevelStep.OpenQualifications ||
               currentStep == GokeLevelStep.CloseQualifications ||
               currentStep == GokeLevelStep.ExplainStage ||
               currentStep == GokeLevelStep.ExplainComposition ||
               currentStep == GokeLevelStep.ExplainLighting ||
               currentStep == GokeLevelStep.ExplainPostProduction ||
               currentStep == GokeLevelStep.ContractBriefing ||
               currentStep == GokeLevelStep.LevelActive;
    }

    public bool CanOpenContractQualifications()
    {
        return currentStep == GokeLevelStep.OpenQualifications ||
               currentStep == GokeLevelStep.CloseQualifications ||
               currentStep == GokeLevelStep.ContractBriefing ||
               currentStep == GokeLevelStep.LevelActive;
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (currentStep == GokeLevelStep.BuyLights)
        {
            if (itemIndex != lightItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Choose the 160 LED Panel under LIGHTS. That's the one for our setup.");
                return false;
            }

            lightsAddedToCart++;

            if (lightsAddedToCart >= 3)
            {
                currentStep = GokeLevelStep.LightCheckout;

                if (TutorialUIManager.Instance != null)
                {
                    TutorialUIManager.Instance.SetupTasks(new string[] { "- Confirm the purchase of all three 160 LED Panels" });
                }
            }
            else if (TutorialUIManager.Instance != null)
            {
                int remainingLights = 3 - lightsAddedToCart;
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add " + remainingLights + " more 160 LED Panel" + (remainingLights == 1 ? "" : "s") + " to your cart" });
            }

            return true;
        }

        if (currentStep == GokeLevelStep.LightCheckout)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Three lights in the cart. Click CONFIRM and we'll collect them.");
            return false;
        }

        if (currentStep == GokeLevelStep.BuyCamera)
        {
            if (itemIndex != level2CameraItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Let's get the Level 2 Camera into the cart first.");
                return false;
            }

            currentStep = GokeLevelStep.BuySDCard;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Now add an SD Card to your cart" });
            }

            return true;
        }

        if (currentStep == GokeLevelStep.BuySDCard)
        {
            if (itemIndex != sdCardItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Add a blank SD Card too. We'll need it for the camera test.");
                return false;
            }

            currentStep = GokeLevelStep.Checkout;

            if (TutorialUIManager.Instance != null)
            {
                bool alreadyOwnsCamera = PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1;
                TutorialUIManager.Instance.SetupTasks(new string[] { alreadyOwnsCamera ? "- Confirm your SD Card purchase" : "- Confirm your Camera and SD Card purchase" });
            }

            return true;
        }

        if (currentStep == GokeLevelStep.Checkout)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("That's our gear sorted. Click CONFIRM to place the order.");
            return false;
        }

        if (tutorialManager != null) tutorialManager.ShowWarning("Let's finish the step on the left, then we'll carry on.");
        return false;
    }

    public bool CanConfirmPurchase()
    {
        if (currentStep == GokeLevelStep.Checkout || currentStep == GokeLevelStep.LightCheckout) return true;

        if (currentStep == GokeLevelStep.BuyLights)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("We need three 160 LED Panels for this setup. Check the cart before confirming.");
            return false;
        }

        if (currentStep == GokeLevelStep.BuyCamera)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("We're missing the Level 2 Camera. Add it before confirming.");
            return false;
        }

        if (currentStep == GokeLevelStep.BuySDCard)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("There's no SD Card in the order yet. Add one so we can record.");
            return false;
        }

        return true;
    }

    public bool CanCancelPurchase()
    {
        if (currentStep != GokeLevelStep.BuyCamera &&
            currentStep != GokeLevelStep.BuySDCard &&
            currentStep != GokeLevelStep.Checkout &&
            currentStep != GokeLevelStep.BuyLights &&
            currentStep != GokeLevelStep.LightCheckout) return true;

        if (tutorialManager != null)
        {
            if (currentStep == GokeLevelStep.BuyLights || currentStep == GokeLevelStep.LightCheckout)
            {
                tutorialManager.ShowWarning("We'll need all three lights. Keep them in the cart and click CONFIRM.");
            }
            else
            {
                bool alreadyOwnsCamera = PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1;
                tutorialManager.ShowWarning(alreadyOwnsCamera ? "Keep that SD Card in the order and click CONFIRM." : "Keep the camera and card in the order, then click CONFIRM.");
            }
        }
        return false;
    }

    public bool CanInsertSDCard(string equipmentName)
    {
        if (currentStep != GokeLevelStep.InsertSDCard)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Let's finish the step on the left, then we'll carry on.");
            return false;
        }

        if (equipmentName != "Level 2 Camera")
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Select the Level 2 Camera and press [C] to load the SD Card first.");
            return false;
        }

        return true;
    }

    public void OnShopOpened()
    {
        if (currentStep == GokeLevelStep.BuyLights)
        {
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Open LIGHTS and add three 160 LED Panels to your cart" });
            }
            return;
        }

        if (currentStep != GokeLevelStep.BuyCamera && currentStep != GokeLevelStep.BuySDCard) return;

        if (TutorialUIManager.Instance != null)
        {
            if (currentStep == GokeLevelStep.BuyCamera)
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add the Level 2 Camera to your cart", "- Add an SD Card before checkout" });
            else
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add a blank SD Card to your cart and confirm purchase" });
        }
    }

    public void OnEquipmentBought(int itemsCount)
    {
        if (currentStep == GokeLevelStep.LightCheckout)
        {
            currentStep = GokeLevelStep.CloseLightShop;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetDynamicGlow("shop", false);
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Close the Equipment Shop" });
            }
            return;
        }

        if (currentStep != GokeLevelStep.Checkout) return;

        if (AlmanacManager.Instance != null)
        {
            AlmanacManager.Instance.UnlockTutorialEquipment();
            AlmanacManager.Instance.UnlockKnowledge("level_2_camera");
        }

        currentStep = GokeLevelStep.CloseShop;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetDynamicGlow("shop", false);
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Close the Equipment Shop" });
        }
    }

    public void OnShopClosed()
    {
        if (currentStep == GokeLevelStep.CloseLightShop)
        {
            currentStep = GokeLevelStep.IntroduceLightPickup;
            isBriefingOpen = true;

            if (tutorialManager != null) tutorialManager.PointLineAt("");

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.ShowBossDialogue("Our three 160 LED Panels have arrived. Pick each one up with <color=red>[E]</color>, then use <color=red>[1-5]</color> to choose which one you're holding.", TutorialUIManager.Instance.posePoint, true, false);
            }
            return;
        }

        if (currentStep != GokeLevelStep.CloseShop) return;

        currentStep = GokeLevelStep.IntroducePickup;
        isBriefingOpen = true;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("There's our camera gear on the delivery table. Pick up the <color=yellow>Level 2 Camera</color> with <color=red>[E]</color> first; we'll get the card next.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    public void OnAlmanacOpened()
    {
        if (currentStep == GokeLevelStep.OpenAlmanac)
        {
            currentStep = GokeLevelStep.CloseAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Review your Level 1 equipment and technique guides", "- Press <color=red>[P]</color> or CLOSE when finished" });
            }
            return;
        }

        if (currentStep == GokeLevelStep.OpenEquipmentAlmanac)
        {
            currentStep = GokeLevelStep.CloseEquipmentAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Open EQUIPMENT", "- Review the Level 2 Camera features", "- Press <color=red>[P]</color> or CLOSE when finished" });
            }
            return;
        }

        if (currentStep == GokeLevelStep.OpenTechniquesAlmanac)
        {
            currentStep = GokeLevelStep.CloseTechniquesAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Open TECHNIQUES", "- Review Rule of Thirds and 3-Point Lighting", "- Press <color=red>[P]</color> or CLOSE when finished" });
            }
        }

    }

    public void OnAlmanacClosed()
    {
        if (currentStep == GokeLevelStep.CloseAlmanac)
        {
            ShowLightPurchaseIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.CloseEquipmentAlmanac)
        {
            ShowContractIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.CloseTechniquesAlmanac)
        {
            StartQualificationsIntroduction();
            return;
        }

    }

    public void OnContractQualificationsOpened()
    {
        if (currentStep != GokeLevelStep.OpenQualifications) return;

        currentStep = GokeLevelStep.CloseQualifications;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Review Rule of Thirds", "- Review 3-Point Lighting", "- Press <color=red>[TAB]</color> when finished" });
        }
    }

    public void OnContractQualificationsClosed()
    {
        if (currentStep != GokeLevelStep.CloseQualifications) return;

        StartProductionTutorial();
    }

    public void OnCameraPickedUp(string equipmentName)
    {
        if (equipmentName != "Level 2 Camera")
        {
            if ((currentStep == GokeLevelStep.IntroducePickup || currentStep == GokeLevelStep.PickUpCamera) && tutorialManager != null)
            {
                tutorialManager.ShowWarning("The Level 2 Camera is waiting on the delivery table. Pick it up with [E].");
            }
            return;
        }

        hasPickedUpLevel2Camera = true;
        if (currentStep != GokeLevelStep.IntroducePickup && currentStep != GokeLevelStep.PickUpCamera) return;

        ShowSDCardPickupIntroduction();
    }

    public void OnSDCardPickedUp()
    {
        hasPickedUpSDCard = true;
        if (currentStep != GokeLevelStep.IntroduceSDCardPickup && currentStep != GokeLevelStep.PickUpSDCard) return;

        ShowSDCardInsertionIntroduction();
    }

    public void OnCardInsertedToCamera(string equipmentName)
    {
        if (currentStep != GokeLevelStep.InsertSDCard) return;
        if (equipmentName != "Level 2 Camera") return;

        currentStep = GokeLevelStep.IntroduceCameraView;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Let's try the Rule of Thirds. Click <color=red>[Left Click]</color> to open the viewfinder, put the product where two grid lines cross, and leave some breathing room beside it.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    public void OnCameraViewEntered(string equipmentName)
    {
        if (equipmentName == "Level 2 Camera") cameraPracticeViewOpen = true;
        if (currentStep != GokeLevelStep.IntroduceCameraView && currentStep != GokeLevelStep.OpenCameraView) return;
        if (equipmentName != "Level 2 Camera") return;

        currentStep = GokeLevelStep.InspectCameraFeatures;
        isBriefingOpen = false;
        hasCompletedRuleOfThirdsPractice = false;
        ruleOfThirdsPracticeTimer = 0f;
        if (practiceLesson != null) return;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Frame the PRACTICE PRODUCT on a Rule of Thirds intersection",
                "- Use the yellow power points as composition targets",
                "- Move left or right to create intentional negative space",
                "- Use <color=red>[Q/E]</color> for height and <color=red>[Scroll]</color> for shot size",
                "- Hold the correct frame for 2 seconds"
            });
        }
    }

    public void OnRuleOfThirdsPracticeUpdated(bool hasCorrectComposition)
    {
        if (!cameraPracticeViewOpen) return;
        if (currentStep != GokeLevelStep.InspectCameraFeatures || hasCompletedRuleOfThirdsPractice) return;
        if (practiceLesson != null && !practiceLesson.ReadyToPlace) return;

        if (!hasCorrectComposition)
        {
            ruleOfThirdsPracticeTimer = 0f;
            return;
        }

        ruleOfThirdsPracticeTimer += Time.deltaTime;
        if (ruleOfThirdsPracticeTimer < 2f) return;

        hasCompletedRuleOfThirdsPractice = true;
        if (practiceLesson == null) ShowFramingSuccess();
    }

    private void ShowFramingSuccess()
    {
        if (awaitingFramingAcknowledgement) return;
        awaitingFramingAcknowledgement = true;
        isBriefingOpen = true;
        if (tutorialManager != null) tutorialManager.FreezePlayerMovement();
        var ui = TutorialUIManager.Instance;
        if (ui != null)
        {
            ui.HideTasks();
            ui.ShowBossDialogue("That's the frame! The product sits on a thirds intersection, so your eye goes straight to it. That space beside it gives our message room without covering the product.", ui.poseHappy, true, false);
        }
    }

    public void OnCameraViewExited(string equipmentName)
    {
        if (equipmentName == "Level 2 Camera") cameraPracticeViewOpen = false;
        if (awaitingFramingAcknowledgement) return;
        if (currentStep != GokeLevelStep.InspectCameraFeatures) return;
        if (equipmentName != "Level 2 Camera") return;

        if (practiceLesson != null) return;
        if (!hasCompletedRuleOfThirdsPractice)
        {
            currentStep = GokeLevelStep.OpenCameraView;
            isBriefingOpen = false;

            if (tutorialManager != null) tutorialManager.ShowWarning("Line the product up where two grid lines cross, then hold that framing for 2 seconds.");

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[]
                {
                    "- Open the Level 2 Camera viewfinder again",
                    "- Complete the Rule of Thirds framing practice"
                });
            }
            return;
        }

        ReturnPracticeLightsToDeliveryZone();

        currentStep = GokeLevelStep.ExplainCameraFeatures;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("That framing gives our product room to shine, with space beside it for a message. We're done with the demo; your three lights are back at delivery.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void StartCameraPurchase()
    {
        isBriefingOpen = false;
        bool alreadyOwnsCamera = PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1;
        currentStep = alreadyOwnsCamera ? GokeLevelStep.BuySDCard : GokeLevelStep.BuyCamera;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            if (alreadyOwnsCamera)
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy a blank SD Card for your Level 2 Camera" });
            else
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy the Level 2 Camera", "- Add an SD Card before checkout" });
            TutorialUIManager.Instance.SetDynamicGlow("shop", true);
        }

        if (tutorialManager != null) tutorialManager.PointLineAt("shop");
    }

    private void ShowCameraIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceCamera;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            bool alreadyOwnsCamera = PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1;
            string message = alreadyOwnsCamera
                ? "Let's see that lighting through a camera. Your Level 2 Camera is at delivery; pick up a blank <color=yellow>SD Card</color> from the shop for our test."
                : "Let's put that lighting to the test. Open the shop with <color=red>[E]</color> and buy the <color=yellow>Level 2 Camera</color> plus one blank <color=yellow>SD Card</color>.";
            TutorialUIManager.Instance.ShowBossDialogue(message, TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void ShowNextLevelPreparation()
    {
        currentStep = GokeLevelStep.PrepareNextLevel;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Ready for the next job? Level 2 asks a little more of us. We'll try the new gear together before you take on the brief.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowAlmanacIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceAlmanac;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("I've put what you've learned in the <color=yellow>Production Almanac</color>. No need to remember it all. Press <color=red>[P]</color> after we chat and have a look.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartAlmanacIntroduction()
    {
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();

        if (AlmanacManager.Instance == null)
        {
            ShowLightPurchaseIntroduction();
            return;
        }

        currentStep = GokeLevelStep.OpenAlmanac;
        // The lesson explicitly asks for the book, including direct level/cheat entry.
        AlmanacManager.Instance.UnlockTutorialEquipment();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Production Almanac", "- Review the guides you unlocked in Level 1" });
        }
    }

    private void ShowEquipmentAlmanacIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceEquipmentAlmanac;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("How did that camera feel? I've added its controls to the Almanac. Open it with <color=red>[P]</color> and take a look at the Level 2 entry.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void StartEquipmentAlmanacReview()
    {
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();

        if (AlmanacManager.Instance == null)
        {
            ShowContractIntroduction();
            return;
        }

        currentStep = GokeLevelStep.OpenEquipmentAlmanac;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Almanac again", "- Open EQUIPMENT and review the Level 2 Camera" });
        }
    }

    private void OfferContract()
    {
        CleanUpLightingPractice();
        currentStep = GokeLevelStep.OfferContract;
        isBriefingOpen = false;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Review the Goke Cola contract", "- Select ACCEPT CONTRACT to continue" });
        }

        if (contractUIManager != null)
        {
            contractUIManager.ShowGokeContract(AcceptContract);
        }
        else
        {
            isBriefingOpen = true;
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.ShowBossDialogue("Goke Cola has 60,000 B-Coins for a red set, Rule of Thirds, three-point lighting, and two graphics. Ready to take it on? Press <color=red>[SPACE]</color> to accept.", TutorialUIManager.Instance.poseBoss, true, false);
            }
        }
    }

    private void ShowContractIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceContract;
        isBriefingOpen = true;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("We've tried the lights and the camera. Now there's a brief from <color=yellow>Goke Cola</color> with your name on it. Let's take a look.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowLightPurchaseIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceLightPurchase;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Let's try a three-light setup before the job. Open the shop with <color=red>[E]</color>, add three 160 LED Panels, and click <color=red>CONFIRM</color>.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartLightPurchase()
    {
        if (lightItemIndex == -1)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The 160 LED Panel is missing from the Equipment Shop.");
            ShowLightingSetupIntroduction();
            return;
        }

        currentStep = GokeLevelStep.BuyLights;
        isBriefingOpen = false;
        lightsAddedToCart = 0;
        pickedUpPracticeLights.Clear();
        placedPracticeLights.Clear();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Open the Equipment Shop", "- Buy three 160 LED Panels" });
            TutorialUIManager.Instance.SetDynamicGlow("shop", true);
        }

        if (tutorialManager != null) tutorialManager.PointLineAt("shop");
    }

    private void StartLightPickup()
    {
        currentStep = GokeLevelStep.PickUpLights;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up all three lights from the delivery table", "- Lights collected: 0 / 3" });
        }
    }

    public void OnLightPickedUp(FilmLightItem light)
    {
        if (currentStep != GokeLevelStep.PickUpLights || light == null) return;

        if (!pickedUpPracticeLights.Add(light.GetInstanceID())) return;
        if (!practiceLights.Contains(light)) practiceLights.Add(light);

        int pickedUpCount = pickedUpPracticeLights.Count;
        if (pickedUpCount < 3)
        {
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up all three lights from the delivery table", "- Lights collected: " + pickedUpCount + " / 3" });
            }
            return;
        }

        ShowLightingSetupIntroduction();
    }

    public bool CanPickUpLight(FilmLightItem light)
    {
        if ((!IsLightingPlacementStep() && currentStep != GokeLevelStep.ObserveLightingSetup) || light == null) return true;
        if (!placedPracticeLights.Contains(light.GetInstanceID())) return true;

        if (tutorialManager != null)
        {
            string warningMessage = currentStep == GokeLevelStep.ObserveLightingSetup
                ? "Keep the completed lights in place while you observe the setup."
                : "That light is already in its correct practice position. Use one of the unplaced lights!";
            tutorialManager.ShowWarning(warningMessage);
        }
        return false;
    }

    public void OnLightTurnedOn(FilmLightItem light)
    {
        if (!IsLightingPlacementStep() || light == null) return;
        ShowCurrentLightingPracticeTasks();
    }

    public void OnLightIntensityChanged(FilmLightItem light, float intensity)
    {
        if (!IsLightingPlacementStep() || light == null) return;
        ShowCurrentLightingPracticeTasks();
    }

    public void OnLightDropped(FilmLightItem light)
    {
        if (!IsLightingPlacementStep() || light == null) return;

        Transform placementMarker = GetCurrentPlacementMarker();
        int requiredIntensity = GetCurrentRequiredIntensity();
        string lightRole = GetCurrentLightRole();

        if (placementMarker == null) return;
        if (practiceLesson != null && (!practiceLesson.ReadyToPlace || !GuidedPracticeLesson.AtMarker(placementMarker)))
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Pick the light up with [E] and finish the current step on the circle first.");
            return;
        }

        // OnDropped already settles the held model in front of the player.
        // The lesson asks the PLAYER to stand on the circle, not the model's pivot.
        bool isNearMarker = GuidedPracticeLesson.AtMarker(placementMarker);
        bool hasCorrectIntensity = Mathf.Abs(light.intensityPercent - requiredIntensity) <= 2.5f;

        if (!light.IsPoweredOn() || !hasCorrectIntensity || !isNearMarker)
        {
            if (tutorialManager != null)
            {
                string correction = !light.IsPoweredOn()
                    ? "Turn the light ON with Left Mouse Button."
                    : !hasCorrectIntensity
                        ? "Set the " + lightRole + " to " + requiredIntensity + "% with the mouse wheel."
                        : "Stand on the " + lightRole + " marker before pressing G.";
                tutorialManager.ShowWarning("Let's make a small adjustment. Pick the light up again. " + correction);
            }
            return;
        }

        practiceLesson?.Release();
        practiceLesson = null;
        light.transform.position = placementMarker.position + Vector3.up * 0.05f;
        ConfigureProfessionalPracticeLight(light);

        Rigidbody[] lightBodies = light.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody lightBody in lightBodies)
        {
            if (lightBody == null) continue;
            lightBody.velocity = Vector3.zero;
            lightBody.angularVelocity = Vector3.zero;
            lightBody.useGravity = false;
            lightBody.isKinematic = true;
        }

        placementMarker.gameObject.SetActive(false);
        placedPracticeLights.Add(light.GetInstanceID());

        if (currentStep == GokeLevelStep.PlaceKeyLight)
        {
            ShowLightingPlacementTutorial();
        }
        else if (currentStep == GokeLevelStep.PlaceFillLight)
        {
            ShowLightingSettingsTutorial();
        }
        else
        {
            StartCoroutine(ObserveLightingSetup());
        }
    }

    private void ConfigureProfessionalPracticeLight(FilmLightItem light)
    {
        if (light == null || light.spotlight == null || lightingPracticeTarget == null) return;

        Light practiceSpotlight = light.spotlight;
        practiceSpotlight.range = Mathf.Max(30f, light.standardRange);
        practiceSpotlight.shadows = LightShadows.Soft;
        practiceSpotlight.shadowBias = 0.08f;
        practiceSpotlight.shadowNormalBias = 0.25f;
        practiceSpotlight.shadowNearPlane = 0.2f;

        float targetHeight = 1.05f;

        if (currentStep == GokeLevelStep.PlaceKeyLight)
        {
            practiceSpotlight.spotAngle = 40f;
            practiceSpotlight.innerSpotAngle = 8f;
            practiceSpotlight.shadowStrength = 0.7f;
        }
        else if (currentStep == GokeLevelStep.PlaceFillLight)
        {
            practiceSpotlight.spotAngle = 50f;
            practiceSpotlight.innerSpotAngle = 10f;
            practiceSpotlight.shadowStrength = 0.25f;
        }
        else
        {
            practiceSpotlight.spotAngle = 34f;
            practiceSpotlight.innerSpotAngle = 6f;
            practiceSpotlight.shadowStrength = 0.55f;
            targetHeight = 1.2f;
        }

        light.AimAt(lightingPracticeTarget.position + Vector3.up * targetHeight);
    }

    private void ShowLightingSetupIntroduction()
    {
        CreateLightingPractice();
        currentStep = GokeLevelStep.IntroduceLightingSetup;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Our Key does the main work. We'll set it up on the yellow circle first. Keep the other two lights for later.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartKeyLightPractice()
    {
        currentStep = GokeLevelStep.PlaceKeyLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        BeginGuidedLightPractice();
    }

    private void ShowLightingPlacementTutorial()
    {
        currentStep = GokeLevelStep.ExplainLightingPlacement;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("See the shadow the Key makes? Let's soften it with our second light. The blue circle marks its position.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void StartFillLightPractice()
    {
        currentStep = GokeLevelStep.PlaceFillLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        BeginGuidedLightPractice();
    }

    private void ShowLightingSettingsTutorial()
    {
        currentStep = GokeLevelStep.ExplainLightingSettings;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Now let's separate the product from the background. We'll use the last light at the magenta circle.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void StartBackLightPractice()
    {
        currentStep = GokeLevelStep.PlaceBackLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        BeginGuidedLightPractice();
    }

    private IEnumerator ObserveLightingSetup()
    {
        currentStep = GokeLevelStep.ObserveLightingSetup;
        isBriefingOpen = false;

        for (int secondsRemaining = 10; secondsRemaining > 0; secondsRemaining--)
        {
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[]
                {
                    "- Observe how the 75% Key creates the main shape",
                    "- Compare the softer 40% Fill and the 60% Back separation",
                    "- Next briefing in " + secondsRemaining + " seconds"
                });
            }

            yield return new WaitForSeconds(1f);
        }

        ShowLightingPracticeComplete();
    }

    private void ShowLightingPracticeComplete()
    {
        currentStep = GokeLevelStep.LightingPracticeComplete;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("There's our three-point setup. Key gives shape, Fill softens the shadows, and Back catches the edge. Leave them there; we'll use this set for the camera test.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ReturnPracticeLightsToDeliveryZone()
    {
        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();
        if (shopTerminal == null || shopTerminal.deliveryZone == null)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The practice lights could not be returned because the delivery zone is missing.");
            return;
        }

        Transform deliveryZone = shopTerminal.deliveryZone;

        for (int i = 0; i < practiceLights.Count; i++)
        {
            FilmLightItem light = practiceLights[i];
            if (light == null) continue;

            if (light.IsPoweredOn()) light.OnUse(Camera.main);
            light.OnDropped(Camera.main);

            Vector3 deliveryOffset = deliveryZone.right * ((i - 1) * 0.6f) + deliveryZone.forward * 0.25f + Vector3.up * 0.65f;
            light.transform.position = deliveryZone.position + deliveryOffset;
            light.transform.rotation = deliveryZone.rotation;

            Rigidbody[] lightBodies = light.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody lightBody in lightBodies)
            {
                if (lightBody == null) continue;
                lightBody.velocity = Vector3.zero;
                lightBody.angularVelocity = Vector3.zero;
            }
        }

        practiceLights.Clear();
        pickedUpPracticeLights.Clear();
        placedPracticeLights.Clear();
    }

    private bool IsLightingPlacementStep()
    {
        return currentStep == GokeLevelStep.PlaceKeyLight ||
               currentStep == GokeLevelStep.PlaceFillLight ||
               currentStep == GokeLevelStep.PlaceBackLight;
    }

    private Transform GetCurrentPlacementMarker()
    {
        if (currentStep == GokeLevelStep.PlaceKeyLight) return keyPlacementMarker;
        if (currentStep == GokeLevelStep.PlaceFillLight) return fillPlacementMarker;
        return backPlacementMarker;
    }

    private int GetCurrentRequiredIntensity()
    {
        if (currentStep == GokeLevelStep.PlaceKeyLight) return 75;
        if (currentStep == GokeLevelStep.PlaceFillLight) return 40;
        return 60;
    }

    private string GetCurrentLightRole()
    {
        if (currentStep == GokeLevelStep.PlaceKeyLight) return "Key Light";
        if (currentStep == GokeLevelStep.PlaceFillLight) return "Fill Light";
        return "Back Light";
    }

    private void BeginGuidedLightPractice()
    {
        Transform marker = GetCurrentPlacementMarker();
        if (keyPlacementMarker != null) keyPlacementMarker.gameObject.SetActive(marker == keyPlacementMarker);
        if (fillPlacementMarker != null) fillPlacementMarker.gameObject.SetActive(marker == fillPlacementMarker);
        if (backPlacementMarker != null) backPlacementMarker.gameObject.SetActive(marker == backPlacementMarker);
        practiceLesson = GuidedPracticeLesson.Light(tutorialManager, marker,
            () => practiceLights.Find(light => light != null && light.gameObject.activeInHierarchy && !placedPracticeLights.Contains(light.GetInstanceID()) &&
                light.GetComponentInParent<Player.PlayerController.PlayerController>() != null),
            GetCurrentLightRole(), GetCurrentRequiredIntensity(), false);
    }

    private void ShowCurrentLightingPracticeTasks()
    {
        if (practiceLesson != null || TutorialUIManager.Instance == null) return;

        string role = GetCurrentLightRole();
        int requiredIntensity = GetCurrentRequiredIntensity();
        string markerColor = "GREEN";

        TutorialUIManager.Instance.SetupTasks(new string[]
        {
            "- Equip a light and click Left Mouse Button to turn it ON",
            "- Use the mouse wheel to set the " + role + " to " + requiredIntensity + "%",
            "- Stand on the " + markerColor + " " + role + " marker",
            "- Press <color=red>[G]</color> to place the light"
        });
    }

    private void AcceptContract()
    {
        if (CareerManager.Instance != null)
        {
            if (PlayerPrefs.GetInt("GokeContractAccepted", 0) == 0)
            {
                CareerManager.Instance.AcceptJob("Goke Cola", 60000);
                PlayerPrefs.SetInt("GokeContractAccepted", 1);
                PlayerPrefs.Save();
            }
            else
            {
                CareerManager.Instance.currentActiveJob = "Goke Cola";
            }
        }

        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockProductionTechniques();
        if (contractUIManager != null) contractUIManager.UnlockQualifications();

        currentStep = GokeLevelStep.IntroduceTechniques;
        if (DevTutorialBypass.Disabled) { StartContract(); return; }
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("The Goke job is yours. If you need a hand, press <color=red>[P]</color>. The lighting, composition, and color guides are there whenever you need them.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartTechniquesAlmanacReview()
    {
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();

        if (AlmanacManager.Instance == null)
        {
            StartQualificationsIntroduction();
            return;
        }

        currentStep = GokeLevelStep.OpenTechniquesAlmanac;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Almanac", "- Open TECHNIQUES and review the new Level 2 guides" });
        }
    }

    private void StartQualificationsIntroduction()
    {
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();

        if (contractUIManager == null)
        {
            StartProductionTutorial();
            return;
        }

        currentStep = GokeLevelStep.OpenQualifications;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[TAB]</color> to open the contract qualifications" });
        }
    }

    private void StartCameraPickup()
    {
        if (hasPickedUpLevel2Camera)
        {
            ShowSDCardPickupIntroduction();
            return;
        }

        isBriefingOpen = false;
        currentStep = GokeLevelStep.PickUpCamera;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the Level 2 Camera from the delivery table" });
        }
    }

    private void StartSDCardPickup()
    {
        if (hasPickedUpSDCard)
        {
            ShowSDCardInsertionIntroduction();
            return;
        }

        isBriefingOpen = false;
        currentStep = GokeLevelStep.PickUpSDCard;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the blank SD Card from the delivery table" });
        }
    }

    private void ShowSDCardPickupIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceSDCardPickup;
        isBriefingOpen = true;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Camera in hand. Grab the SD Card from the delivery table with <color=red>[E]</color>, and we'll load it up.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void ShowSDCardInsertionIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceSDInsert;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Select your Level 2 Camera and press <color=red>[C]</color> to insert the blank SD Card. We need that in before we can use the viewfinder or record.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void StartSDCardInsertion()
    {
        isBriefingOpen = false;
        currentStep = GokeLevelStep.InsertSDCard;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Equip the Level 2 Camera", "- Press <color=red>[C]</color> to insert the SD Card" });
        }
    }

    private void StartCameraFeatureInspection()
    {
        isBriefingOpen = false;
        currentStep = GokeLevelStep.OpenCameraView;

        var steps = new List<GuidedPracticeLesson.Step>();
        if (lightingPracticeTarget != null && keyPlacementMarker != null && fillPlacementMarker != null)
        {
            Vector3 position = (keyPlacementMarker.position + fillPlacementMarker.position) * 0.5f;
            cameraPracticeMarker = CreatePlacementMarker("Camera Practice Circle", position,
                Color.green, "CAMERA");
            steps.Add(new GuidedPracticeLesson.Step(
                "Bring the camera to the green CAMERA circle. The lights can stay where they are while we try our framing.",
                "Equip the Level 2 Camera and stand on the CAMERA circle",
                () => GuidedPracticeLesson.AtMarker(cameraPracticeMarker)));
        }
        steps.Add(new GuidedPracticeLesson.Step(
            "Click <color=red>[Left Click]</color> to open the viewfinder. Its grid divides the picture into thirds.",
            "[Left Click] Open the Level 2 Camera viewfinder",
            () => cameraPracticeViewOpen));
        steps.Add(new GuidedPracticeLesson.Step(
            "Place the product where two grid lines cross. Use <color=red>[Scroll]</color> for size and <color=red>[Q/E]</color> for height. Leave room beside it for a message.",
            "Frame the product at a grid intersection and hold steady for 2 seconds",
            () => hasCompletedRuleOfThirdsPractice));
        practiceLesson = new GuidedPracticeLesson(tutorialManager, steps, () =>
        {
            practiceLesson = null;
            if (cameraPracticeMarker != null) cameraPracticeMarker.gameObject.SetActive(false);
            ShowFramingSuccess();
        }, cameraPracticeMarker);
    }

    private void StartProductionTutorial()
    {
        currentStep = GokeLevelStep.ExplainStage;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Let's build Goke's set. Open the Director Tablet with <color=red>[E]</color>, add a red wall, and place the can with some space between it and the backdrop.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void ShowCompositionTutorial()
    {
        currentStep = GokeLevelStep.ExplainComposition;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Give the whole can a spot on a Rule of Thirds intersection. Leave room beside it for graphics; <color=red>[TAB]</color> brings the brief back if you need it.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void ShowLightingTutorial()
    {
        currentStep = GokeLevelStep.ExplainLighting;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Use the three roles we practiced: Key shapes the can, a softer Fill controls shadows from the opposite side, and Back separates it from the set. Choose your own intensities; 75/40/60 was a practice example. Aim all three at the can and power them on.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void ShowPostProductionTutorial()
    {
        currentStep = GokeLevelStep.ExplainPostProduction;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Roll a 10-second take with <color=red>[R]</color>, then take the card to the computer. We'll give Graphic 1 the first five seconds and Graphic 2 the last five.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowContractBriefing()
    {
        currentStep = GokeLevelStep.ContractBriefing;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("You've got the plan. Keep the brief handy with <color=red>[TAB]</color>, and take your time. Press <color=red>[SPACE]</color> when you're ready to get to work.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void StartContract()
    {
        isBriefingOpen = false;
        currentStep = GokeLevelStep.LevelActive;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.HideTasks();
        }
    }

    private void SetupLevel2Camera()
    {
        GameObject level2CameraPrefab = Resources.Load<GameObject>("Prefabs/Level 2 Camera Placeholder");
        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();

        if (shopTerminal != null && level2CameraPrefab != null)
        {
            if (PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1)
            {
                shopTerminal.RestoreLevel2Camera(level2CameraPrefab);
                level2CameraItemIndex = shopTerminal.availableItems.FindIndex(item => item.itemName == "LEVEL 2 CAMERA");
            }
            else
            {
                level2CameraItemIndex = shopTerminal.SetupLevel2Camera(level2CameraPrefab);
            }

            sdCardItemIndex = shopTerminal.availableItems.FindIndex(item => item.itemName.Contains("SD"));
            lightItemIndex = shopTerminal.availableItems.FindIndex(item => item.itemName == "160 LED PANEL");

            if (sdCardItemIndex == -1) Debug.LogWarning("SD Card could not be found in the Equipment Shop.");
            if (lightItemIndex == -1) Debug.LogWarning("160 LED Panel could not be found in the Equipment Shop.");
        }
        else
        {
            Debug.LogWarning("Level 2 Camera could not be added to the Equipment Shop.");
        }
    }

    private void CreateLightingPractice()
    {
        if (lightingPracticeRoot != null) return;

        Renderer stageRenderer = FindStageRenderer();
        if (stageRenderer == null)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The raised Stage could not be found for the lighting practice.");
            return;
        }

        Bounds stageBounds = stageRenderer.bounds;
        Vector3 stageCenter = stageBounds.center;
        stageCenter.y = stageBounds.max.y + 0.03f;

        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        Vector3 stageFront = player != null ? player.transform.position - stageCenter : Vector3.back;
        stageFront.y = 0f;

        if (stageFront.sqrMagnitude < 0.01f) stageFront = Vector3.back;

        if (Mathf.Abs(stageFront.x) > Mathf.Abs(stageFront.z))
            stageFront = new Vector3(Mathf.Sign(stageFront.x), 0f, 0f);
        else
            stageFront = new Vector3(0f, 0f, Mathf.Sign(stageFront.z));

        Vector3 stageRight = Vector3.Cross(Vector3.up, stageFront).normalized;
        float stageFrontExtent = Mathf.Abs(stageFront.x) * stageBounds.extents.x + Mathf.Abs(stageFront.z) * stageBounds.extents.z;
        float stageSideExtent = Mathf.Abs(stageRight.x) * stageBounds.extents.x + Mathf.Abs(stageRight.z) * stageBounds.extents.z;
        float frontLightDistance = Mathf.Min(4.6f, stageFrontExtent * 0.72f, stageSideExtent * 0.45f);
        float sideLightDistance = frontLightDistance;
        float backLightDistance = Mathf.Min(2.4f, stageFrontExtent * 0.38f);
        float backLightSideDistance = Mathf.Min(4.2f, stageSideExtent * 0.4f);

        Vector3 targetPosition = stageCenter - stageFront * Mathf.Min(1f, stageFrontExtent * 0.15f);
        Vector3 keyPosition = targetPosition + stageFront * frontLightDistance - stageRight * sideLightDistance;
        Vector3 fillPosition = targetPosition + stageFront * frontLightDistance + stageRight * sideLightDistance;
        Vector3 backPosition = targetPosition - stageFront * backLightDistance + stageRight * backLightSideDistance;

        targetPosition = ClampPracticePointToStage(targetPosition, stageBounds);
        keyPosition = ClampPracticePointToStage(keyPosition, stageBounds);
        fillPosition = ClampPracticePointToStage(fillPosition, stageBounds);
        backPosition = ClampPracticePointToStage(backPosition, stageBounds);

        lightingPracticeRoot = new GameObject("Goke Lighting Practice");
        lightingPracticeDirector = FindObjectOfType<DirectorTerminal>();
        if (lightingPracticeDirector != null)
        {
            lightingPracticeWall = lightingPracticeDirector.CreatePracticeWall(new Color(150f / 255f, 0f, 0f, 1f));
        }
        else if (tutorialManager != null)
        {
            tutorialManager.ShowWarning("The Director Terminal could not create the practice wall.");
        }

        lightingPracticeTarget = CreatePracticeTarget(targetPosition);
        keyPlacementMarker = CreatePlacementMarker("Key Light Marker", keyPosition, Color.green, "KEY LIGHT\n75%");
        fillPlacementMarker = CreatePlacementMarker("Fill Light Marker", fillPosition, Color.green, "FILL LIGHT\n40%");
        backPlacementMarker = CreatePlacementMarker("Back Light Marker", backPosition, Color.green, "BACK LIGHT\n60%");
    }

    private Renderer FindStageRenderer()
    {
        GameObject stageRoot = GameObject.Find("Stage");
        if (stageRoot == null) return null;

        Renderer[] stageRenderers = stageRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer stageRenderer in stageRenderers)
        {
            if (stageRenderer != null && stageRenderer.gameObject.name == "stage") return stageRenderer;
        }

        return stageRenderers.Length > 0 ? stageRenderers[0] : null;
    }

    private Vector3 ClampPracticePointToStage(Vector3 point, Bounds stageBounds)
    {
        float edgePadding = 0.75f;
        point.x = Mathf.Clamp(point.x, stageBounds.min.x + edgePadding, stageBounds.max.x - edgePadding);
        point.y = stageBounds.max.y + 0.03f;
        point.z = Mathf.Clamp(point.z, stageBounds.min.z + edgePadding, stageBounds.max.z - edgePadding);
        return point;
    }

    private Transform CreatePracticeTarget(Vector3 targetPosition)
    {
        GameObject targetRoot = new GameObject("Practice Product Target");
        targetRoot.transform.SetParent(lightingPracticeRoot.transform);
        targetRoot.transform.position = targetPosition;

        GameObject targetBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        targetBody.name = "Practice Product";
        targetBody.transform.SetParent(targetRoot.transform);
        targetBody.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        targetBody.transform.localScale = new Vector3(0.45f, 0.65f, 0.45f);
        targetBody.AddComponent<RecordableSubject>();

        Collider targetCollider = targetBody.GetComponent<Collider>();
        if (targetCollider != null) targetCollider.isTrigger = true;

        Renderer targetRenderer = targetBody.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targetRenderer.material.color = new Color(0.8f, 0.05f, 0.05f, 1f);
            targetRenderer.material.EnableKeyword("_EMISSION");
            targetRenderer.material.SetColor("_EmissionColor", new Color(0.25f, 0f, 0f, 1f));
            if (targetRenderer.material.HasProperty("_Metallic")) targetRenderer.material.SetFloat("_Metallic", 0.25f);
            if (targetRenderer.material.HasProperty("_Smoothness")) targetRenderer.material.SetFloat("_Smoothness", 0.55f);
        }

        CreatePracticeLabel(targetRoot.transform, new Vector3(0f, 1.65f, 0f), "PRACTICE\nPRODUCT", Color.white);
        return targetRoot.transform;
    }

    private Transform CreatePlacementMarker(string markerName, Vector3 markerPosition, Color markerColor, string markerText)
    {
        GameObject markerRoot = new GameObject(markerName);
        markerRoot.transform.SetParent(lightingPracticeRoot.transform);
        markerRoot.transform.position = markerPosition;

        GameObject markerDisc = GuidedPracticeLesson.CreateGreenMarker(markerRoot.transform);
        markerDisc.name = "Placement Point";
        markerDisc.transform.SetParent(markerRoot.transform);
        markerDisc.transform.localPosition = Vector3.zero;
        // Marker size and floor clearance match the first tutorial.

        Collider markerCollider = markerDisc.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);

        Renderer markerRenderer = markerDisc.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material.color = markerColor;
            markerRenderer.material.EnableKeyword("_EMISSION");
            markerRenderer.material.SetColor("_EmissionColor", markerColor * 0.65f);
        }

        CreatePracticeLabel(markerRoot.transform, new Vector3(0f, 0.35f, 0f), markerText, markerColor);
        return markerRoot.transform;
    }

    private void CreatePracticeLabel(Transform labelParent, Vector3 localPosition, string labelText, Color labelColor)
    {
        GameObject labelObject = new GameObject("Practice Label");
        labelObject.transform.SetParent(labelParent);
        labelObject.transform.localPosition = localPosition;

        TextMeshPro markerLabel = labelObject.AddComponent<TextMeshPro>();
        markerLabel.text = labelText;
        markerLabel.fontSize = 3f;
        markerLabel.alignment = TextAlignmentOptions.Center;
        markerLabel.color = labelColor;
        markerLabel.rectTransform.sizeDelta = new Vector2(5f, 1.4f);
        practiceMarkerLabels.Add(markerLabel);
    }

    private void CleanUpLightingPractice()
    {
        if (lightingPracticeRoot != null) Destroy(lightingPracticeRoot);
        if (lightingPracticeDirector != null && lightingPracticeWall != null) lightingPracticeDirector.RemovePracticeWall(lightingPracticeWall);

        lightingPracticeRoot = null;
        lightingPracticeWall = null;
        lightingPracticeDirector = null;
        lightingPracticeTarget = null;
        keyPlacementMarker = null;
        fillPlacementMarker = null;
        backPlacementMarker = null;
        practiceLights.Clear();
        practiceMarkerLabels.Clear();
    }

    private void SetupContractUI()
    {
        contractUIManager = FindObjectOfType<ContractUIManager>();
        if (contractUIManager == null) contractUIManager = gameObject.AddComponent<ContractUIManager>();
        if (contractUIManager != null) contractUIManager.PrepareGokeContract();
    }

    private IEnumerator UnlockPlayerAfterSpace()
    {
        yield return new WaitUntil(() => Keyboard.current == null || !Keyboard.current.spaceKey.isPressed);
        yield return null;

        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null)
        {
            p.canLook = true;
            p.canMove = true;
        }
    }

    private void CleanUpStudio()
    {
        DirectorTerminal stageManager = FindObjectOfType<DirectorTerminal>();
        if (stageManager != null) stageManager.ClearStage();

        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.name.Contains("Cube") || obj.name.Contains("Flower") || obj.name.Contains("Floral") || obj.name.Contains("Cola"))
            {
                Destroy(obj);
            }
        }
    }
}
