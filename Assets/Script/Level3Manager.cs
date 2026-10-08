using PlayerPrefs = GameSavePrefs;
using System.Collections;
using System.Collections.Generic;
using Player.Equipment;
using TMPro;
using UnityEngine;

public class Level3Manager : MonoBehaviour
{
    public static Level3Manager Instance;
    private bool rimLessonStarted;
    private bool cameraPracticeActive;
    public bool RecordingBlockedByPractice => cameraPracticeActive || lightingPracticeRoot != null;
    public bool RimEquipmentAvailable { get; private set; }
    private float rimSetupReadySince = -1f;

    private bool CarStageAndLightReady()
    {
        var director = FindObjectOfType<DirectorTerminal>();
        var car = FindObjectOfType<CubeVehicle>();
        if(director == null || !director.HasWall() || director.IsTerminalActive() || car == null) return false;
        var shop = FindObjectOfType<ShopTerminal>();
        if(shop != null && shop.IsTerminalActive()) return false;
        if(TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return false;
        var bounds = new Bounds(car.transform.position, Vector3.zero);
        foreach(var renderer in car.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        foreach(var light in FindObjectsOfType<FilmLightItem>())
        {
            if(light.GetComponentInParent<Player.Interactor.EquipmentInteractor>() != null ||
                !light.IsPoweredOn() || light.spotlight == null ||
                (light.EquipmentName != "Level 3 Soft Light" && light.forcesHardLight) ||
                light.intensityPercent < 30f || light.diffusionPercent < 50f) continue;
            Vector3 direction = bounds.center - light.spotlight.transform.position;
            float halfAngle = light.spotlight.spotAngle * .5f;
            if(direction.magnitude <= light.spotlight.range &&
                Vector3.Angle(light.spotlight.transform.forward, direction) <= halfAngle) return true;
        }
        return false;
    }

    private ProductionKit LessonStrip()
    {
        foreach(var kit in FindObjectsOfType<ProductionKit>(true))
            if(!kit.template && kit.kind == 0) return kit;
        return null;
    }

    private bool StripNearCar()
    {
        var strip=LessonStrip(); var car=FindObjectOfType<CubeVehicle>();
        if(strip==null || !strip.placed || car==null) return false;
        var bounds=new Bounds(car.transform.position,Vector3.zero);
        foreach(var r in car.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var p=strip.transform.position+Vector3.up*1.2f;
        return Vector3.Distance(bounds.ClosestPoint(p),p)<3f;
    }

    private void BeginCameraMovementLesson(bool afterLightPlacement = false)
    {
        cameraPracticeActive = true;
        rimLessonStarted=true;
        var inventory = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        Vector3 previousPosition = Vector3.zero;
        bool trackingMovement = false;
        float practiceSeconds = 0f;
        practiceLesson=new GuidedPracticeLesson(tutorialManager,new List<GuidedPracticeLesson.Step>
        {
            new GuidedPracticeLesson.Step("Your Soft Light is placed at 3200K for a warm look. Now pick up your camera with <color=yellow>[E]</color> and select its hotbar slot. If it is already in your inventory, just equip it. We'll practice smooth movement before recording.","Pick up and equip your camera",()=>inventory != null && inventory.GetHeldItem() is FilmCameraItem),
            new GuidedPracticeLesson.Step("Next, pick up an SD card with <color=yellow>[E]</color>. It stores multiple takes, up to 60 seconds in total. If you have none, buy an SD card from the shop and collect it from delivery. Keep it in your hotbar for now; this movement rehearsal does not need a recording.","Collect an SD card with free space",()=>inventory != null && inventory.HasBlankSDCard()),
            new GuidedPracticeLesson.Step("Select your camera and click <color=yellow>Left Mouse Button</color> to open the viewfinder. Look through it to frame the lit subject before practicing movement.","Equip camera and click LMB to open the viewfinder",()=>inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.IsCameraViewActive()),
            new GuidedPracticeLesson.Step("White balance changes how the camera renders color; it does not change the lamps. Let's compare two settings on this practice subject. Press F2 to open camera settings.", "[F2] Open camera settings", () => inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.IsCameraViewActive() && camera.SettingsOpen),
            new GuidedPracticeLesson.Step("Select WHITE BALANCE with Up/Down. Use Left/Right to set 3200K. Look at the subject and remember its color at this setting.", "Set WHITE BALANCE to 3200K", () => inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.SettingsOpen && Mathf.Abs(camera.WhiteBalanceKelvin - 3200f) < 50f),
            new GuidedPracticeLesson.Step("Now raise white balance to 6500K with Right Arrow. Watch the image become warmer even though the lamps have not changed.", "Raise WHITE BALANCE to 6500K and compare", () => inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.SettingsOpen && Mathf.Abs(camera.WhiteBalanceKelvin - 6500f) < 50f),
            new GuidedPracticeLesson.Step("Return to 3200K with Left Arrow. Notice the cooler result compared with 6500K. Judge the subject's colors, not just the number: white balance is a camera adjustment, not lamp brightness.", "Return WHITE BALANCE to 3200K", () => inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.SettingsOpen && Mathf.Abs(camera.WhiteBalanceKelvin - 3200f) < 50f),
            new GuidedPracticeLesson.Step("Press F2 to close settings. Keep the viewfinder open so we can practice steady movement next.", "[F2] Close camera settings", () => inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.IsCameraViewActive() && !camera.SettingsOpen),
            new GuidedPracticeLesson.Step("Keep the viewfinder open. Hold <color=yellow>Ctrl</color> while moving with WASD, and turn gently with the mouse. Ctrl slows walking and looking for a steady shot. Practice for five seconds while keeping the lit subject framed; then I'll come back. We are rehearsing, so do not record yet.","Keep viewfinder open; practice Ctrl + WASD for 5 seconds",()=>
            {
                var keys = UnityEngine.InputSystem.Keyboard.current;
                bool moving = inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.IsCameraViewActive() && keys != null &&
                    (keys.leftCtrlKey.isPressed || keys.rightCtrlKey.isPressed) &&
                    (keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed);
                if (!moving) { trackingMovement = false; return practiceSeconds >= 5f; }
                Vector3 position = inventory.transform.position;
                if (trackingMovement)
                {
                    Vector3 delta = position - previousPosition;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > .000001f) practiceSeconds += Time.deltaTime;
                }
                previousPosition = position;
                trackingMovement = true;
                return practiceSeconds >= 5f;
            }),
            new GuidedPracticeLesson.Step("Good practice! You've compared white balance and tried steady movement. When filming, start gently, keep your subject framed, and let the camera settle before stopping. We'll discuss the next job after you accept its contract.","Finish the camera practice",()=>true)
        },()=>
        {
            practiceLesson=null;
            cameraPracticeActive = false;
            PlayerPrefs.SetInt("Level3CameraMovementLessonComplete",1);
            PlayerPrefs.Save();
            if (afterLightPlacement) ShowLightingPracticeComplete();
            else ShowLevelTasks();
        });
    }

    private enum Level3Step
    {
        GokeResults,
        Introduction,
        IntroduceLight,
        BuyLight,
        LightCheckout,
        CloseLightShop,
        IntroducePickup,
        PickUpLight,
        IntroducePractice,
        PlaceSoftLight,
        ObserveSoftLight,
        PracticeComplete,
        IntroduceAlmanac,
        OpenAlmanac,
        ReviewAlmanac,
        IntroduceContract,
        OfferContract,
        ContractAccepted,
        LevelActive
    }

    private bool isLevelStarted = false;
    private bool isBriefingOpen = false;
    private bool requiresLightPurchase = false;
    private Level3Step currentStep;
    private TutorialManager tutorialManager;
    private ContractUIManager contractUIManager;
    private int level3LightItemIndex = -1;
    private FilmLightItem practiceLight;
    private int threePointRole;
    private FilmLightItem collectedBackLight;
    private Vector3 practiceFront;
    private Vector3 practiceRight;
    private float practiceFrontDistance;
    private float practiceSideDistance;
    private FilmLightItem purchasedPracticeLight;
    private readonly List<FilmLightItem> threePointPracticeLights = new List<FilmLightItem>();
    private readonly List<GameObject> loanLights = new List<GameObject>();
    private string PracticeRole => threePointRole == 0 ? "Soft Key" : threePointRole == 1 ? "Soft Fill" : "Soft Back";
    private float PracticeIntensity => threePointRole == 0 ? 75f : threePointRole == 1 ? 40f : 60f;
    private float PracticeDiffusion => threePointRole == 0 ? 75f : threePointRole == 1 ? 100f : 25f;
    private GuidedPracticeLesson practiceLesson;
    private GameObject lightingPracticeRoot;
    private GameObject lightingPracticeWall;
    private DirectorTerminal lightingPracticeDirector;
    private Transform lightingPracticeTarget;
    private Transform softLightPlacementMarker;
    private List<TextMeshPro> practiceMarkerLabels = new List<TextMeshPro>();

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
        foreach (var loan in loanLights) if (loan != null) Destroy(loan);
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
        CleanUpLightingPractice();
        currentStep = Level3Step.LevelActive;
        isBriefingOpen = false;
        enabled = false;
        if (PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 0) OfferContract();
    }

    private void LateUpdate()
    {
        if(currentStep==Level3Step.LevelActive && isLevelStarted && !DevTutorialBypass.Disabled && !rimLessonStarted && PlayerPrefs.GetInt("Level3CameraMovementLessonComplete",0)==0 && PlayerPrefs.GetInt("LamborminiContractAccepted",0)==1)
        {
            if(CarStageAndLightReady())
            {
                if(rimSetupReadySince < 0f) rimSetupReadySince = Time.time;
                if(Time.time - rimSetupReadySince >= .5f) BeginCameraMovementLesson();
            }
            else rimSetupReadySince = -1f;
        }
        practiceLesson?.Tick();
        CampaignGuidance.Update(tutorialManager, isBriefingOpen || (practiceLesson != null && practiceLesson.IsExplaining) ? "" : currentStep.ToString(), 3);
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
        currentStep = Level3Step.GokeResults;

        CampaignProgression.SetCurrentLevel(3);

        bool restartLevelIntroduction = CampaignProgression.ConsumeCheatIntroduction(3);
        bool contractAlreadyAccepted = PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 1;
        bool purchaseLessonCompleted = PlayerPrefs.GetInt("Level3LightPurchaseLessonCompleted", 0) == 1;

        requiresLightPurchase = restartLevelIntroduction || !purchaseLessonCompleted;
        if (requiresLightPurchase)
        {
            PlayerPrefs.SetInt("Level3LightPurchased", 0);
            PlayerPrefs.SetInt("Level3LightPurchaseLessonCompleted", 0);
            PlayerPrefs.Save();
        }

        SetupLevel3Equipment();
        SetupContractUI();

        if (AlmanacManager.Instance != null && (!contractAlreadyAccepted || restartLevelIntroduction || requiresLightPurchase))
        {
            AlmanacManager.Instance.PrepareLevelIntroduction(3);
        }

        if (contractAlreadyAccepted && !restartLevelIntroduction && !requiresLightPurchase)
        {
            if (CareerManager.Instance != null) CareerManager.Instance.currentActiveJob = "Terrari";
            if (contractUIManager != null) contractUIManager.UnlockQualifications();
            StartLevel();
            if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        string gokeGrade = CrossSceneData.finalGrades.letterGrade;
        if (string.IsNullOrEmpty(gokeGrade)) gokeGrade = "PASS";

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Goke's happy with the commercial! You earned a <color=yellow>" + gokeGrade + "</color>. That lighting and two-graphic edit were a step up from our first job.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    public void CloseBriefing()
    {
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.TryAdvanceBossDialoguePage()) return;
        if (practiceLesson != null) { practiceLesson.Continue(); return; }
        if (!isBriefingOpen) return;

        if (currentStep == Level3Step.GokeResults)
        {
            ShowLevelIntroduction();
            return;
        }

        if (currentStep == Level3Step.Introduction)
        {
            ShowLightIntroduction();
            return;
        }

        if (currentStep == Level3Step.IntroduceLight)
        {
            StartLightPurchase();
            return;
        }

        if (currentStep == Level3Step.IntroducePickup)
        {
            StartLightPickup();
            return;
        }

        if (currentStep == Level3Step.IntroducePractice)
        {
            StartSoftLightPractice();
            return;
        }

        if (currentStep == Level3Step.PracticeComplete)
        {
            FinishLightingPractice();
            return;
        }

        if (currentStep == Level3Step.IntroduceAlmanac)
        {
            StartAlmanacReview();
            return;
        }

        if (currentStep == Level3Step.ReviewAlmanac)
        {
            ShowContractIntroduction();
            return;
        }

        if (currentStep == Level3Step.IntroduceContract)
        {
            OfferContract();
            return;
        }

        if (currentStep == Level3Step.OfferContract)
        {
            AcceptContract();
            return;
        }

        if (currentStep == Level3Step.ContractAccepted)
        {
            StartLevel();
        }
    }

    public bool IsBriefingActive()
    {
        return isBriefingOpen || (practiceLesson != null && practiceLesson.IsExplaining);
    }

    public bool IsEquipmentIntroductionActive()
    {
        return isLevelStarted && currentStep != Level3Step.LevelActive;
    }

    public bool CanOpenAlmanac()
    {
        return currentStep == Level3Step.OpenAlmanac ||
               currentStep == Level3Step.ReviewAlmanac ||
               currentStep == Level3Step.LevelActive;
    }

    public bool CanOpenContractQualifications()
    {
        return currentStep == Level3Step.ContractAccepted ||
               currentStep == Level3Step.LevelActive;
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (threePointRole == 0 && itemIndex == level3LightItemIndex &&
            (currentStep == Level3Step.BuyLight || currentStep == Level3Step.LightCheckout))
        {
            var shop = FindObjectOfType<ShopTerminal>();
            if (shop != null && shop.CartItemCount(itemIndex) >= 1)
            {
                tutorialManager?.ShowWarning("Only ONE Better Light for now. Confirm this purchase and set up your Key light first.");
                return false;
            }
        }
        if (currentStep == Level3Step.BuyLight)
        {
            if (itemIndex != level3LightItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Let's buy the Better Lights first. That's the tool we're trying today.");
                return false;
            }

            return true;
        }

        if (currentStep == Level3Step.LightCheckout)
        {
            return itemIndex == level3LightItemIndex;
        }

        return true;
    }

    public void OnBetterLightCartChanged(ShopTerminal shop)
    {
        if (shop == null || (currentStep != Level3Step.BuyLight && currentStep != Level3Step.LightCheckout)) return;
        int required = threePointRole == 1 ? 2 : 1;
        int count = shop.CartItemCount(level3LightItemIndex);
        bool ready = count >= required;
        currentStep = ready ? Level3Step.LightCheckout : Level3Step.BuyLight;
        TutorialUIManager.Instance?.SetupTasks(new[] { ready
            ? "- Confirm the Better Lights purchase"
            : "- Add " + (required - count) + " more Better Light to your cart (" + count + "/" + required + ")" });
    }

    public bool CanConfirmPurchase()
    {
        if (currentStep == Level3Step.LightCheckout)
        {
            var shop = FindObjectOfType<ShopTerminal>();
            int required = threePointRole == 1 ? 2 : 1;
            if (threePointRole == 0 && shop != null && shop.CartItemCount(level3LightItemIndex) > 1)
            {
                tutorialManager?.ShowWarning("Buy only ONE Better Light for the first lesson. Remove the extra lights before confirming.");
                return false;
            }
            if (shop != null && shop.CartItemCount(level3LightItemIndex) >= required) return true;
            tutorialManager?.ShowWarning("Add " + required + " Better Lights to the cart, then confirm.");
            return false;
        }

        if (currentStep == Level3Step.BuyLight)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("We're still missing the Better Lights. Add it to the cart first.");
            return false;
        }

        return true;
    }

    public bool CanCancelPurchase()
    {
        if (currentStep != Level3Step.BuyLight && currentStep != Level3Step.LightCheckout) return true;

        if (tutorialManager != null) tutorialManager.ShowWarning("Let's finish ordering the Soft Light before we head to the stage.");
        return false;
    }

    public void OnShopOpened()
    {
        if (currentStep != Level3Step.BuyLight) return;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { threePointRole == 1 ? "- Open LIGHTS and add TWO Better Lights to your cart" : "- Open LIGHTS and add a Better Light to your cart" });
        }
    }

    public void OnEquipmentBought(int itemsCount)
    {
        if (currentStep != Level3Step.LightCheckout) return;

        requiresLightPurchase = false;
        PlayerPrefs.SetInt("Level3LightPurchaseLessonCompleted", 1);
        PlayerPrefs.Save();
        currentStep = Level3Step.CloseLightShop;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetDynamicGlow("shop", false);
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Close the Equipment Shop" });
        }
    }

    public void OnShopClosed()
    {
        if (currentStep != Level3Step.CloseLightShop) return;
        ShowLightPickupIntroduction();
    }

    public void OnLightPickedUp(FilmLightItem light)
    {
        if (currentStep != Level3Step.PickUpLight || light == null) return;
        if (threePointPracticeLights.Contains(light))
        {
            tutorialManager?.ShowWarning("Pick up another Better Light from delivery. Leave the placed lights on the stage.");
            return;
        }

        if (light.EquipmentName != "Level 3 Soft Light")
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Grab the Better Lights from delivery with [E]. We'll try it on the stage.");
            return;
        }

        if (threePointRole == 1 && collectedBackLight == null)
        {
            collectedBackLight = light;
            TutorialUIManager.Instance?.SetupTasks(new[] { "- Pick up the SECOND Better Light from delivery (1/2 collected)" });
            return;
        }
        if (threePointRole == 1 && light == collectedBackLight) return;
        practiceLight = light;
        if (threePointRole == 0) purchasedPracticeLight = light;
        ShowLightingPracticeIntroduction();
    }

    public bool CanPickUpLight(FilmLightItem light)
    {
        if (light == null || !threePointPracticeLights.Contains(light)) return true;

        if (tutorialManager != null) tutorialManager.ShowWarning("Leave the light there for a moment. Take a look at what it does to the surface.");
        return false;
    }

    public void OnLightTurnedOn(FilmLightItem light)
    {
        if (currentStep == Level3Step.PlaceSoftLight && light == practiceLight) ShowCurrentLightingTasks();
    }

    public void OnLightIntensityChanged(FilmLightItem light, float intensity)
    {
        if (currentStep == Level3Step.PlaceSoftLight && light == practiceLight) ShowCurrentLightingTasks();
    }

    public void OnLightTilted(float tilt)
    {
        if (currentStep == Level3Step.PlaceSoftLight) ShowCurrentLightingTasks();
    }

    public void OnLightFeatureChanged(FilmLightItem light)
    {
        if (currentStep == Level3Step.PlaceSoftLight && light == practiceLight) ShowCurrentLightingTasks();
    }

    public void OnLightDropped(FilmLightItem light)
    {
        if (currentStep != Level3Step.PlaceSoftLight || !IsAvailablePracticeLight(light) || softLightPlacementMarker == null) return;
        practiceLight = light;

        if (practiceLesson != null && (!practiceLesson.ReadyToPlace || !GuidedPracticeLesson.AtMarker(softLightPlacementMarker)))
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Pick the light up with [E] and finish the current step on the circle first.");
            return;
        }

        // Use the same player-position check as entry into the guided lesson.
        bool isNearMarker = GuidedPracticeLesson.AtMarker(softLightPlacementMarker);
        bool hasCorrectIntensity = Mathf.Abs(light.intensityPercent - PracticeIntensity) <= 2.5f;
        bool hasCorrectTilt = Mathf.Abs(light.GetCurrentTilt() + 10f) <= 2.5f;
        bool hasCorrectTemperature = Mathf.Abs(light.GetColorTemperature() - 3200f) <= 250f;
        bool hasCorrectDiffusion = Mathf.Abs(light.GetDiffusionPercent() - PracticeDiffusion) <= 2.5f;

        if (!light.IsPoweredOn() || !hasCorrectIntensity || !hasCorrectTilt || !hasCorrectTemperature || !hasCorrectDiffusion || !isNearMarker)
        {
            if (tutorialManager != null)
            {
                string correction = !light.IsPoweredOn()
                    ? "Turn the Soft Light ON with Left Mouse Button."
                    : !hasCorrectIntensity
                        ? "Set the " + PracticeRole + " to " + PracticeIntensity + "% with the mouse wheel."
                        : !hasCorrectTilt
                            ? "Set the tilt to -10 degrees with the arrow keys."
                            : !hasCorrectTemperature
                                ? "Set color temperature to 3200K with Z and X."
                                : !hasCorrectDiffusion
                                    ? "Set diffusion to " + PracticeDiffusion + "% with V and B."
                                    : "Stand on the " + PracticeRole.ToUpperInvariant() + " marker before pressing G.";
                tutorialManager.ShowWarning("Let's adjust that a little. Pick the light up again. " + correction);
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

        softLightPlacementMarker.gameObject.SetActive(false);
        threePointPracticeLights.Add(light);
        if (threePointRole < 2 && TryPrepareNextPracticeLight()) return;
        currentStep = Level3Step.ObserveSoftLight;
        isBriefingOpen = false;
        StartCoroutine(CompareThreePointLights());
    }

    public void OnContractQualificationsOpened()
    {
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review Automotive Composition",
                "- Review Soft Reflective Lighting",
                "- Press <color=red>[TAB]</color> when finished"
            });
        }
    }

    public void OnContractQualificationsClosed()
    {
        if (currentStep == Level3Step.LevelActive) ShowLevelTasks();
    }

    public void OnAlmanacOpened()
    {
        if (currentStep != Level3Step.OpenAlmanac) return;

        currentStep = Level3Step.ReviewAlmanac;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review the Better Lights guide",
                "- Review Soft Lighting for Reflective Surfaces",
                "- Review Automotive Staging",
                "- Press <color=red>[P]</color> or CLOSE when finished"
            });
        }
    }

    public void OnAlmanacClosed()
    {
        if (currentStep != Level3Step.ReviewAlmanac) return;

        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("That guide's yours to keep. If the Soft Light controls slip your mind, press <color=red>[P]</color> and take another look.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowLevelIntroduction()
    {
        currentStep = Level3Step.Introduction;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Welcome to <color=yellow>Level 3</color>. First, practice softer lighting, camera white balance and smooth movement. Soft highlights reveal shape; white balance controls how warm or cool the camera image looks. We'll review the next job after practice.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void StartLevel()
    {
        isBriefingOpen = false;
        currentStep = Level3Step.LevelActive;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            ShowLevelTasks();
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
    }

    private void SetupLevel3Equipment()
    {
        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();
        if (shopTerminal == null || shopTerminal.availableItems.Count < 2) return;

        shopTerminal.RestoreProductionCamera();

        bool usePlaceholder = shopTerminal.level3LightPrefab == null;
        GameObject lightPrefab = usePlaceholder ? shopTerminal.availableItems[1].prefabToSpawn : shopTerminal.level3LightPrefab;
        if (lightPrefab == null) return;

        if (threePointRole == 0 && !requiresLightPurchase && PlayerPrefs.GetInt("Level3LightPurchased", 0) == 1)
        {
            shopTerminal.RestoreLevel3Light(lightPrefab, usePlaceholder);
            level3LightItemIndex = shopTerminal.availableItems.FindIndex(item => item.itemName == "LEVEL 3 SOFT LIGHT");
        }
        else
        {
            FilmLightItem[] existingLights = FindObjectsOfType<FilmLightItem>(true);
            foreach (FilmLightItem existingLight in existingLights)
            {
                if (existingLight != null && existingLight.EquipmentName == "Level 3 Soft Light") Destroy(existingLight.gameObject);
            }

            level3LightItemIndex = shopTerminal.SetupLevel3Light(lightPrefab, usePlaceholder);
        }
    }

    private void SetupContractUI()
    {
        contractUIManager = FindObjectOfType<ContractUIManager>();
        if (contractUIManager == null) contractUIManager = gameObject.AddComponent<ContractUIManager>();
        if (contractUIManager != null) contractUIManager.PrepareLevel3Contract();
    }

    private void ShowLightIntroduction()
    {
        currentStep = Level3Step.IntroduceLight;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Our new tool is <color=yellow>Better Lights</color>. Practice at 3200K to see a warm light. Output changes brightness; diffusion softens reflections. Hold Q/E to adjust stand height, then G to place it. Your camera also unlocks white balance in F2 settings. It changes the camera image separately from the light. Open the equipment shop with <color=red>[E]</color>.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void StartLightPurchase()
    {
        if (!requiresLightPurchase && PlayerPrefs.GetInt("Level3LightPurchased", 0) == 1)
        {
            ShowLightPickupIntroduction();
            return;
        }

        if (level3LightItemIndex == -1)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The Better Lights is missing from the Equipment Shop.");
            ShowLightPickupIntroduction();
            return;
        }

        currentStep = Level3Step.BuyLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { threePointRole == 1 ? "- Buy TWO more Better Lights from the Equipment Shop" : "- Buy a Better Light from the Equipment Shop" });
            TutorialUIManager.Instance.SetDynamicGlow("shop", true);
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
        if (tutorialManager != null) tutorialManager.PointLineAt("shop");
    }

    private void ShowLightPickupIntroduction()
    {
        currentStep = Level3Step.IntroducePickup;
        isBriefingOpen = true;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Your Soft Light's arrived. Pick it up from the delivery table with <color=red>[E]</color> and bring it to the stage marker.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void StartLightPickup()
    {
        if (threePointRole == 2 && collectedBackLight != null)
        {
            practiceLight = collectedBackLight;
            ShowLightingPracticeIntroduction();
            return;
        }
        currentStep = Level3Step.PickUpLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { threePointRole == 1 ? "- Pick up BOTH Better Lights from delivery (0/2 collected)" : "- Pick up the Better Light from the delivery table" });
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
    }

    private void ShowLightingPracticeIntroduction()
    {
        CreateLightingPractice();
        currentStep = Level3Step.IntroducePractice;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            string purpose = threePointRole == 0
                ? "KEY is the main light. Place it in front and to one side to create a bright side and a shadow side."
                : threePointRole == 1
                    ? "FILL sits on the opposite front side. Keep it weaker than the Key to soften shadows without flattening the subject."
                    : "BACK sits behind the subject. Its job is a bright edge that separates the subject from the background.";
            TutorialUIManager.Instance.ShowBossDialogue(purpose + " Follow the green circle; we'll adjust one control at a time.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartSoftLightPractice()
    {
        currentStep = Level3Step.PlaceSoftLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        practiceLesson = GuidedPracticeLesson.Light(tutorialManager, softLightPlacementMarker,
            GetHeldPracticeLight,
            PracticeRole, PracticeIntensity, true, lightingPracticeTarget);
    }

    private bool IsAvailablePracticeLight(FilmLightItem light)
    {
        return light != null && light.HasAdvancedFeatures() && !threePointPracticeLights.Contains(light);
    }

    private FilmLightItem GetHeldPracticeLight()
    {
        var inventory = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        var light = inventory != null ? inventory.GetHeldItem() as FilmLightItem : null;
        if (!IsAvailablePracticeLight(light)) return null;

        // Follow the equipped light, not the order in which deliveries were picked up.
        // Keep the other collected light available for the Back role.
        if (threePointRole == 1 && light == collectedBackLight && practiceLight != light &&
            IsAvailablePracticeLight(practiceLight))
            collectedBackLight = practiceLight;
        practiceLight = light;
        return light;
    }

    private bool TryPrepareNextPracticeLight()
    {
        var shop = FindObjectOfType<ShopTerminal>();
        if (shop == null || level3LightItemIndex < 0 || level3LightItemIndex >= shop.availableItems.Count ||
            shop.availableItems[level3LightItemIndex].prefabToSpawn == null) return false;
        threePointRole++;
        Vector3 position = lightingPracticeTarget.position + (threePointRole == 1
            ? practiceFront * practiceFrontDistance + practiceRight * practiceSideDistance
            : -practiceFront * Mathf.Min(practiceFrontDistance, 2.2f) + practiceRight * practiceSideDistance);
        var stage = FindStageRenderer();
        if (stage != null) position = ClampPracticePointToStage(position, stage.bounds);
        softLightPlacementMarker = CreatePlacementMarker(position);
        foreach (var label in softLightPlacementMarker.GetComponentsInChildren<TextMeshPro>())
            label.text = PracticeRole.ToUpperInvariant() + "\n" + PracticeIntensity + "%";
        practiceLight = null;
        requiresLightPurchase = threePointRole == 1;
        currentStep = threePointRole == 1 ? Level3Step.IntroduceLight : Level3Step.IntroducePickup;
        isBriefingOpen = true;
        var ui = TutorialUIManager.Instance;
        if (ui != null) ui.ShowBossDialogue(threePointRole == 1
            ? "Your Key light gives the subject shape. Buy TWO more Better Lights from the shop: one for Fill and one for Back. These are yours to keep. Leave the Key on."
            : "Equip the remaining Better Light from your hotbar. Place it behind the subject as the Back light to reveal its outline. Leave the Key and Fill in place.", ui.posePointUp, true, false);
        return true;
    }

    private IEnumerator CompareThreePointLights()
    {
        var savedLights = new Dictionary<Light, bool>();
        var savedRenderers = new Dictionary<Renderer, bool>();
        var player = FindObjectOfType<Player.PlayerController.PlayerController>();
        var view = player != null ? player.GameplayCamera : null;
        Vector3 savedPosition = view != null ? view.transform.position : Vector3.zero;
        Quaternion savedRotation = view != null ? view.transform.rotation : Quaternion.identity;
        float savedFov = view != null ? view.fieldOfView : 60f;
        bool couldMove = player != null && player.canMove;
        bool couldLook = player != null && player.canLook;
        float orbitAngle = 0f;
        string[] prompts = {
            "KEY ONLY: FIND THE BRIGHT SIDE AND SHADOW SIDE.",
            "ADD FILL: WATCH THE SHADOW SIDE BECOME SOFTER.",
            "ADD BACK: LOOK FOR THE BRIGHT EDGE AROUND THE SUBJECT." };
        try
        {
            foreach (var lamp in FindObjectsOfType<Light>())
            {
                savedLights[lamp] = lamp.enabled;
                lamp.enabled = false;
            }
            if (player != null)
            {
                player.canMove = player.canLook = false;
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    savedRenderers[renderer] = renderer.forceRenderingOff;
                    renderer.forceRenderingOff = true;
                }
            }
            // Compare from the intended front of the subject, not the Back-light marker.
            if (view != null && lightingPracticeTarget != null)
            {
                Vector3 center = lightingPracticeTarget.position + Vector3.up * .7f;
                Vector3 destination = center + practiceFront * 4.5f + Vector3.up * .4f;
                Quaternion rotation = Quaternion.LookRotation(center - destination);
                for (float elapsed = 0; elapsed < 1f;)
                {
                    if (!PauseManager.isPaused && Application.isFocused) elapsed += Time.unscaledDeltaTime;
                    float blend = Mathf.SmoothStep(0f, 1f, elapsed);
                    view.transform.SetPositionAndRotation(Vector3.Lerp(savedPosition, destination, blend), Quaternion.Slerp(savedRotation, rotation, blend));
                    view.fieldOfView = Mathf.Lerp(savedFov, 55f, blend);
                    yield return null;
                }
            }
            for (int role = 0; role < threePointPracticeLights.Count; role++)
            {
                bool compared = false;
                TutorialUIManager.Instance?.SetupTasks(new[] { prompts[role] + "\nHOLD [B] TO SEE WITHOUT THIS LIGHT." });
                yield return null;
                while (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.enterKey.isPressed) yield return null;
                while (true)
                {
                    var keys = UnityEngine.InputSystem.Keyboard.current;
                    if (!PauseManager.isPaused && Application.isFocused && keys != null)
                    {
                        if (view != null && lightingPracticeTarget != null && !keys.bKey.isPressed)
                        {
                            orbitAngle = (orbitAngle + 10f * Time.unscaledDeltaTime) % 360f;
                            Vector3 center = lightingPracticeTarget.position + Vector3.up * .7f;
                            Vector3 offset = Quaternion.AngleAxis(orbitAngle, Vector3.up) * practiceFront * 4.5f + Vector3.up * .4f;
                            // Keep the camera on the subject side of walls and scenery.
                            float distance = offset.magnitude;
                            foreach (var hit in Physics.RaycastAll(center, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                            {
                                if (hit.collider.transform.IsChildOf(lightingPracticeTarget) ||
                                    (player != null && hit.collider.transform.IsChildOf(player.transform)) ||
                                    hit.collider.GetComponentInParent<FilmLightItem>() != null) continue;
                                distance = Mathf.Min(distance, Mathf.Max(.5f, hit.distance - .25f));
                            }
                            view.transform.SetPositionAndRotation(center + offset.normalized * distance, Quaternion.LookRotation(-offset));
                        }
                        if (compared && !keys.bKey.isPressed && keys.enterKey.wasPressedThisFrame) break;
                        if (keys.bKey.isPressed && !compared)
                        {
                            compared = true;
                            TutorialUIManager.Instance?.SetupTasks(new[] { prompts[role] + "\nRELEASE [B] TO RESTORE. [ENTER] NEXT." });
                        }
                        for (int i = 0; i < threePointPracticeLights.Count; i++)
                            if (threePointPracticeLights[i] != null && threePointPracticeLights[i].spotlight != null)
                                threePointPracticeLights[i].spotlight.enabled = i <= role && !(i == role && keys.bKey.isPressed);
                    }
                    yield return null;
                }
            }
        }
        finally
        {
            foreach (var entry in savedLights)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in savedRenderers)
                if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
            if (view != null)
            {
                view.transform.SetPositionAndRotation(savedPosition, savedRotation);
                view.fieldOfView = savedFov;
            }
            if (player != null) { player.canMove = couldMove; player.canLook = couldLook; }
            foreach (var light in threePointPracticeLights)
                if (light != null && light.spotlight != null) light.spotlight.enabled = light.IsPoweredOn();
        }
        TutorialUIManager.Instance?.HideTasks();
        BeginCameraMovementLesson(true);
    }

    private IEnumerator ObserveLightingSetup()
    {
        currentStep = Level3Step.ObserveSoftLight;
        isBriefingOpen = false;

        for (int secondsRemaining = 10; secondsRemaining > 0; secondsRemaining--)
        {
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[]
                {
                    "- Observe the wide, smooth highlight across the practice surface",
                    "- Compare the bright side with the controlled shadow side",
                    "- Notice how some shadow preserves shape and depth",
                    "- Next briefing in " + secondsRemaining + " seconds"
                });
            }

            yield return new WaitForSeconds(1f);
        }

        ShowLightingPracticeComplete();
    }

    private void ShowLightingPracticeComplete()
    {
        currentStep = Level3Step.PracticeComplete;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("See how the softer reflection reveals the shape without losing all the shadow? Keep your three Better Lights and use them for your commercial.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void FinishLightingPractice()
    {
        for (int i = 0; i < threePointPracticeLights.Count; i++)
        {
            practiceLight = threePointPracticeLights[i];
            ReturnPracticeLightToDeliveryZone(i);
        }
        practiceLight = null;
        foreach (var loan in loanLights) if (loan != null) Destroy(loan);
        loanLights.Clear();
        threePointPracticeLights.Clear();
        CleanUpLightingPractice();
        ShowAlmanacIntroduction();
    }

    private void ShowAlmanacIntroduction()
    {
        currentStep = Level3Step.IntroduceAlmanac;

        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockLevel3Equipment();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("I've added our Soft Light lesson to the Almanac. Press <color=red>[P]</color> after this and have a look at the controls and reflective-product guide.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void StartAlmanacReview()
    {
        if (AlmanacManager.Instance == null)
        {
            ShowContractIntroduction();
            return;
        }

        currentStep = Level3Step.OpenAlmanac;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Press <color=red>[P]</color> to open the Almanac",
                "- Read the new Level 3 lighting guides"
            });
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
    }

    private void ShowContractIntroduction()
    {
        // Present the offer first; the accepted callback owns the production briefing.
        OfferContract();
    }

    private void OfferContract()
    {
        currentStep = Level3Step.OfferContract;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review the Terrari contract",
                "- Select the contract, read the brief, then close it to continue"
            });
        }

        if (contractUIManager != null)
        {
            contractUIManager.ShowLevel3Contract(AcceptContract);
        }
        else
        {
            isBriefingOpen = true;
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.ShowBossDialogue("A new contract is ready. Your production advance is " + ProductionEconomy.Advance(3).ToString("N0") + " B-Coins. Press <color=red>[SPACE]</color> to accept, then we'll discuss the job.", TutorialUIManager.Instance.poseBoss, true, false);
            }
        }
    }

    private void AcceptContract()
    {
        if (CareerManager.Instance != null)
        {
            if (PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 0)
            {
                CareerManager.Instance.AcceptJob("Terrari", ProductionEconomy.Advance(3));
                PlayerPrefs.SetInt("LamborminiContractAccepted", 1);
                PlayerPrefs.Save();
            }
            else
            {
                CareerManager.Instance.currentActiveJob = "Terrari";
            }
        }

        if (contractUIManager != null) contractUIManager.UnlockQualifications();

        currentStep = Level3Step.ContractAccepted;
        if (DevTutorialBypass.Disabled) { currentStep = Level3Step.LevelActive; isBriefingOpen = false; return; }
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Contract accepted! <color=yellow>Terrari</color> wants a 25-second reveal. Place the orange car on a dark set. Record separate back, side and overall takes, about 7 seconds each. One SD card holds up to 60 seconds; eject it with <color=red>[C]</color> when ready to import. Use Better Lights at 75%, -10°, 3200K and 75% diffusion. Hold Ctrl for smooth camera movement. In editing, add the 2-second Terrari intro and 2-second outro. Press <color=red>[TAB]</color> to review the brief.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowCurrentLightingTasks()
    {
        if (practiceLesson != null || TutorialUIManager.Instance == null || practiceLight == null) return;

        string powerTask = practiceLight.IsPoweredOn() ? "<color=#55FF88>ON</color>" : "OFF";
        string intensityTask = Mathf.RoundToInt(practiceLight.intensityPercent) + "% / " + PracticeIntensity + "%";
        string tiltTask = Mathf.RoundToInt(practiceLight.GetCurrentTilt()) + " degrees / -10 degrees";
        string temperatureTask = Mathf.RoundToInt(practiceLight.GetColorTemperature()) + "K / 3200K";
        string diffusionTask = Mathf.RoundToInt(practiceLight.GetDiffusionPercent()) + "% / " + PracticeDiffusion + "%";

        TutorialUIManager.Instance.SetupTasks(new string[]
        {
            "- Power: " + powerTask + "  |  Intensity: " + intensityTask,
            "- Set tilt: " + tiltTask,
            "- Temperature: " + temperatureTask + "  |  Diffusion: " + diffusionTask,
            "- Stand on the SOFT KEY marker and press <color=red>[G]</color>"
        });
    }

    private void ConfigurePracticeLight(FilmLightItem light)
    {
        if (light == null || light.spotlight == null || lightingPracticeTarget == null) return;

        Light practiceSpotlight = light.spotlight;

        practiceSpotlight.range = Mathf.Max(40f, light.advancedRange);
        practiceSpotlight.spotAngle = 38f;
        practiceSpotlight.innerSpotAngle = 30f;
        practiceSpotlight.shadows = LightShadows.Soft;
        practiceSpotlight.shadowStrength = 0.55f;
        practiceSpotlight.shadowBias = 0.08f;
        practiceSpotlight.shadowNormalBias = 0.25f;
        practiceSpotlight.shadowNearPlane = 0.2f;
        light.RefreshAdvancedFeatures();
        // Dropping must retain the player's previewed head position and aim.
    }

    private void ReturnPracticeLightToDeliveryZone(int slot = 0)
    {
        if (practiceLight == null) return;

        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();
        if (shopTerminal == null || shopTerminal.deliveryZone == null)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The Soft Light could not be returned because the delivery zone is missing.");
            return;
        }

        Transform deliveryZone = shopTerminal.deliveryZone;
        practiceLight.ReturnToDelivery(deliveryZone.position + deliveryZone.right * ((slot - 1) * .85f)
            + Vector3.up * .65f, deliveryZone.rotation);

        practiceLight = null;
    }

    private void CreateLightingPractice()
    {
        if (lightingPracticeRoot != null) return;

        Renderer stageRenderer = FindStageRenderer();
        if (stageRenderer == null)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The raised Stage could not be found for the Soft Light practice.");
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

        // Move the whole rig forward and keep the rear marker away from the curved backdrop.
        Vector3 targetPosition = stageCenter + stageFront * Mathf.Min(1f, stageFrontExtent * .18f);
        practiceFront = stageFront;
        practiceRight = stageRight;
        // Widen the triangle without pushing the rear light into the backdrop.
        practiceFrontDistance = Mathf.Min(3.2f, stageFrontExtent * .48f);
        practiceSideDistance = Mathf.Min(3.8f, stageSideExtent * .60f);
        Vector3 lightPosition = targetPosition + stageFront * practiceFrontDistance - stageRight * practiceSideDistance;

        targetPosition = ClampPracticePointToStage(targetPosition, stageBounds);
        lightPosition = ClampPracticePointToStage(lightPosition, stageBounds);

        lightingPracticeRoot = new GameObject("Better Lights Practice");
        lightingPracticeDirector = FindObjectOfType<DirectorTerminal>();
        if (lightingPracticeDirector != null)
        {
            lightingPracticeWall = lightingPracticeDirector.CreatePracticeWall(new Color(0.17f, 0.18f, 0.21f, 1f));
        }

        lightingPracticeTarget = CreatePracticeTarget(targetPosition);
        softLightPlacementMarker = CreatePlacementMarker(lightPosition);
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
        GameObject targetRoot = new GameObject("Reflective Practice Target");
        targetRoot.transform.SetParent(lightingPracticeRoot.transform);
        targetRoot.transform.position = targetPosition;

        GameObject targetBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetBody.name = "Reflective Practice Surface";
        targetBody.transform.SetParent(targetRoot.transform);
        targetBody.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        targetBody.transform.localScale = new Vector3(1.8f, 1.4f, 0.75f);

        Collider targetCollider = targetBody.GetComponent<Collider>();
        if (targetCollider != null) targetCollider.isTrigger = true;

        Renderer targetRenderer = targetBody.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targetRenderer.material.color = new Color(0.55f, 0.06f, 0.06f, 1f);
            if (targetRenderer.material.HasProperty("_Metallic")) targetRenderer.material.SetFloat("_Metallic", 0.65f);
            if (targetRenderer.material.HasProperty("_Smoothness")) targetRenderer.material.SetFloat("_Smoothness", 0.8f);
        }

        CreatePracticeLabel(targetRoot.transform, new Vector3(0f, 1.8f, 0f), "REFLECTIVE\nPRACTICE SURFACE", Color.white);
        return targetRoot.transform;
    }

    private Transform CreatePlacementMarker(Vector3 markerPosition)
    {
        GameObject markerRoot = new GameObject("Soft Key Light Marker");
        markerRoot.transform.SetParent(lightingPracticeRoot.transform);
        markerRoot.transform.position = markerPosition;

        GameObject markerDisc = GuidedPracticeLesson.CreateGreenMarker(markerRoot.transform);
        markerDisc.name = "Placement Point";
        markerDisc.transform.SetParent(markerRoot.transform);
        markerDisc.transform.localPosition = Vector3.up * .12f;
        // Marker size and floor clearance match the first tutorial.

        Collider markerCollider = markerDisc.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);

        Color markerColor = Color.green;
        Renderer markerRenderer = markerDisc.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material.color = markerColor;
            markerRenderer.material.EnableKeyword("_EMISSION");
            markerRenderer.material.SetColor("_EmissionColor", markerColor * 0.65f);
        }

        CreatePracticeLabel(markerRoot.transform, new Vector3(0f, 0.38f, 0f), "SOFT KEY\n75% | -10 DEGREES", markerColor);
        return markerRoot.transform;
    }

    private void CreatePracticeLabel(Transform labelParent, Vector3 localPosition, string labelText, Color labelColor)
    {
        GameObject labelObject = new GameObject("Practice Label");
        labelObject.transform.SetParent(labelParent);
        labelObject.transform.localPosition = localPosition;

        TextMeshPro markerLabel = labelObject.AddComponent<TextMeshPro>();
        markerLabel.text = labelText;
        markerLabel.fontSize = 2.5f;
        markerLabel.alignment = TextAlignmentOptions.Center;
        markerLabel.color = labelColor;
        markerLabel.rectTransform.sizeDelta = new Vector2(6f, 1.4f);
        practiceMarkerLabels.Add(markerLabel);
    }

    private void CleanUpLightingPractice()
    {
        foreach (var loan in loanLights) if (loan != null) Destroy(loan);
        loanLights.Clear();
        if (lightingPracticeRoot != null) Destroy(lightingPracticeRoot);
        if (lightingPracticeDirector != null && lightingPracticeWall != null) lightingPracticeDirector.RemovePracticeWall(lightingPracticeWall);

        lightingPracticeRoot = null;
        lightingPracticeWall = null;
        lightingPracticeDirector = null;
        lightingPracticeTarget = null;
        softLightPlacementMarker = null;
        practiceMarkerLabels.Clear();
    }


    private void ShowLevelTasks()
    {
        if(practiceLesson!=null){practiceLesson.ShowCurrentTask();return;}
        if (TutorialUIManager.Instance == null) return;
        if(!rimLessonStarted && PlayerPrefs.GetInt("Level3CameraMovementLessonComplete",0)==0)
        {
            TutorialUIManager.Instance.SetupTasks(new[]{"Place the car and backdrop; close the tablet", "Set down the warm Soft Light, power it ON and aim at the car", "Use at least 30% output and 50% diffusion; Ctrl camera lesson follows"});
            return;
        }
        TutorialUIManager.Instance.HideTasks();
    }
}



 // Small action-by-action lesson shared by the existing level managers.
