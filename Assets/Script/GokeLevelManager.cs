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
    public float ThirdsFramingProgress => Mathf.Clamp01(ruleOfThirdsPracticeTimer / 2f);
    public bool ThirdsPracticeActive => currentStep == GokeLevelStep.InspectCameraFeatures && !hasCompletedRuleOfThirdsPractice;
    private int thirdsPracticeStage;
    public int ThirdsPracticeIntersection => thirdsPracticeStage == 0 ? 0 : 2;
    public bool ThirdsIndependentPractice => thirdsPracticeStage >= 2 && !hasCompletedRuleOfThirdsPractice;

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
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.TryAdvanceBossDialoguePage()) return;
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
            if (!cameraPracticeViewOpen) OnCameraViewExited("NONY FX Camera");
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

            if (lightsAddedToCart >= lightsRequiredToBuy)
            {
                currentStep = GokeLevelStep.LightCheckout;

                if (TutorialUIManager.Instance != null)
                {
                    TutorialUIManager.Instance.SetupTasks(new string[] { "- Confirm the missing 160 LED Panels" });
                }
            }
            else if (TutorialUIManager.Instance != null)
            {
                int remainingLights = lightsRequiredToBuy - lightsAddedToCart;
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add " + remainingLights + " more 160 LED Panel" + (remainingLights == 1 ? "" : "s") + " to your cart" });
            }

            return true;
        }

        if (currentStep == GokeLevelStep.LightCheckout)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The missing lights are in the cart. Click CONFIRM and we'll collect them.");
            return false;
        }

        if (currentStep == GokeLevelStep.BuyCamera)
        {
            if (itemIndex != level2CameraItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Let's get the NONY FX Camera into the cart first.");
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
                bool alreadyOwnsCamera = true;
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
            if (tutorialManager != null) tutorialManager.ShowWarning("We're missing the NONY FX Camera. Add it before confirming.");
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
                bool alreadyOwnsCamera = true;
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

        if (!CameraFeatureUnlocks.IsCamera(equipmentName))
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Select the NONY FX Camera and press [C] to load the SD Card first.");
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Open LIGHTS and add " + lightsRequiredToBuy + " missing 160 LED Panels to your cart" });
            }
            return;
        }

        if (currentStep != GokeLevelStep.BuyCamera && currentStep != GokeLevelStep.BuySDCard) return;

        if (TutorialUIManager.Instance != null)
        {
            if (currentStep == GokeLevelStep.BuyCamera)
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add the NONY FX Camera to your cart" });
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
                TutorialUIManager.Instance.ShowBossDialogue("Your new panels and the lights you already own are at delivery. Pick each one up with <color=red>[E]</color>, then use <color=red>[1-5]</color> to choose which one you're holding.", TutorialUIManager.Instance.posePoint, true, false);
            }
            return;
        }

        if (currentStep != GokeLevelStep.CloseShop) return;

        currentStep = GokeLevelStep.IntroducePickup;
        isBriefingOpen = true;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("There's our camera gear on the delivery table. Pick up the <color=yellow>NONY FX Camera</color> with <color=red>[E]</color> first; we'll get the card next.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    public void OnAlmanacOpened()
    {
        if (currentStep == GokeLevelStep.OpenAlmanac)
        {
            currentStep = GokeLevelStep.CloseAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Review your Level 1 guides. Press <color=red>[P]</color> when finished." });
            }
            return;
        }

        if (currentStep == GokeLevelStep.OpenEquipmentAlmanac)
        {
            currentStep = GokeLevelStep.CloseEquipmentAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Review EQUIPMENT > NONY FX Camera. Press <color=red>[P]</color> when finished." });
            }
            return;
        }

        if (currentStep == GokeLevelStep.OpenTechniquesAlmanac)
        {
            currentStep = GokeLevelStep.CloseTechniquesAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Review the new TECHNIQUES guides. Press <color=red>[P]</color> when finished." });
            }
        }

    }

    public void OnAlmanacClosed()
    {
        if (currentStep == GokeLevelStep.CloseAlmanac)
        {
            BeginCompositionPractice();
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
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Review Rule of Thirds", "- Review space for branding", "- Press <color=red>[TAB]</color> when finished" });
        }
    }

    public void OnContractQualificationsClosed()
    {
        if (currentStep != GokeLevelStep.CloseQualifications) return;

        StartProductionTutorial();
    }

    public void OnCameraPickedUp(string equipmentName)
    {
        if (!CameraFeatureUnlocks.IsCamera(equipmentName))
        {
            if ((currentStep == GokeLevelStep.IntroducePickup || currentStep == GokeLevelStep.PickUpCamera) && tutorialManager != null)
            {
                tutorialManager.ShowWarning("The NONY FX Camera is waiting on the delivery table. Pick it up with [E].");
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
        if (!CameraFeatureUnlocks.IsCamera(equipmentName)) return;

        currentStep = GokeLevelStep.IntroduceCameraView;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Let's try the Rule of Thirds. Click <color=red>[Left Click]</color> to open the viewfinder, open F2 settings and turn Grid ON, then put the product where two grid lines cross, and leave some breathing room beside it.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    public void OnCameraViewEntered(string equipmentName)
    {
        if (CameraFeatureUnlocks.IsCamera(equipmentName)) cameraPracticeViewOpen = true;
        if (currentStep != GokeLevelStep.IntroduceCameraView && currentStep != GokeLevelStep.OpenCameraView) return;
        if (!CameraFeatureUnlocks.IsCamera(equipmentName)) return;

        currentStep = GokeLevelStep.InspectCameraFeatures;
        isBriefingOpen = false;
        hasCompletedRuleOfThirdsPractice = false;
        ruleOfThirdsPracticeTimer = 0f;
        thirdsPracticeStage = 0;
        if (practiceLesson != null) return;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Frame the PRACTICE PRODUCT on a Rule of Thirds intersection",
                "- Press F2 and set Grid to ON before framing",
                "- Move left or right to create intentional negative space",
                "- Use <color=red>[Q/E]</color> for height and <color=red>[Scroll]</color> for shot size",
                "- Hold the correct frame for 2 seconds"
            });
        }
    }

    public void OnRuleOfThirdsPracticeUpdated(bool hasCorrectComposition)
    {
        if (PauseManager.isPaused || !Application.isFocused) return;
        if (!cameraPracticeViewOpen) return;
        if (currentStep != GokeLevelStep.InspectCameraFeatures || hasCompletedRuleOfThirdsPractice) return;
        if (practiceLesson != null && (practiceLesson.IsExplaining || practiceLesson.CurrentPermission != "camera.frame")) return;

        if (!hasCorrectComposition)
        {
            ruleOfThirdsPracticeTimer = Mathf.Max(0f, ruleOfThirdsPracticeTimer - Time.deltaTime * .5f);
            return;
        }

        ruleOfThirdsPracticeTimer += Time.deltaTime;
        if (!DevTutorialBypass.PracticeDelayComplete(ruleOfThirdsPracticeTimer, 2f)) return;

        ruleOfThirdsPracticeTimer = 0f;
        thirdsPracticeStage++;
        if (thirdsPracticeStage < 3) return;
        hasCompletedRuleOfThirdsPractice = true;
        if (practiceLesson == null) ShowFramingSuccess();
    }

    public bool ShowThirdsLessonGuide => currentStep == GokeLevelStep.InspectCameraFeatures && !hasCompletedRuleOfThirdsPractice && !ThirdsIndependentPractice;

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
            ui.ShowBossDialogue("Good framing! Your subject is clear, with room beside it for a message. Use that space when you add graphics.", ui.poseHappy, true, false);
        }
    }

    public void OnCameraViewExited(string equipmentName)
    {
        if (CameraFeatureUnlocks.IsCamera(equipmentName)) cameraPracticeViewOpen = false;
        if (awaitingFramingAcknowledgement) return;
        if (currentStep != GokeLevelStep.InspectCameraFeatures) return;
        if (!CameraFeatureUnlocks.IsCamera(equipmentName)) return;

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
                    "- Open the NONY FX Camera viewfinder again",
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
            TutorialUIManager.Instance.ShowBossDialogue("That framing gives our product room to shine, with space beside it for a message. We're done with the ready-lit demo. Use the space beside your subject for the advert's message.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    public static void EnsureEquipmentAdvance()
    {
        if (PlayerPrefs.GetInt("GokeContractAccepted", 0) != 0 || PlayerPrefs.GetInt("GokeEquipmentLoanIssued", 0) != 0) return;
        // Preserve money already paid by the previous advance implementation as outstanding debt.
        if (PlayerPrefs.GetInt("GokeEquipmentAdvancePaid", 0) > 0)
        { PlayerPrefs.SetInt("GokeEquipmentLoanIssued", 1); PlayerPrefs.Save(); return; }
        int required = ProductionEconomy.SDCard + Mathf.Max(0, 3 - ShopTerminal.OwnedPanelLights) * ProductionEconomy.PanelLight;
        int amount = Mathf.Max(0, required - PlayerPrefs.GetInt("PlayerMoney", 0));
        PlayerPrefs.SetInt("GokeEquipmentLoanIssued", 1);
        PlayerPrefs.SetInt("GokeEquipmentAdvancePaid", amount);
        if (CareerManager.Instance != null) CareerManager.Instance.AddMoney(amount, "Equipment support");
        else
        {
            PlayerAnalytics.TransactionMade(amount, "Equipment support", "Equipment support");
            PlayerPrefs.SetInt("PlayerMoney", PlayerPrefs.GetInt("PlayerMoney", 0) + amount);
        }
        PlayerPrefs.Save();
        if (amount > 0) GameSaveManager.Instance?.SaveBudgetCheckpoint();
        if (amount > 0) GameFeedback.Show("BOSS EQUIPMENT LOAN\n+" + amount.ToString("N0") + " B-Coins | Deducted when you accept Goke");
    }

    private int lightsRequiredToBuy = 3;

    private void StartCameraPurchase()
    {
        EnsureEquipmentAdvance();
        isBriefingOpen = false;
        bool alreadyOwnsCamera = true;
        currentStep = alreadyOwnsCamera ? GokeLevelStep.BuySDCard : GokeLevelStep.BuyCamera;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            if (alreadyOwnsCamera)
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy a blank SD Card for your NONY FX Camera" });
            else
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy the NONY FX Camera" });
            TutorialUIManager.Instance.SetDynamicGlow("shop", true);
        }

        if (tutorialManager != null) tutorialManager.PointLineAt("shop");
    }

    private void ShowCameraIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceCamera;
        isBriefingOpen = true;
        if (TutorialUIManager.Instance != null)
            TutorialUIManager.Instance.ShowBossDialogue("Your camera now has a thirds grid. Keep using it! Buy one blank SD Card for practice. In the viewfinder, F2 opens settings. The camera focuses automatically so you can concentrate on framing.", TutorialUIManager.Instance.posePointUp, true, false);
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
            BeginCompositionPractice();
            return;
        }

        currentStep = GokeLevelStep.OpenAlmanac;
        AlmanacManager.Instance.RequestNavigationLesson();
        // The lesson explicitly asks for the book, including direct level/cheat entry.
        AlmanacManager.Instance.UnlockTutorialEquipment();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Production Almanac" });
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
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Almanac" });
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
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Review the Goke Cola contract", "- Select the contract, read the brief, then close it to continue" });
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
                TutorialUIManager.Instance.ShowBossDialogue("Goke Cola has 10,500 B-Coins for a red set, Rule of Thirds, readable lighting, and two graphics. Ready to take it on? Press <color=red>[SPACE]</color> to accept.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Your equipment from the last contract is waiting at delivery. We need three lights total, so buy only the missing panels. If you are short, I will lend you the equipment money and deduct the loan from your advance when you accept Goke.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void BeginCompositionPractice()
    {
        CreateLightingPractice();
        if (lightingPracticeTarget != null)
            foreach (var marker in new[] { keyPlacementMarker, fillPlacementMarker, backPlacementMarker })
            {
                if (marker == null) continue;
                marker.gameObject.SetActive(false);
                var lampObject = new GameObject("Composition practice light", typeof(Light));
                lampObject.transform.SetParent(lightingPracticeRoot.transform, false);
                lampObject.transform.position = marker.position + Vector3.up * 2f;
                lampObject.transform.LookAt(lightingPracticeTarget.position + Vector3.up);
                var lamp = lampObject.GetComponent<Light>();
                lamp.type = LightType.Spot;
                lamp.range = 25f;
                lamp.spotAngle = 65f;
                lamp.intensity = marker == fillPlacementMarker ? 1.5f : 3f;
                lamp.shadows = LightShadows.Soft;
            }
        ShowCameraIntroduction();
    }

    private void StartLightPurchase()
    {
        if (lightItemIndex == -1)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The 160 LED Panel is missing from the Equipment Shop.");
            ShowLightingSetupIntroduction();
            return;
        }

        FindObjectOfType<ShopTerminal>()?.RestoreOwnedEquipment();
        lightsRequiredToBuy = Mathf.Max(0, 3 - ShopTerminal.OwnedPanelLights);
        if (lightsRequiredToBuy == 0) { StartLightPickup(); return; }
        EnsureEquipmentAdvance();
        currentStep = GokeLevelStep.BuyLights;
        isBriefingOpen = false;
        lightsAddedToCart = 0;
        pickedUpPracticeLights.Clear();
        placedPracticeLights.Clear();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Open the Equipment Shop" });
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
        // Keep the beam width, shadows and output the player saw while aiming.

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
        int roleIndex = currentStep == GokeLevelStep.PlaceKeyLight ? 0 : currentStep == GokeLevelStep.PlaceFillLight ? 1 : 2;
        observationLights[roleIndex] = light.spotlight;

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
        }

        // Preserve the height and aim the player previewed before pressing G.
    }

    private void ShowLightingSetupIntroduction()
    {
        CreateLightingPractice();
        currentStep = GokeLevelStep.IntroduceLightingSetup;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Our Key does the main work, in front and to one side of the subject. This larger setup leaves more working distance. Walk to the marked Key circle; we will set power, intensity, stand height and tilt before placing it. Keep the other two lights for later.", TutorialUIManager.Instance.posePointUp, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("See the shadow the Key makes? Let's soften it with our second light. The Fill circle is opposite the Key, still in front of the subject. Keep it weaker so the shadows retain depth.", TutorialUIManager.Instance.poseOpenHand, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Now let's separate the product from the background. We'll use the last light behind and to one side of the subject, at the Back circle. Raise it to catch the edge without pointing straight into the camera.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void StartBackLightPractice()
    {
        currentStep = GokeLevelStep.PlaceBackLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        BeginGuidedLightPractice();
    }

    [Header("Editable Lighting Observation Tutorial")]
    private readonly Light[] observationLights = new Light[3];
    [SerializeField, TextArea(2, 5)] private string[] lightingObservationTasks =
    {
        "KEY: Notice the light and shadow.",
        "FILL: Notice the softer shadows.",
        "BACK: Notice the bright edge."
    };
    [SerializeField] private string lightingObservationContinue = "[ENTER] Next";
    [SerializeField, TextArea(3, 8)] private string lightingObservationComplete =
        "There's our three-point setup. Key gives shape, Fill softens the shadows, and Back catches the edge. Leave them there; we'll use this set for the camera test.";

    private IEnumerator ObserveLightingSetup()
    {
        currentStep = GokeLevelStep.ObserveLightingSetup;
        isBriefingOpen = false;

        var player = FindObjectOfType<Player.PlayerController.PlayerController>();
        var view = player != null ? player.GameplayCamera : null;
        bool couldMove = player != null && player.canMove, couldLook = player != null && player.canLook;
        Quaternion savedRotation = view != null ? view.transform.rotation : Quaternion.identity;
        Vector3 savedPosition = view != null ? view.transform.position : Vector3.zero;
        float savedFov = view != null ? view.fieldOfView : 60f;
        var savedLights = new Dictionary<Light, bool>();
        var targetRenderer = lightingPracticeTarget != null ? lightingPracticeTarget.GetComponentInChildren<Renderer>() : null;
        Material targetMaterial = targetRenderer != null ? targetRenderer.material : null;
        Color savedColor = targetMaterial != null ? targetMaterial.color : Color.white;
        foreach (var lamp in FindObjectsOfType<Light>())
        {
            savedLights[lamp] = lamp.enabled;
            lamp.enabled = false;
        }
        if (player != null) player.canMove = player.canLook = false;
        if (targetMaterial != null) targetMaterial.color = new Color(.65f, .65f, .65f);
        int observationIndex = 0;
        float orbitAngle = 0f;
        Vector3 orbitCenter = targetRenderer != null ? targetRenderer.bounds.center : savedPosition;
        Vector3 orbitDirection = Vector3.ProjectOnPlane(savedPosition - orbitCenter, Vector3.up).normalized;
        if (orbitDirection.sqrMagnitude < .01f) orbitDirection = Vector3.back;
        const float observationFov = 55f;
        float framingRadius = targetRenderer != null ? targetRenderer.bounds.extents.magnitude : .75f;
        float minimumDistance = framingRadius / Mathf.Sin(observationFov * .5f * Mathf.Deg2Rad) * 1.25f;
        Vector3 orbitOffset = orbitDirection * Mathf.Max(3.5f, minimumDistance) + Vector3.up * .5f;
        try
        {
        if (view != null && targetRenderer != null)
        {
            Quaternion aim = Quaternion.LookRotation(-orbitOffset);
            for (float t = 0; t < 1f;)
            {
                if (!PauseManager.isPaused) t += Time.unscaledDeltaTime;
                view.transform.rotation = Quaternion.Slerp(savedRotation, aim, Mathf.SmoothStep(0, 1, t));
                view.transform.position = Vector3.Lerp(savedPosition, orbitCenter + orbitOffset, Mathf.SmoothStep(0, 1, t));
                view.fieldOfView = Mathf.Lerp(savedFov, observationFov, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
        }
        foreach (string instruction in lightingObservationTasks ?? new string[0])
        {
            if (string.IsNullOrWhiteSpace(instruction)) continue;
            if (TutorialUIManager.Instance != null)
            {
                // Also shorten the previous defaults already serialized in a scene.
                // Leave Inspector-authored instructions untouched.
                string prompt = instruction;
                switch (prompt)
                {
                    case "KEY ONLY: Notice the bright side and the shadow side. Hold [B] to compare without the key light.":
                        prompt = "KEY: Notice the light and shadow."; break;
                    case "ADD FILL: Watch the shadow side become lighter. Hold [B] to compare key-only lighting.":
                        prompt = "FILL: Notice the softer shadows."; break;
                    case "ADD BACK: Look for the bright edge separating the product. Hold [B] to compare without the back light.":
                        prompt = "BACK: Notice the bright edge."; break;
                }
                string next = lightingObservationContinue == "Press [ENTER] when ready"
                    ? "[ENTER] Next" : lightingObservationContinue;
                TutorialUIManager.Instance.SetupTasks(new[] { prompt + "\nHold [B] Compare\n" + next });
            }
            // No timer: each observation stays visible until the player acknowledges it.
            yield return null;
            while (Keyboard.current != null && Keyboard.current.enterKey.isPressed) yield return null;
            while (PauseManager.isPaused || !Application.isFocused || Keyboard.current == null ||
                   !Keyboard.current.enterKey.wasPressedThisFrame)
            {
                if (currentStep != GokeLevelStep.ObserveLightingSetup) yield break;
                if (!PauseManager.isPaused && Application.isFocused && view != null && targetRenderer != null)
                {
                    // Freeze the angle during comparison so only the light changes.
                    if (Keyboard.current == null || !Keyboard.current.bKey.isPressed)
                        orbitAngle += 18f * Time.unscaledDeltaTime;
                    Vector3 offset = Quaternion.AngleAxis(orbitAngle, Vector3.up) * orbitOffset;
                    Vector3 destination = orbitCenter + offset;
                    // Keep the orbit in front of walls/fixtures rather than passing through them.
                    float distance = offset.magnitude;
                    foreach (var hit in Physics.RaycastAll(orbitCenter, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(lightingPracticeTarget)) continue;
                        if (player != null && hit.transform.IsChildOf(player.transform)) continue;
                        distance = Mathf.Min(distance, Mathf.Max(.3f, hit.distance - .15f));
                    }
                    // If an obstacle leaves too little room, keep the last wide view
                    // instead of pushing the camera into an extreme product close-up.
                    if (distance >= minimumDistance)
                    {
                        destination = orbitCenter + offset.normalized * distance;
                        view.transform.SetPositionAndRotation(destination, Quaternion.LookRotation(orbitCenter - destination));
                    }
                }
                if (!PauseManager.isPaused)
                    for (int i = 0; i < observationLights.Length; i++)
                        if (observationLights[i] != null)
                            observationLights[i].enabled = i <= observationIndex &&
                                !(i == observationIndex && Keyboard.current != null && Keyboard.current.bKey.isPressed);
                yield return null;
            }
            observationIndex++;
        }
        }
        finally
        {
            foreach (var entry in savedLights) if (entry.Key != null) entry.Key.enabled = entry.Value;
            if (targetMaterial != null) targetMaterial.color = savedColor;
            if (view != null) { view.transform.SetPositionAndRotation(savedPosition, savedRotation); view.fieldOfView = savedFov; }
            if (player != null) { player.canMove = couldMove; player.canLook = couldLook; player.SyncLookToCamera(); }
        }

        ShowLightingPracticeComplete();
    }

    private void ShowLightingPracticeComplete()
    {
        currentStep = GokeLevelStep.LightingPracticeComplete;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue(lightingObservationComplete, TutorialUIManager.Instance.poseHappy, true, false);
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
                CareerManager.Instance.AcceptJob("Goke Cola", Mathf.Max(0, ProductionEconomy.Advance(2) - PlayerPrefs.GetInt("GokeEquipmentAdvancePaid", 0)));
                int repaid = PlayerPrefs.GetInt("GokeEquipmentAdvancePaid", 0);
                PlayerPrefs.SetInt("GokeEquipmentAdvancePaid", 0);
                if (repaid > 0) GameFeedback.Show("BOSS LOAN REPAID\n" + repaid.ToString("N0") + " B-Coins deducted from the contract advance");
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
        AlmanacManager.Instance.RequestTechniqueReviewHighlight();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Almanac" });
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
        var held = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        if (held != null && held.GetHeldItem() is Player.Equipment.FilmCameraItem) hasPickedUpLevel2Camera = true;
        if (hasPickedUpLevel2Camera)
        {
            ShowSDCardPickupIntroduction();
            return;
        }

        isBriefingOpen = false;
        currentStep = GokeLevelStep.PickUpCamera;
        foreach (var camera in FindObjectsOfType<Player.Equipment.FilmCameraItem>(true))
            if (camera.GetComponentInParent<Player.PlayerController.PlayerController>() != null)
            { hasPickedUpLevel2Camera = true; ShowSDCardPickupIntroduction(); return; }

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the NONY FX Camera from the delivery table" });
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
            TutorialUIManager.Instance.ShowBossDialogue("Select your NONY FX Camera and press <color=red>[C]</color> to insert the blank SD Card. We need that in before we can use the viewfinder or record.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void StartSDCardInsertion()
    {
        isBriefingOpen = false;
        currentStep = GokeLevelStep.InsertSDCard;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Equip the NONY FX Camera", "- Press <color=red>[C]</color> to insert the SD Card" });
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
                "Equip the NONY FX Camera and stand on the CAMERA circle",
                () => GuidedPracticeLesson.AtMarker(cameraPracticeMarker)));
        }
        steps.Add(new GuidedPracticeLesson.Step(
            "Click <color=red>[Left Click]</color> to open the viewfinder. Next, enable the optional grid in F2 settings.",
            "[Left Click] Open the NONY FX Camera viewfinder",
            () => cameraPracticeViewOpen));
        steps.Add(new GuidedPracticeLesson.Step(
            "First, press F2 to open camera settings. We'll change one setting before returning to the viewfinder.",
            "Press [F2] to OPEN camera settings",
            () => { var held = FindObjectOfType<Player.Interactor.EquipmentInteractor>(); return held != null && held.GetHeldItem() is Player.Equipment.FilmCameraItem camera && camera.SettingsOpen; }));
        steps.Add(new GuidedPracticeLesson.Step(
            "With settings open, select Grid and press the RIGHT ARROW to turn it ON. The grid helps you place your subject.",
            "Set GRID to ON with the RIGHT ARROW",
            () => { var held = FindObjectOfType<Player.Interactor.EquipmentInteractor>(); return held != null && held.GetHeldItem() is Player.Equipment.FilmCameraItem camera && camera.GridEnabled; }));
        steps.Add(new GuidedPracticeLesson.Step(
            "Now press F2 again to CLOSE settings. This returns you to the clear viewfinder. Then we'll practise framing.",
            "Press [F2] to CLOSE camera settings",
            () => { var held = FindObjectOfType<Player.Interactor.EquipmentInteractor>(); return held != null && held.GetHeldItem() is Player.Equipment.FilmCameraItem camera && camera.IsCameraViewActive() && !camera.SettingsOpen; }));
        steps.Add(new GuidedPracticeLesson.Step(
            "Place the subject on the LOWER-LEFT gold dot. Keep it fully visible. The space on the right is for your message.",
            "Place the subject on the LOWER-LEFT crossing; leave space on the RIGHT",
            () => thirdsPracticeStage >= 1, permission: "camera.frame"));
        steps.Add(new GuidedPracticeLesson.Step(
            "Good! Now frame the subject on the LOWER-RIGHT dot. Notice how your message space moves to the left.",
            "Place the subject on the LOWER-RIGHT crossing; leave space on the LEFT",
            () => thirdsPracticeStage >= 2, permission: "camera.frame"));
        steps.Add(new GuidedPracticeLesson.Step(
            "Turn Grid OFF in F2, then close settings. Frame the subject lower-right again, leaving message space on the left. The feedback will help.",
            "Turn Grid OFF; frame the whole subject lower-right with message space on the LEFT",
            () => hasCompletedRuleOfThirdsPractice, permission: "camera.frame"));
        practiceLesson = new GuidedPracticeLesson(tutorialManager, steps, () =>
        {
            practiceLesson = null;
            if (cameraPracticeMarker != null) cameraPracticeMarker.gameObject.SetActive(false);
            ShowFramingSuccess();
        }, cameraPracticeMarker, lockMovementAtStation: false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Use your existing light to keep the subject readable. This job focuses on composition: leave space beside the subject and place your branding there. We'll learn three-point lighting with Better Lights in Level 3.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void ShowPostProductionTutorial()
    {
        currentStep = GokeLevelStep.ExplainPostProduction;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Record at least 8 seconds of product footage with <color=red>[R]</color>, then take the card to the computer. Build a 12-second video: 2-second intro, 8 seconds of your footage, and 2-second outro. Choose the timing of your two graphics.", TutorialUIManager.Instance.poseBoss, true, false);
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
        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();
        if (shopTerminal == null) return;
        shopTerminal.RestoreProductionCamera();
        sdCardItemIndex = shopTerminal.availableItems.FindIndex(item => item.itemName.Contains("SD"));
        lightItemIndex = shopTerminal.availableItems.FindIndex(item => item.itemName == "160 LED PANEL");
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
        float frontLightDistance = Mathf.Min(5.6f, stageFrontExtent * 0.78f, stageSideExtent * 0.48f);
        float sideLightDistance = frontLightDistance;
        float backLightDistance = Mathf.Min(3.2f, stageFrontExtent * 0.44f);
        float backLightSideDistance = Mathf.Min(4.2f, stageSideExtent * 0.4f);

        Vector3 targetPosition = stageCenter - stageFront * Mathf.Min(1.8f, stageFrontExtent * 0.24f);
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
            lightingPracticeWall = lightingPracticeDirector.CreatePracticeWall(new Color(0.5f, 0.5f, 0.5f, 1f));
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

        CreatePracticeLabel(targetRoot.transform, new Vector3(0f, 1.65f, 0f), "PRACTICE\nSUBJECT", Color.black);
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
        if (stageManager != null) stageManager.ClearAllProps();

        // Keep the purchased backdrop; only remove the previous contract's props.
        // imported studio windows and fixtures also use names such as Cube.001.
    }
}
