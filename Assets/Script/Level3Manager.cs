using UnityEngine;

public class Level3Manager : MonoBehaviour
{
    public static Level3Manager Instance;

    private enum Level3Step
    {
        GokeResults,
        Introduction,
        IntroduceLight,
        IntroduceActor,
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
    private Level3Step currentStep;
    private TutorialManager tutorialManager;
    private ContractUIManager contractUIManager;

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
        if (Instance == this) Instance = null;
    }

    public void BeginLevel(TutorialManager tutorialManager)
    {
        if (isLevelStarted) return;

        this.tutorialManager = tutorialManager;
        isLevelStarted = true;
        isBriefingOpen = true;
        currentStep = Level3Step.GokeResults;

        CampaignProgression.SetCurrentLevel(3);

        SetupLevel3Equipment();
        SetupContractUI();

        if (PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 1)
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
            ShowActorIntroduction();
            return;
        }

        if (currentStep == Level3Step.IntroduceActor)
        {
            ShowAlmanacIntroduction();
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

    public void OnContractQualificationsOpened()
    {
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review Actor Direction",
                "- Review Automotive Lighting",
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
                "- Review Hiring & Posing Actors",
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
            TutorialUIManager.Instance.ShowBossDialogue("Good. The Almanac now covers the <color=yellow>Level 3 Soft Light</color>, hiring and posing actors, and automotive staging. Keep checking it whenever a production introduces new equipment or crew.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowLevelIntroduction()
    {
        currentStep = Level3Step.Introduction;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Welcome to <color=yellow>Level 3</color>. From this point forward, clients will expect more advanced equipment choices, more complicated stage setups, and stronger creative decisions. Your next Level 3 contract will appear here when it is ready.", TutorialUIManager.Instance.poseBoss, true, false);
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

        if (PlayerPrefs.GetInt("Level3LightPurchased", 0) == 1)
        {
            shopTerminal.RestoreLevel3Light(lightPrefab, usePlaceholder);
        }
        else
        {
            shopTerminal.SetupLevel3Light(lightPrefab, usePlaceholder);
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
            TutorialUIManager.Instance.ShowBossDialogue("Before the next contract, I want you to meet a new piece of equipment: the <color=yellow>Level 3 Soft Light</color>. It has twice the output of the 160 LED Panel and produces softer shadows for cleaner subject lighting. I placed a temporary model in the Equipment Shop for you to inspect.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowAlmanacIntroduction()
    {
        currentStep = Level3Step.IntroduceAlmanac;

        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockLevel3Equipment();

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Before the contract, check the new <color=yellow>Production Almanac</color> guides. They explain the Level 3 Soft Light, how to hire and pose actors, and how to stage an actor with a vehicle. Press <color=red>[P]</color> after this message to open the Almanac.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void ShowActorIntroduction()
    {
        currentStep = Level3Step.IntroduceActor;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("New equipment is only part of Level 3. From now on, some contracts will also require an <color=yellow>Actor</color>. Open the Director Terminal and drag one of the actor cards onto the stage, just like a prop. Select the actor afterward and use the <color=yellow>POSE ACTOR</color> button to change the performance pose. There is also a temporary car model available for larger productions.", TutorialUIManager.Instance.poseOpenHand, true, false);
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
                "- Read the three new Level 3 production guides"
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
            TutorialUIManager.Instance.ShowBossDialogue("A new automotive contract just arrived from <color=yellow>Lambormini</color>. The client wants a premium car commercial using the new Level 3 Soft Light and a hired actor. You will need to place the car, direct the actor into a pose, and frame both of them clearly. Review the contract board before accepting.", TutorialUIManager.Instance.poseBoss, true, false);
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
                TutorialUIManager.Instance.ShowBossDialogue("Lambormini is offering an 80,000 B-Coin contract requiring a car, a posed actor, the Level 3 Soft Light, and a premium automotive composition. Press Space to accept.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Contract accepted. The Lambormini car and actor cards are available in the Director Terminal. Use the Level 3 Soft Light to create clean body highlights, place an actor beside the car, and choose a pose that does not block the vehicle. Press <color=red>[TAB]</color> whenever you need to review the qualifications.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowLevelTasks()
    {
        if (TutorialUIManager.Instance == null) return;

        TutorialUIManager.Instance.SetupTasks(new string[]
        {
            "- LAMBORMINI CONTRACT",
            "- Get the Level 3 Soft Light",
            "- Add the Lambormini car from the Director Terminal",
            "- Hire and pose one actor beside the car",
            "- Frame the actor and car for the commercial"
        });
    }
}