internal sealed class GuidedPracticeLesson
{
    internal sealed class Step
    {
        public string message;
        public string task;
        public System.Func<bool> done;
        public System.Action guide;
        public string permission;
        public Step(string message, string task, System.Func<bool> done, System.Action guide = null, string permission = null)
        { this.message = message; this.task = task; this.done = done; this.guide = guide; this.permission = permission; }
    }

    private readonly TutorialManager tutorial;
    private readonly List<Step> steps;
    private readonly System.Action complete;
    private int index;
    private float stableSince = -1f;
    private readonly Transform station;
    private readonly bool lockMovementAtStation;
    private Player.PlayerController.PlayerController stationPlayer;
    private LineRenderer guideLine;
    private bool stationLocked;
    private bool released;
    public bool IsExplaining { get; private set; }
    public string CurrentPermission => !released && index < steps.Count ? steps[index].permission : null;
    public bool ReadyToPlace => index == steps.Count - 1 && !IsExplaining;

    public GuidedPracticeLesson(TutorialManager tutorial, List<Step> steps, System.Action complete = null, Transform station = null, bool lockMovementAtStation = true)
    {
        this.tutorial = tutorial;
        this.steps = steps;
        this.complete = complete;
        this.station = station;
        this.lockMovementAtStation = lockMovementAtStation;
        stationPlayer = Object.FindObjectOfType<Player.PlayerController.PlayerController>();
        if (station != null && tutorial != null && tutorial.objectiveLine != null)
        {
            GameObject lineObject = new GameObject("Practice Objective Line");
            lineObject.transform.SetParent(station, false);
            guideLine = lineObject.AddComponent<LineRenderer>();
            guideLine.sharedMaterial = tutorial.objectiveLine.sharedMaterial;
            guideLine.widthCurve = tutorial.objectiveLine.widthCurve;
            guideLine.widthMultiplier = tutorial.objectiveLine.widthMultiplier;
            guideLine.colorGradient = tutorial.objectiveLine.colorGradient;
            guideLine.useWorldSpace = true; guideLine.positionCount = 2;
            guideLine.enabled = false;
        }
        Explain();
    }

