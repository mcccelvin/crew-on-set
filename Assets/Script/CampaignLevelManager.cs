using UnityEngine;

public class CampaignLevelManager : MonoBehaviour
{
    public static CampaignLevelManager Instance;

    public enum CampaignLevelStep
    {
        PreviousResults,
        Introduction,
        IntroduceContract,
        OfferContract,
        ContractAccepted,
        IntroduceAlmanac,
        OpenAlmanac,
        ReviewAlmanac,
        ContractBriefing,
        LevelActive,
        CampaignComplete
    }

    public CampaignLevelStep currentStep;

    private bool isLevelStarted = false;
    private bool isBriefingOpen = false;
    private int activeLevel = 4;
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
        BeginLevel(tutorialManager, CampaignProgression.GetCurrentLevel());
    }

    public void BeginLevel(TutorialManager tutorialManager, int level)
    {
        if (isLevelStarted) return;

        int currentLevel = Mathf.Clamp(level, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);
        if (currentLevel < 4) return;

        this.tutorialManager = tutorialManager;
        activeLevel = Mathf.Clamp(currentLevel, 4, 5);
        isLevelStarted = true;
        isBriefingOpen = true;
        currentStep = CampaignLevelStep.PreviousResults;

        CampaignProgression.SetCurrentLevel(activeLevel);
        RestoreOwnedEquipment();
        SetupContractUI();

        if (activeLevel == 5 && PlayerPrefs.GetInt(CampaignProgression.GetGradedKey(5), 0) == 1)
        {
            ShowCampaignComplete();
            return;
        }

        bool contractAlreadyAccepted = PlayerPrefs.GetInt(CampaignProgression.GetAcceptedKey(activeLevel), 0) == 1;
        if (AlmanacManager.Instance != null && !contractAlreadyAccepted)
        {
            AlmanacManager.Instance.PrepareLevelIntroduction(activeLevel);
        }

        if (contractAlreadyAccepted)
        {
            if (CareerManager.Instance != null)
            {
                CareerManager.Instance.currentActiveJob = CampaignProgression.GetContractName(activeLevel);
            }

            if (contractUIManager != null) contractUIManager.UnlockQualifications();
            UnlockCampaignKnowledge();
            StartContract();
            return;
        }

        ShowPreviousResults();
    }

    public int GetActiveLevel()
    {
        return activeLevel;
    }

    public bool IsBriefingActive()
    {
        return isBriefingOpen;
    }

    public bool IsLevelActive()
    {
        return currentStep == CampaignLevelStep.LevelActive;
    }

    public void CloseBriefing()
    {
        AdvanceDialogue();
    }

    public void AdvanceDialogue()
    {
        if (!isBriefingOpen) return;

        if (currentStep == CampaignLevelStep.PreviousResults)
        {
            ShowLevelIntroduction();
            return;
        }

        if (currentStep == CampaignLevelStep.Introduction)
        {
            ShowContractIntroduction();
            return;
        }

        if (currentStep == CampaignLevelStep.IntroduceContract)
        {
            OfferContract();
            return;
        }

        if (currentStep == CampaignLevelStep.OfferContract)
        {
            AcceptContract();
            return;
        }

        if (currentStep == CampaignLevelStep.ContractAccepted)
        {
            ShowAlmanacIntroduction();
            return;
        }

        if (currentStep == CampaignLevelStep.IntroduceAlmanac)
        {
            StartAlmanacReview();
            return;
        }

        if (currentStep == CampaignLevelStep.ReviewAlmanac)
        {
            ShowContractBriefing();
            return;
        }

        if (currentStep == CampaignLevelStep.ContractBriefing)
        {
            StartContract();
            return;
        }

        if (currentStep == CampaignLevelStep.CampaignComplete)
        {
            CloseCampaignCompleteMessage();
        }
    }

    public bool CanOpenAlmanac()
    {
        return currentStep == CampaignLevelStep.OpenAlmanac ||
               currentStep == CampaignLevelStep.ReviewAlmanac ||
               currentStep == CampaignLevelStep.LevelActive ||
               currentStep == CampaignLevelStep.CampaignComplete;
    }

    public bool CanOpenContractQualifications()
    {
        return currentStep == CampaignLevelStep.LevelActive;
    }

    public void OnAlmanacOpened()
    {
        if (currentStep != CampaignLevelStep.OpenAlmanac) return;

        currentStep = CampaignLevelStep.ReviewAlmanac;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(GetAlmanacReviewTasks());
        }
    }

    public void OnAlmanacClosed()
    {
        if (currentStep != CampaignLevelStep.ReviewAlmanac) return;

        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            if (activeLevel == 4)
            {
                TutorialUIManager.Instance.ShowBossDialogue("Good. The Almanac explains shot coverage, continuity, soft natural lighting, and the warm grade required for Kape Kultura. Use those guides while you plan each shot.", TutorialUIManager.Instance.poseHappy, true, false);
            }
            else
            {
                TutorialUIManager.Instance.ShowBossDialogue("Good. The Almanac now contains the complete campaign workflow. For Haraya, every decision must support the same audience, brand message, and polished visual identity.", TutorialUIManager.Instance.poseHappy, true, false);
            }
        }
    }

    public void OnContractQualificationsOpened()
    {
        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review Wide, Medium, and Close-Up coverage",
                "- Review Continuity and soft natural lighting",
                "- Press <color=red>[TAB]</color> when finished"
            });
        }
        else
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review the Haraya creative brief",
                "- Review integrated lighting and delivery requirements",
                "- Press <color=red>[TAB]</color> when finished"
            });
        }
    }

    public void OnContractQualificationsClosed()
    {
        if (currentStep == CampaignLevelStep.LevelActive) ShowLevelTasks();
    }

    private void ShowPreviousResults()
    {
        string previousGrade = CrossSceneData.finalGrades.letterGrade;
        if (string.IsNullOrEmpty(previousGrade)) previousGrade = "PASS";

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Excellent work on Lambormini! The client approved your automotive commercial with a <color=yellow>" + previousGrade + "</color> grade. You successfully balanced an actor, a vehicle, and soft lighting in one premium frame.", TutorialUIManager.Instance.poseHappy, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Kape Kultura approved your story-driven commercial with a <color=yellow>" + previousGrade + "</color> grade. Your wide, medium, and close-up shots worked together as one continuous scene.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowLevelIntroduction()
    {
        currentStep = CampaignLevelStep.Introduction;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Welcome to <color=yellow>Level 4</color>. A single hero shot is no longer enough. This production must tell a short, believable story using several matching camera angles while keeping the actor, product, lighting, and screen direction continuous.", TutorialUIManager.Instance.poseBoss, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Welcome to <color=yellow>Level 5</color>, your final campaign. This time I will not give you a fixed recipe. You must combine production design, actor direction, composition, lighting, coverage, branding, and color into one consistent client pitch.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowContractIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceContract;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("A new contract has arrived from <color=yellow>Kape Kultura</color>. The client wants a warm everyday coffee story: a brown set, a clearly posed actor interacting with the coffee product, soft natural light, and wide, medium, and close-up coverage that cuts together smoothly.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Your final client is <color=yellow>Haraya</color>, a Filipino lifestyle brand preparing a major launch. The brief requires a teal set, an actor, a hero product, a vehicle, at least four shots, complete 3-Point Lighting, and a polished 20-second edit.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void OfferContract()
    {
        currentStep = CampaignLevelStep.OfferContract;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Review the " + CampaignProgression.GetContractName(activeLevel) + " contract",
                "- Select ACCEPT CONTRACT to continue"
            });
        }

        if (contractUIManager != null)
        {
            if (activeLevel == 4) contractUIManager.ShowLevel4Contract(AcceptContract);
            else contractUIManager.ShowLevel5Contract(AcceptContract);
        }
        else
        {
            isBriefingOpen = true;

            if (TutorialUIManager.Instance != null)
            {
                int payment = activeLevel == 4 ? 100000 : 150000;
                TutorialUIManager.Instance.ShowBossDialogue(CampaignProgression.GetContractName(activeLevel) + " is offering " + payment.ToString("N0") + " B-Coins upfront. Press Space to accept the contract.", TutorialUIManager.Instance.poseBoss, true, false);
            }
        }
    }

    public void AcceptContract()
    {
        string acceptedKey = CampaignProgression.GetAcceptedKey(activeLevel);
        int upfrontPayment = activeLevel == 4 ? 100000 : 150000;

        if (CareerManager.Instance != null)
        {
            if (PlayerPrefs.GetInt(acceptedKey, 0) == 0)
            {
                CareerManager.Instance.AcceptJob(CampaignProgression.GetContractName(activeLevel), upfrontPayment);
            }
            else
            {
                CareerManager.Instance.currentActiveJob = CampaignProgression.GetContractName(activeLevel);
            }
        }

        PlayerPrefs.SetInt(acceptedKey, 1);
        PlayerPrefs.Save();

        if (contractUIManager != null) contractUIManager.UnlockQualifications();
        UnlockCampaignKnowledge();

        currentStep = CampaignLevelStep.ContractAccepted;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Contract accepted. Your next lesson is coverage and continuity. The Almanac has been updated with the exact techniques you need. After this message, press <color=red>[P]</color> and review them before building the set.", TutorialUIManager.Instance.posePointUp, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Final contract accepted. Your Almanac now contains the Haraya campaign workflow and final quality-control checklist. Review them before you commit B-Coins or begin the set.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void ShowAlmanacIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceAlmanac;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Press <color=red>[P]</color> after this message. Read the Shot Coverage, Continuity, and Soft Natural Lighting guides. You can return to them at any time during the contract.", TutorialUIManager.Instance.posePoint, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Press <color=red>[P]</color> after this message. Review Creative Brief Planning, Integrated Production, and Final Delivery. This is your reference, but the creative decisions are yours.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void StartAlmanacReview()
    {
        if (AlmanacManager.Instance == null)
        {
            ShowContractBriefing();
            return;
        }

        currentStep = CampaignLevelStep.OpenAlmanac;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Press <color=red>[P]</color> to open the Production Almanac"
            });
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
    }

    private string[] GetAlmanacReviewTasks()
    {
        if (activeLevel == 4)
        {
            return new string[]
            {
                "- Review Shot Coverage & Continuity",
                "- Review Soft Natural Lighting",
                "- Review the Warm Commercial Grade",
                "- Press <color=red>[P]</color> or CLOSE when finished"
            };
        }

        return new string[]
        {
            "- Review Creative Brief Planning",
            "- Review Integrated Campaign Production",
            "- Review Final Delivery & Quality Control",
            "- Press <color=red>[P]</color> or CLOSE when finished"
        };
    }

    private void ShowContractBriefing()
    {
        currentStep = CampaignLevelStep.ContractBriefing;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Build the warm brown set first. Stage exactly one coffee product with one clearly posed actor, then record at least three clips: a wide shot, a medium shot, and a close-up. Keep the same pose, keep the actor on the same side of the product, and use the Level 3 Soft Light in every clip. Finish with a warm 15-second edit. Press Space when ready.", TutorialUIManager.Instance.poseBoss, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Read the Haraya brief, then plan the complete production. The teal set, actor, product, vehicle, four-shot coverage, three shot sizes, 3-Point Lighting, and polished 20-second edit must feel like one campaign. Press Space when ready.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void StartContract()
    {
        currentStep = CampaignLevelStep.LevelActive;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            ShowLevelTasks();
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
    }

    private void ShowLevelTasks()
    {
        if (TutorialUIManager.Instance == null) return;

        TutorialUIManager.Instance.HideTasks();
    }

    private void UnlockCampaignKnowledge()
    {
        if (AlmanacManager.Instance == null) return;

        if (activeLevel == 4)
        {
            AlmanacManager.Instance.UnlockLevel4Knowledge();
        }
        else
        {
            AlmanacManager.Instance.UnlockLevel5Knowledge();
        }
    }

    private void SetupContractUI()
    {
        contractUIManager = FindObjectOfType<ContractUIManager>();
        if (contractUIManager == null) contractUIManager = gameObject.AddComponent<ContractUIManager>();
        if (contractUIManager != null) contractUIManager.PrepareCampaignContract(activeLevel);
    }

    private void RestoreOwnedEquipment()
    {
        ShopTerminal shopTerminal = FindObjectOfType<ShopTerminal>();
        if (shopTerminal == null || shopTerminal.availableItems.Count < 2) return;

        GameObject level2CameraPrefab = Resources.Load<GameObject>("Prefabs/Level 2 Camera Placeholder");
        if (level2CameraPrefab != null) shopTerminal.RestoreLevel2Camera(level2CameraPrefab);

        if (PlayerPrefs.GetInt("LamborminiContractGraded", 0) == 1 && PlayerPrefs.GetInt("Level3LightPurchased", 0) == 0)
        {
            PlayerPrefs.SetInt("Level3LightPurchased", 1);
            PlayerPrefs.Save();
        }

        bool usePlaceholder = shopTerminal.level3LightPrefab == null;
        GameObject level3LightPrefab = usePlaceholder ? shopTerminal.availableItems[1].prefabToSpawn : shopTerminal.level3LightPrefab;
        if (level3LightPrefab == null) return;

        if (PlayerPrefs.GetInt("Level3LightPurchased", 0) == 1)
        {
            shopTerminal.RestoreLevel3Light(level3LightPrefab, usePlaceholder);
        }
        else
        {
            shopTerminal.SetupLevel3Light(level3LightPrefab, usePlaceholder);
        }
    }

    private void ShowCampaignComplete()
    {
        currentStep = CampaignLevelStep.CampaignComplete;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("You completed the Haraya launch and finished all five Crew-On-Set contracts. You can now revisit the studio, review the complete Production Almanac, and improve any commercial that did not earn your target grade.", TutorialUIManager.Instance.poseEndWave, true, false);
        }
    }

    private void CloseCampaignCompleteMessage()
    {
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- CAMPAIGN COMPLETE",
                "- Press <color=red>[P]</color> to review the Production Almanac"
            });
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
    }
}
