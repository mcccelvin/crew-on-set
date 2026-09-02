using System.Collections;
using System.Collections.Generic;
using Player.Equipment;
using TMPro;
using UnityEngine;

public class Level3Manager : MonoBehaviour
{
    public static Level3Manager Instance;

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
        CleanUpLightingPractice();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
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
            if (CareerManager.Instance != null) CareerManager.Instance.currentActiveJob = "Lambormini";
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
            TutorialUIManager.Instance.ShowBossDialogue("Excellent work on the Goke Cola contract! The client approved your production with a <color=yellow>" + gokeGrade + "</color> grade. You proved that you can handle Rule of Thirds composition, 3-Point Lighting, and a more demanding commercial edit.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    public void CloseBriefing()
    {
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
        return isBriefingOpen;
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
                if (tutorialManager != null) tutorialManager.ShowWarning("Buy the Level 3 Soft Light first!");
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
            if (tutorialManager != null) tutorialManager.ShowWarning("The Soft Light is already in your cart. Confirm the purchase!");
            return false;
        }

        return true;
    }

    public bool CanConfirmPurchase()
    {
        if (currentStep == Level3Step.LightCheckout) return true;

        if (currentStep == Level3Step.BuyLight)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Add the Level 3 Soft Light before checkout!");
            return false;
        }