    private void Explain()
    {
        IsExplaining = true;
        stableSince = -1f;
        if (tutorial != null) tutorial.FreezePlayerMovement();
        var ui = TutorialUIManager.Instance;
        if (ui != null) ui.ShowBossDialogue(steps[index].message, ui.posePoint, true, false);
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
    }

    public void Continue()
    {
        var ui = TutorialUIManager.Instance;
        if (!IsExplaining || (ui != null && !ui.CanAdvanceBossDialogue())) return;
        IsExplaining = false;
        if (ui != null)
        {
            ui.HideBossDialogue();
            ui.SetupTasks(new[] { steps[index].task });
        }
        if (tutorial != null) tutorial.UnfreezePlayerMovement();
        if (stationLocked && stationPlayer != null) stationPlayer.canMove = false;
    }

    public void ShowCurrentTask()
    {
        if (!released && !IsExplaining && index < steps.Count)
            TutorialUIManager.Instance?.SetupTasks(new[] { steps[index].task });
    }

    public void Tick()
    {
        if (released) return;
        if (stationLocked && stationPlayer != null) stationPlayer.canMove = false;
        if (guideLine != null && stationPlayer != null && station != null)
        {
            guideLine.enabled = !stationLocked && !IsExplaining;
            if (guideLine.enabled) ProductionGuideLine.Draw(guideLine, stationPlayer.transform, station, stationPlayer.GameplayCamera);
        }
        if (IsExplaining || index >= steps.Count || PauseManager.isPaused) return;
        if (steps[index].guide != null) steps[index].guide();
        else CampaignGuidance.HighlightPracticeControl(steps[index].task);
        if (steps[index].done == null || !steps[index].done())
        { stableSince = -1f; return; }
        if (stableSince < 0f) stableSince = Time.time;
        if (!DevTutorialBypass.PracticeDelayComplete(Time.time - stableSince, 0.5f)) return;
        if (index == 0 && station != null && stationPlayer != null)
        {
            if (lockMovementAtStation && !stationPlayer.TryLockToPracticePoint(station))
            { stableSince = -1f; return; }
            stationLocked = lockMovementAtStation;
            if (guideLine != null) guideLine.enabled = false;
            foreach (Renderer visual in station.GetComponentsInChildren<Renderer>())
                if (!(visual is LineRenderer)) visual.enabled = false;
        }
        index++;
        if (index < steps.Count) Explain();
        else { Release(); complete?.Invoke(); }
    }

