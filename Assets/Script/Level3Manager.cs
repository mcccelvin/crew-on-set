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
        rimLessonStarted=true;
        var inventory = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        Vector3 previousPosition = Vector3.zero;
        bool trackingMovement = false;
        float practiceSeconds = 0f;
        practiceLesson=new GuidedPracticeLesson(tutorialManager,new List<GuidedPracticeLesson.Step>
        {
            new GuidedPracticeLesson.Step("Your Soft Light is placed at 3200K for a warm look. Now pick up your camera with <color=yellow>[E]</color> and select its hotbar slot. If it is already in your inventory, just equip it. We'll practice smooth movement before recording.","Pick up and equip your camera",()=>inventory != null && inventory.GetHeldItem() is FilmCameraItem),
            new GuidedPracticeLesson.Step("Next, pick up a blank SD card with <color=yellow>[E]</color>. It stores one recording. If you have none, buy an SD card from the shop and collect it from delivery. Keep it in your hotbar for now; this movement rehearsal does not need a recording.","Collect a blank SD card",()=>inventory != null && inventory.HasBlankSDCard()),
            new GuidedPracticeLesson.Step("Select your camera and click <color=yellow>Left Mouse Button</color> to open the viewfinder. Look through it to frame the lit subject before practicing movement.","Equip camera and click LMB to open the viewfinder",()=>inventory != null && inventory.GetHeldItem() is FilmCameraItem camera && camera.IsCameraViewActive()),
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
            new GuidedPracticeLesson.Step("Use three SD cards for three different views: back, side and overall. Record about 7 seconds for each view. For a moving take, hold Ctrl before pressing WASD, keep the car framed, release WASD first, let the camera settle, then stop recording. The editor provides a 2-second Terrari intro and 2-second outro, making a 25-second commercial.","Continue to finish the camera-movement lesson",()=>true)
        },()=>
        {
            practiceLesson=null;
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
        if (currentStep == Level3Step.BuyLight)
        {
            if (itemIndex != level3LightItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Let's buy the Level 3 Soft Light first. That's the tool we're trying today.");
                return false;
            }

            currentStep = Level3Step.LightCheckout;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Confirm the Level 3 Soft Light purchase" });
            }

            return true;
        }

        if (currentStep == Level3Step.LightCheckout)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("You've got the Soft Light in your cart. Click CONFIRM to order it.");
            return false;
        }

        return true;
    }

    public bool CanConfirmPurchase()
    {
        if (currentStep == Level3Step.LightCheckout) return true;

        if (currentStep == Level3Step.BuyLight)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("We're still missing the Level 3 Soft Light. Add it to the cart first.");
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
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Open LIGHTS", "- Add the Level 3 Soft Light to your cart" });
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

        if (light.EquipmentName != "Level 3 Soft Light")
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Grab the Level 3 Soft Light from delivery with [E]. We'll try it on the stage.");
            return;
        }

        practiceLight = light;
        ShowLightingPracticeIntroduction();
    }

    public bool CanPickUpLight(FilmLightItem light)
    {
        if (currentStep != Level3Step.ObserveSoftLight || light == null || light != practiceLight) return true;

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
        if (currentStep != Level3Step.PlaceSoftLight || light == null || light != practiceLight || softLightPlacementMarker == null) return;

        if (practiceLesson != null && (!practiceLesson.ReadyToPlace || !GuidedPracticeLesson.AtMarker(softLightPlacementMarker)))
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Pick the light up with [E] and finish the current step on the circle first.");
            return;
        }

        // Use the same player-position check as entry into the guided lesson.
        bool isNearMarker = GuidedPracticeLesson.AtMarker(softLightPlacementMarker);
        bool hasCorrectIntensity = Mathf.Abs(light.intensityPercent - 75f) <= 2.5f;
        bool hasCorrectTilt = Mathf.Abs(light.GetCurrentTilt() + 10f) <= 2.5f;
        bool hasCorrectTemperature = Mathf.Abs(light.GetColorTemperature() - 3200f) <= 250f;
        bool hasCorrectDiffusion = Mathf.Abs(light.GetDiffusionPercent() - 75f) <= 2.5f;

        if (!light.IsPoweredOn() || !hasCorrectIntensity || !hasCorrectTilt || !hasCorrectTemperature || !hasCorrectDiffusion || !isNearMarker)
        {
            if (tutorialManager != null)
            {
                string correction = !light.IsPoweredOn()
                    ? "Turn the Soft Light ON with Left Mouse Button."
                    : !hasCorrectIntensity
                        ? "Set the Soft Light to 75% with the mouse wheel."
                        : !hasCorrectTilt
                            ? "Set the tilt to -10 degrees with the arrow keys."
                            : !hasCorrectTemperature
                                ? "Set color temperature to 3200K with Z and X."
                                : !hasCorrectDiffusion
                                    ? "Set diffusion to 75% with V and B."
                                    : "Stand on the SOFT KEY marker before pressing G.";
                tutorialManager.ShowWarning("Let's adjust that a little. Pick the light up again. " + correction);
            }
            return;
        }

        practiceLesson?.Release();
        practiceLesson = null;
        light.PlaceOnSurface(softLightPlacementMarker.position);
        ConfigurePracticeLight(light);

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
        currentStep = Level3Step.ObserveSoftLight;
        isBriefingOpen = false;
        BeginCameraMovementLesson(true);
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
                "- Review the Level 3 Soft Light guide",
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
            TutorialUIManager.Instance.ShowBossDialogue("For <color=yellow>Level 3</color>, create an orange supercar reveal in a dark showroom. Show three views: the back, the side, and an overall view with the full car visible. Soft highlights describe the curves; the dark background separates the orange paint.", TutorialUIManager.Instance.poseBoss, true, false);
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

        GameObject level2CameraPrefab = Resources.Load<GameObject>("Prefabs/Level 2 Camera Placeholder");
        if (level2CameraPrefab != null) shopTerminal.RestoreLevel2Camera(level2CameraPrefab);

        bool usePlaceholder = shopTerminal.level3LightPrefab == null;
        GameObject lightPrefab = usePlaceholder ? shopTerminal.availableItems[1].prefabToSpawn : shopTerminal.level3LightPrefab;
        if (lightPrefab == null) return;

        if (!requiresLightPurchase && PlayerPrefs.GetInt("Level3LightPurchased", 0) == 1)
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
            TutorialUIManager.Instance.ShowBossDialogue("Our new tool is the <color=yellow>Level 3 Soft Light</color>. We will use it warm at about 3200K so the Terrari paint feels rich. Output changes brightness, color temperature changes warmth, and diffusion softens reflections. While holding the light, hold Q to raise or E to lower its real stand, then G to place it. The setup-only beam guide never appears in recordings. After lighting the car, I will teach you to hold Ctrl for smooth camera movement. Open the equipment shop with <color=red>[E]</color>.", TutorialUIManager.Instance.poseHappy, true, false);
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
            if (tutorialManager != null) tutorialManager.ShowWarning("The Level 3 Soft Light is missing from the Equipment Shop.");
            ShowLightPickupIntroduction();
            return;
        }

        currentStep = Level3Step.BuyLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Open the Equipment Shop", "- Buy the Level 3 Soft Light" });
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
        currentStep = Level3Step.PickUpLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the Level 3 Soft Light from the delivery table" });
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
            TutorialUIManager.Instance.ShowBossDialogue("Let's try the Soft Light on our practice set. I'll walk you through each control, one at a time.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartSoftLightPractice()
    {
        currentStep = Level3Step.PlaceSoftLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        practiceLesson = GuidedPracticeLesson.Light(tutorialManager, softLightPlacementMarker,
            () => practiceLight != null && practiceLight.gameObject.activeInHierarchy && practiceLight.GetComponentInParent<Player.PlayerController.PlayerController>() != null ? practiceLight : null,
            "Soft Key", 75f, true);
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
            TutorialUIManager.Instance.ShowBossDialogue("See how the reflection spreads across the surface? The softer edge reveals the shape without losing all the shadow. I'll send the light back to delivery now.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void FinishLightingPractice()
    {
        ReturnPracticeLightToDeliveryZone();
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
        currentStep = Level3Step.IntroduceContract;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("<color=yellow>Terrari</color> wants a 25-second reveal. Record three separate takes on three SD cards: back, side and overall, about 7 seconds each. The editor provides a 2-second Terrari intro and 2-second outro. Build a dark set, place the orange car, and use the warm Soft Light to reveal its shape. Use the Ctrl camera movement we practiced for steady moving shots.", TutorialUIManager.Instance.poseBoss, true, false);
        }
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
                TutorialUIManager.Instance.ShowBossDialogue("Use the warm Soft Light we practiced with and your existing camera. Hold Ctrl for smooth camera movement when filming. Your production advance is 8,500 B-Coins. Press <color=red>[SPACE]</color> to accept.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("We've got the job. Place the car with the tablet and try our warm Soft Light settings: 75%, -10°, 3200K, and 75% diffusion. The brief's on <color=red>[TAB]</color>.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowCurrentLightingTasks()
    {
        if (practiceLesson != null || TutorialUIManager.Instance == null || practiceLight == null) return;

        string powerTask = practiceLight.IsPoweredOn() ? "<color=#55FF88>ON</color>" : "OFF";
        string intensityTask = Mathf.RoundToInt(practiceLight.intensityPercent) + "% / 75%";
        string tiltTask = Mathf.RoundToInt(practiceLight.GetCurrentTilt()) + " degrees / -10 degrees";
        string temperatureTask = Mathf.RoundToInt(practiceLight.GetColorTemperature()) + "K / 3200K";
        string diffusionTask = Mathf.RoundToInt(practiceLight.GetDiffusionPercent()) + "% / 75%";

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
        light.AimAt(lightingPracticeTarget.position + Vector3.up * 0.8f);
    }

    private void ReturnPracticeLightToDeliveryZone()
    {
        if (practiceLight == null) return;

        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();
        if (shopTerminal == null || shopTerminal.deliveryZone == null)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("The Soft Light could not be returned because the delivery zone is missing.");
            return;
        }

        if (practiceLight.IsPoweredOn()) practiceLight.OnUse(Camera.main);

        Transform deliveryZone = shopTerminal.deliveryZone;
        practiceLight.transform.position = deliveryZone.position + deliveryZone.right * 0.35f + Vector3.up * 0.65f;
        practiceLight.transform.rotation = deliveryZone.rotation;

        Rigidbody[] lightBodies = practiceLight.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody lightBody in lightBodies)
        {
            if (lightBody == null) continue;
            lightBody.velocity = Vector3.zero;
            lightBody.angularVelocity = Vector3.zero;
            lightBody.isKinematic = false;
            lightBody.useGravity = true;
        }

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

        Vector3 targetPosition = stageCenter - stageFront * Mathf.Min(0.8f, stageFrontExtent * 0.12f);
        Vector3 lightPosition = targetPosition + stageFront * Mathf.Min(4.2f, stageFrontExtent * 0.68f) - stageRight * Mathf.Min(3.2f, stageSideExtent * 0.42f);

        targetPosition = ClampPracticePointToStage(targetPosition, stageBounds);
        lightPosition = ClampPracticePointToStage(lightPosition, stageBounds);

        lightingPracticeRoot = new GameObject("Level 3 Soft Light Practice");
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
        markerDisc.transform.localPosition = Vector3.zero;
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
    private Player.PlayerController.PlayerController stationPlayer;
    private LineRenderer guideLine;
    private bool stationLocked;
    private bool released;
    public bool IsExplaining { get; private set; }
    public string CurrentPermission => !released && index < steps.Count ? steps[index].permission : null;
    public bool ReadyToPlace => index == steps.Count - 1 && !IsExplaining;

    public GuidedPracticeLesson(TutorialManager tutorial, List<Step> steps, System.Action complete = null, Transform station = null)
    {
        this.tutorial = tutorial;
        this.steps = steps;
        this.complete = complete;
        this.station = station;
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
            float height = tutorial != null ? tutorial.lineHeightOffset : .5f;
            guideLine.SetPosition(0, GuideEndpoint(stationPlayer.transform, true));
            guideLine.SetPosition(1, GuideEndpoint(station, true));
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
            stationLocked = true;
            stationPlayer.canMove = false;
            CharacterController controller = stationPlayer.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            Vector3 position = stationPlayer.transform.position;
            stationPlayer.transform.position = new Vector3(station.position.x, position.y, station.position.z);
            if (wasEnabled) controller.enabled = true;
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
        if (stationLocked && stationPlayer != null) stationPlayer.canMove = true;
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
        if (marker == null || player == null) return false;
        Vector3 offset = player.transform.position - marker.position;
        return new Vector2(offset.x, offset.z).magnitude <= 1.25f;
    }

    public static GameObject CreateGreenMarker(Transform parent)
    {
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        GameObject source = tutorial != null ? tutorial.stageWalkTriggerCircle : null;
        GameObject visual;
        if (source != null)
        {
            visual = Object.Instantiate(source, parent);
            visual.transform.rotation = source.transform.rotation;
            visual.transform.localScale = source.transform.lossyScale;
            visual.SetActive(true);
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
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return visual;
    }


    public static GuidedPracticeLesson Light(TutorialManager tutorial, Transform marker,
        System.Func<FilmLightItem> heldLight, string role, float intensity, bool advanced)
    {
        var steps = new List<Step>
        {
            new Step("Bring your light to the green " + role + " circle. We'll work through the controls once you're standing there.",
                "Equip the light and walk onto the green " + role + " circle",
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
        if (advanced) practiceHeight = .75f;
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
            steps.Add(new Step("Negative tilt points down; positive tilt points up. Press Down Arrow to tilt down to -10 degrees with <color=red>[Up/Down]</color>. Aim the light onto the subject.",
                "[Up/Down] Set tilt to -10 degrees",
                () => heldLight() != null && Mathf.Abs(heldLight().GetCurrentTilt() + 10f) <= 2.5f));
            steps.Add(new Step("Hold <color=red>[Z]</color> to lower Kelvin or <color=red>[X]</color> to raise it. Set 3200K for a warm automotive look. Lower numbers look warmer; higher numbers look cooler.",
                "[Z/X] Set temperature to 3200K",
                () => heldLight() != null && Mathf.Abs(heldLight().GetColorTemperature() - 3200f) <= 250f));
            steps.Add(new Step("Use <color=red>[V/B]</color> to set diffusion to 75%. Diffusion softens shadow edges and spreads the reflection.",
                "[V/B] Set diffusion to 75%",
                () => heldLight() != null && Mathf.Abs(heldLight().GetDiffusionPercent() - 75f) <= 2.5f));
        }
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
                        step == "PickUpSDCard" ? item is Player.Equipment.SDCardItem card && !card.isUsedCard :
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