        return true;
    }

    public bool CanCancelPurchase()
    {
        if (currentStep != Level3Step.BuyLight && currentStep != Level3Step.LightCheckout) return true;

        if (tutorialManager != null) tutorialManager.ShowWarning("Complete the Soft Light purchase so the lesson can continue!");
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
            if (tutorialManager != null) tutorialManager.ShowWarning("Pick up the Level 3 Soft Light from the delivery table!");
            return;
        }

        practiceLight = light;
        ShowLightingPracticeIntroduction();
    }

    public bool CanPickUpLight(FilmLightItem light)
    {
        if (currentStep != Level3Step.ObserveSoftLight || light == null || light != practiceLight) return true;

        if (tutorialManager != null) tutorialManager.ShowWarning("Keep the Soft Light in position while you study the result.");
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

        Vector3 lightPosition = light.transform.position;
        Vector3 markerPosition = softLightPlacementMarker.position;
        lightPosition.y = 0f;
        markerPosition.y = 0f;

        bool isNearMarker = Vector3.Distance(lightPosition, markerPosition) <= 1.5f;
        bool hasCorrectIntensity = Mathf.Abs(light.intensityPercent - 75f) <= 2.5f;
        bool hasCorrectTilt = Mathf.Abs(light.GetCurrentTilt() + 10f) <= 2.5f;
        bool hasCorrectTemperature = Mathf.Abs(light.GetColorTemperature() - 5400f) <= 250f;
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
                                ? "Set color temperature to 5400K with Z and X."
                                : !hasCorrectDiffusion
                                    ? "Set diffusion to 75% with V and B."
                                    : "Stand on the SOFT KEY marker before pressing G.";
                tutorialManager.ShowWarning("Pick the light back up. " + correction);
            }
            return;
        }

        light.transform.position = softLightPlacementMarker.position + Vector3.up * 0.05f;
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
        StartCoroutine(ObserveLightingSetup());
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
            TutorialUIManager.Instance.ShowBossDialogue("Good. Level 3 is about controlling a larger, softer source. Use it to reveal shape and reflections without flattening the subject. The Almanac now keeps that setup available whenever you need to review it.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowLevelIntroduction()
    {
        currentStep = Level3Step.Introduction;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Welcome to <color=yellow>Level 3</color>. This level focuses on one professional upgrade: learning how a larger <color=yellow>Soft Light</color> creates broad highlights, smooth shadow transitions, and cleaner reflections on premium products.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Meet the <color=yellow>Level 3 Soft Light</color>. It has twice the output of the 160 LED Panel, adjustable <color=yellow>color temperature</color>, and controllable <color=yellow>diffusion</color> for smoother shadow edges. Before taking a client job, you will buy it and complete a guided setup on the stage.", TutorialUIManager.Instance.poseHappy, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("The Soft Light is waiting at the <color=yellow>delivery table</color>. Pick it up, then bring it to the stage. I prepared a reflective practice surface and a marked professional Key Light position.", TutorialUIManager.Instance.posePoint, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Stand on the <color=yellow>SOFT KEY marker</color>. Turn the light ON, set intensity to <color=yellow>75%</color>, tilt to <color=yellow>-10 degrees</color>, color temperature to <color=yellow>5400K</color> with Z and X, and diffusion to <color=yellow>75%</color> with V and B. This creates neutral product color, a broad highlight, and a controlled soft shadow. Press <color=red>[G]</color> when finished.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartSoftLightPractice()
    {
        currentStep = Level3Step.PlaceSoftLight;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        ShowCurrentLightingTasks();

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
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
            TutorialUIManager.Instance.ShowBossDialogue("Soft Light practice complete. A larger source does not mean lighting everything evenly. Your 75% side Key created a clean reflection, the -10 degree tilt aimed the beam through the subject, and the remaining shadow kept the form three-dimensional. I will return the light to delivery after this message.", TutorialUIManager.Instance.poseHappy, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("The <color=yellow>Production Almanac</color> now includes the Level 3 Soft Light, reflective-surface lighting, and automotive staging. Press <color=red>[P]</color> after this message and review the same setup you just practiced.", TutorialUIManager.Instance.posePoint, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("A new automotive contract just arrived from <color=yellow>Lambormini</color>. This Level 3 assignment is a product-lighting test: stage the vehicle by itself, reveal its body shape with the Soft Light, and create a premium composition. Actors will be introduced in Level 4, where performance and continuity become part of the brief.", TutorialUIManager.Instance.poseBoss, true, false);
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
                "- Review the Lambormini contract",
                "- Select ACCEPT CONTRACT to continue"
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
                TutorialUIManager.Instance.ShowBossDialogue("Lambormini is offering an 80,000 B-Coin contract requiring the vehicle, the Level 3 Soft Light, and a premium automotive composition. Press Space to accept.", TutorialUIManager.Instance.poseBoss, true, false);
            }
        }
    }

    private void AcceptContract()
    {
        if (CareerManager.Instance != null)
        {
            if (PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 0)
            {
                CareerManager.Instance.AcceptJob("Lambormini", 80000);
                PlayerPrefs.SetInt("LamborminiContractAccepted", 1);
                PlayerPrefs.Save();
            }
            else
            {
                CareerManager.Instance.currentActiveJob = "Lambormini";
            }
        }

        if (contractUIManager != null) contractUIManager.UnlockQualifications();

        currentStep = Level3Step.ContractAccepted;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Contract accepted. The Lambormini car is available in the Director Terminal. Start with the Level 3 Soft Light at 75%, -10 degrees, 5400K, and 75% diffusion. Then refine its distance and angle until the body shape reads clearly. Press <color=red>[TAB]</color> whenever you need to review the qualifications.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowCurrentLightingTasks()
    {
        if (TutorialUIManager.Instance == null || practiceLight == null) return;

        string powerTask = practiceLight.IsPoweredOn() ? "<color=#55FF88>ON</color>" : "OFF";
        string intensityTask = Mathf.RoundToInt(practiceLight.intensityPercent) + "% / 75%";
        string tiltTask = Mathf.RoundToInt(practiceLight.GetCurrentTilt()) + " degrees / -10 degrees";
        string temperatureTask = Mathf.RoundToInt(practiceLight.GetColorTemperature()) + "K / 5400K";
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
        practiceSpotlight.range = 12f;
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

        GameObject markerDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerDisc.name = "Placement Point";
        markerDisc.transform.SetParent(markerRoot.transform);
        markerDisc.transform.localPosition = Vector3.zero;
        markerDisc.transform.localScale = new Vector3(0.85f, 0.025f, 0.85f);

        Collider markerCollider = markerDisc.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);

        Color markerColor = new Color(1f, 0.76f, 0.08f, 1f);
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
        if (TutorialUIManager.Instance == null) return;
        TutorialUIManager.Instance.HideTasks();
    }
}