    public void Release()
    {
        if (released) return;
        released = true;
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
        if (stationLocked && stationPlayer != null)
        {
            stationPlayer.ReleasePracticePoint();
            stationPlayer.canMove = true;
        }
        stationLocked = false;
        if (guideLine != null) Object.Destroy(guideLine.gameObject);
    }

    public static bool AtMarker(Transform marker)
    {
        return IsPlayerAtMarker(marker);
    }

    public static Vector3 GuideEndpoint(Transform target, bool floor)
    {
        if (target == null) return Vector3.zero;
        Vector3 point = target.position;
        if (!floor)
        {
            Collider collider = target.GetComponentInChildren<Collider>();
            if (collider != null && collider.enabled) return collider.bounds.center;
            Renderer mesh = target.GetComponentInChildren<MeshRenderer>();
            if (mesh != null) return mesh.bounds.center;
        }
        Vector3 grounded = point;
        float closest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * .2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
            if (hit.normal.y < .5f) continue;
            if (hit.point.y <= point.y + .1f && hit.point.y > point.y - 4f && hit.distance < closest)
            { closest = hit.distance; grounded = hit.point; }
        }
        return grounded + Vector3.up * .035f;
    }

    private static bool IsPlayerAtMarker(Transform marker)
    {
        var player = Object.FindObjectOfType<Player.PlayerController.PlayerController>();
        return player != null && PracticePointLock.IsAtCenter(player.transform, marker);
    }

    public static GameObject CreateGreenMarker(Transform parent)
    {
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        GameObject source = tutorial != null ? tutorial.stageWalkTriggerCircle : null;
        GameObject visual;
        var sourceMesh = source != null ? source.GetComponentInChildren<MeshFilter>(true) : null;
        if (sourceMesh != null && sourceMesh.sharedMesh != null)
        {
            // Duplicate the original A/B/C circle's visual, without copying hidden
            // renderer flags, tutorial scripts, or its old label.
            visual = new GameObject("Practice Circle", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            visual.GetComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
            var sourceRenderer = sourceMesh.GetComponent<MeshRenderer>();
            if (sourceRenderer != null)
                visual.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
            visual.transform.rotation = sourceMesh.transform.rotation;
            Vector3 size = sourceMesh.transform.lossyScale;
            Vector3 parentSize = parent.lossyScale;
            visual.transform.localScale = new Vector3(size.x / Mathf.Max(.001f, Mathf.Abs(parentSize.x)),
                size.y / Mathf.Max(.001f, Mathf.Abs(parentSize.y)), size.z / Mathf.Max(.001f, Mathf.Abs(parentSize.z)));
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = new Vector3(1.4f, .02f, 1.4f);
        }
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);
        Renderer renderer = visual.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return visual;
    }


    public static GuidedPracticeLesson Light(TutorialManager tutorial, Transform marker,
        System.Func<FilmLightItem> heldLight, string role, float intensity, bool advanced, Transform subject = null)
    {
        var steps = new List<Step>
        {
            new Step("Bring your light to the center of the green " + role + " circle. We'll work through the controls once you're standing there.",
                "Equip the light and walk to the center of the green " + role + " circle",
                () => heldLight() != null && AtMarker(marker)),
            new Step("Good spot. Press <color=red>[Left Click]</color> to switch the light on. Watch where the light falls.",
                "[Left Click] Turn the light ON",
                () => heldLight() != null && heldLight().IsPoweredOn()),
            new Step("Use <color=red>[Scroll]</color> to set brightness to " + intensity + "%. " +
                (role.Contains("Fill") ? "A weaker fill keeps some shadow so the product still has shape." :
                role.Contains("Back") ? "This light catches the edge and separates the product from the background." :
                "This is our main light; it gives the subject its shape. These percentages are practice starting points, not universal settings. Greater distance reduces illumination; judge the subject through the camera."),
                "[Scroll] Set brightness to " + intensity + "%",
                () => heldLight() != null && Mathf.Abs(heldLight().intensityPercent - intensity) <= 2.5f)
        };
        float practiceHeight = role.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0 ? .8f :
            role.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0 ? .25f : .5f;
        if (advanced && role.IndexOf("Key", System.StringComparison.OrdinalIgnoreCase) >= 0) practiceHeight = .75f;
        float? startingHeight = null;
        bool adjustedHeight = false;
        steps.Add(new Step("Lights start at +0.50 m extension. The HEIGHT indicator shows the current extension above the original stand. Hold <color=red>[Q]</color> to raise it or <color=red>[E]</color> to lower it, down to +0.00 m. For this " + role + " demonstration set the extension to +" + practiceHeight.ToString("F2") + " m. " + (advanced ? "Try the height controls yourself; if it already matches, move it away and back. " : "If it already matches, keep it there. ") + "The head and beam move together. A high Key shapes the subject, a lower Fill opens shadows, and a raised Back light outlines the edge.",
            "[Q up / E down] Set height extension to +" + practiceHeight.ToString("F2") + " m",
            () =>
            {
                var light = heldLight();
                if (light == null) return false;
                if (!startingHeight.HasValue) startingHeight = light.HeightExtension;
                if (Mathf.Abs(light.HeightExtension - startingHeight.Value) > .02f) adjustedHeight = true;
                return (!advanced || adjustedHeight) && Mathf.Abs(light.HeightExtension - practiceHeight) <= .08f;
            }));
        if (!advanced)
            steps.Add(new Step("After changing height, use <color=red>[Up/Down]</color> to tilt the head. Try -10 degrees. Raising the stand does not automatically aim it. After placement we aim this demonstration light at the practice subject; on your commercial, check the beam yourself.",
                "[Up/Down] Try a -10 degree tilt",
                () => heldLight() != null && Mathf.Abs(heldLight().GetCurrentTilt() + 10f) <= 2.5f));
        if (advanced)
        {
            float diffusion = role.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 100f :
                role.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 25f : 75f;
            steps.Add(new Step("Negative tilt points down; positive tilt points up. Press Down Arrow to tilt down to -10 degrees with <color=red>[Up/Down]</color>. Aim the light onto the subject.",
                "[Up/Down] Set tilt to -10 degrees",
                () => heldLight() != null && Mathf.Abs(heldLight().GetCurrentTilt() + 10f) <= 2.5f));
            steps.Add(new Step("Use <color=red>[Z/X]</color> to set 3200K. We match all three lights for this warm practice look, not because three-point lighting requires 3200K. Your camera's white balance also affects the result.",
                "[Z/X] Set temperature to 3200K",
                () => heldLight() != null && Mathf.Abs(heldLight().GetColorTemperature() - 3200f) <= 250f));
            string diffusionReason = diffusion == 100f ? "A softer Fill gently opens the shadows." :
                diffusion == 25f ? "Less diffusion gives our Back light a more defined edge." : "A soft Key gives a broad reflection while keeping shape.";
            steps.Add(new Step("Use <color=red>[V/B]</color> to set diffusion to " + diffusion + "%. " + diffusionReason + " These are game practice settings, not universal lighting rules.",
                "[V/B] Set diffusion to " + diffusion + "%",
                () => heldLight() != null && Mathf.Abs(heldLight().GetDiffusionPercent() - diffusion) <= 2.5f));
        }
        if (advanced && subject != null)
            steps.Add(new Step("Turn toward the practice subject. Watch the beam land on it. A light only helps when it reaches the subject; the stand position alone is not enough.",
                "Aim the light at the practice subject",
                () =>
                {
                    var light = heldLight();
                    if (light == null || light.spotlight == null || subject == null) return false;
                    Vector3 direction = subject.position + Vector3.up * .7f - light.spotlight.transform.position;
                    return direction.magnitude <= light.spotlight.range && Vector3.Angle(light.spotlight.transform.forward, direction) <= light.spotlight.spotAngle * .3f;
                }));
        steps.Add(new Step("Everything is set. Stand on the " + role + " circle and press <color=red>[G]</color> to place the light.",
            "Stand on the " + role + " circle, then [G] place the light", null));
        return new GuidedPracticeLesson(tutorial, steps, null, marker);
    }
}

internal static class CampaignGuidance
{
    private static float nextControlRefresh;
    public static void HighlightPracticeControl(string task)
    {
        if (Time.unscaledTime < nextControlRefresh || TutorialHighlighter.Instance == null) return;
        nextControlRefresh = Time.unscaledTime + .2f;
        DirectorTerminal director = Object.FindObjectOfType<DirectorTerminal>();
        if (director == null || !director.IsTerminalActive()) return;
        string caption = task.Contains("POSE ACTOR") ? "POSE ACTOR" : task.Contains("Actor card") ? "ACTOR" : null;
        if (caption == null) return;
        foreach (TMP_Text text in director.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!string.Equals(text.text.Trim(), caption, System.StringComparison.OrdinalIgnoreCase)) continue;
            var button = text.GetComponentInParent<UnityEngine.UI.Button>();
            RectTransform rect = button != null ? button.transform as RectTransform : text.transform.parent as RectTransform;
            if (rect != null) TutorialHighlighter.Instance.HighlightElement(rect);
            return;
        }
    }
    private static RectTransform highlighted;
    private static Transform pickup;
    private static string previousStep;
    private static float nextRefresh;
    private static bool ownsLine;

    public static void Update(TutorialManager tutorial, string step, int level)
    {
        if (tutorial == null || PauseManager.isPaused) return;
        // The Almanac owns the shared spotlight throughout its navigation lesson.
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsNavigationLessonActive)
        {
            highlighted = null;
            previousStep = null;
            return;
        }
        if (previousStep == step && Time.unscaledTime < nextRefresh) return;
        bool changed = previousStep != step;
        previousStep = step;
        nextRefresh = Time.unscaledTime + .2f;
        RectTransform target = null;
        bool wantsShop = step == "BuyCamera" || step == "BuySDCard" || step == "BuyLights" || step == "BuyLight" || step == "Checkout" || step == "LightCheckout";
        if (wantsShop)
        {
            ShopTerminal shop = Object.FindObjectOfType<ShopTerminal>();
            if (shop != null && shop.IsTerminalActive())
            {
                if (step == "Checkout" || step == "LightCheckout") target = tutorial.shopCheckoutBtnRect;
                else target = shop.GetTutorialCartTarget(step == "BuyCamera" ? "LEVEL 2 CAMERA" :
                    step == "BuySDCard" ? "SD CARD" : level == 3 ? "LEVEL 3 SOFT LIGHT" : "160 LED PANEL");
                if (ownsLine) { tutorial.PointLineAtTransform(null); ownsLine = false; }
            }
            else { tutorial.PointLineAt("shop"); ownsLine = true; }
        }
        else if (step == "PickUpCamera" || step == "PickUpSDCard" || step == "PickUpLights" || step == "PickUpLight")
        {
            if (changed || pickup == null || !pickup.gameObject.activeInHierarchy ||
                pickup.GetComponentInParent<Player.PlayerController.PlayerController>() != null)
            {
                pickup = null;
                float best = float.MaxValue;
                var player = Object.FindObjectOfType<Player.PlayerController.PlayerController>();
                foreach (Player.Equipment.Equipment item in Object.FindObjectsOfType<Player.Equipment.Equipment>())
                {
                    if (item.GetComponentInParent<Player.PlayerController.PlayerController>() != null) continue;
                    bool matches = step == "PickUpCamera" ? item is Player.Equipment.FilmCameraItem :
                        step == "PickUpSDCard" ? item is Player.Equipment.SDCardItem card && card.HasSpace :
                        item is Player.Equipment.FilmLightItem;
                    if (!matches) continue;
                    if (level == 3 && (!(item is Player.Equipment.FilmLightItem softLight) || !softLight.HasAdvancedFeatures())) continue;
                    float distance = player != null ? Vector3.Distance(player.transform.position, item.transform.position) : 0;
                    if (distance < best) { pickup = item.transform; best = distance; }
                }
            }
            tutorial.PointLineAtTransform(pickup); ownsLine = true;
        }
        else
        {
            if (ownsLine) { tutorial.PointLineAtTransform(null); ownsLine = false; }
            if (step == "OfferContract") target = tutorial.acceptContractButtonRect;
            AlmanacManager book = AlmanacManager.Instance;
            if (book != null && book.IsOpen())
            {
                if (step == "CloseAlmanac" || step == "CloseEquipmentAlmanac" || step == "CloseTechniquesAlmanac" || step == "ReviewAlmanac")
                    target = book.knowledgeTabBtn != null && book.knowledgeTabBtn.interactable ? book.knowledgeTabBtn.transform as RectTransform : null;
            }
        }
        if (target != null && !target.gameObject.activeInHierarchy) target = null;
        if (highlighted == target) return;
        if (TutorialHighlighter.Instance != null)
        {
            if (target != null) TutorialHighlighter.Instance.HighlightElement(target);
            else if (highlighted != null) TutorialHighlighter.Instance.HideHighlight();
        }
        highlighted = target;
    }
}
