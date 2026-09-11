using PlayerPrefs = GameSavePrefs;
using UnityEngine;

public class CampaignLevelManager : MonoBehaviour
{
    public static CampaignLevelManager Instance;

    public enum CampaignLevelStep
    {
        PreviousResults,
        Introduction,
        IntroduceActor,
        PracticeActor,
        ActorPlaced,
        ActorPosed,
        ActorPracticeComplete,
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
    private GuidedPracticeLesson practiceLesson;
    private GameObject directorPracticeMarker;
    private int activeLevel = 4;
    private TutorialManager tutorialManager;
    private ContractUIManager contractUIManager;

    public void DisableForDevTesting()
    {
        if (!isLevelStarted) return;
        StopAllCoroutines();
        practiceLesson?.Release();
        practiceLesson = null;
        if (directorPracticeMarker != null) directorPracticeMarker.SetActive(false);
        currentStep = CampaignLevelStep.LevelActive;
        isBriefingOpen = false;
        enabled = false;
        if (PlayerPrefs.GetInt(CampaignProgression.GetAcceptedKey(activeLevel), 0) == 0) OfferContract();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void LateUpdate()
    {
        practiceLesson?.Tick();
    }

    private void OnDestroy()
    {
        RemoveDirectorPracticeMarker();
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
        return isBriefingOpen || (practiceLesson != null && practiceLesson.IsExplaining);
    }

    public bool IsLevelActive()
    {
        return currentStep == CampaignLevelStep.LevelActive;
    }

    public bool IsActorIntroductionActive()
    {
        return activeLevel >= 4 &&
               (currentStep == CampaignLevelStep.PracticeActor ||
                currentStep == CampaignLevelStep.ActorPlaced ||
                currentStep == CampaignLevelStep.ActorPosed);
    }

    public void OnDirectorTerminalOpened()
    {
        if (!IsActorIntroductionActive() || practiceLesson != null) return;

        if (TutorialUIManager.Instance != null)
        {
            if (currentStep == CampaignLevelStep.ActorPlaced)
            {
                TutorialUIManager.Instance.SetupTasks(new string[]
                {
                    "- Select the placed actor",
                    "- Click POSE ACTOR to choose a performance"
                });
            }
            else if (currentStep == CampaignLevelStep.ActorPosed)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Close the Director Terminal" });
            }
            else
            {
                TutorialUIManager.Instance.SetupTasks(new string[]
                {
                    "- Click one Actor card",
                    "- Move the actor onto the stage and click to place"
                });
            }
        }
    }

    public void OnActorPlaced(GameObject actor)
    {
        if (activeLevel < 4 || currentStep != CampaignLevelStep.PracticeActor || actor == null) return;

        currentStep = CampaignLevelStep.ActorPlaced;
        if (practiceLesson != null) return;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Select the placed actor",
                "- Click POSE ACTOR to choose a performance"
            });
        }
    }

    public void OnActorPosed(CubeActor actor)
    {
        if (activeLevel < 4 || actor == null) return;
        if (currentStep != CampaignLevelStep.PracticeActor && currentStep != CampaignLevelStep.ActorPlaced) return;

        currentStep = CampaignLevelStep.ActorPosed;
        if (practiceLesson != null) return;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- <color=#55FF88>Actor pose selected: " + actor.GetPoseName() + "</color>",
                "- Close the Director Terminal"
            });
        }
    }

    public void OnDirectorTerminalClosed()
    {
        if (activeLevel < 4 || practiceLesson != null) return;

        if (currentStep == CampaignLevelStep.ActorPosed)
        {
            ShowActorPracticeComplete();
            return;
        }

        if (currentStep != CampaignLevelStep.PracticeActor && currentStep != CampaignLevelStep.ActorPlaced) return;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Open the Director Terminal",
                "- Place one actor and choose a pose"
            });
        }
    }

    public void CloseBriefing()
    {
        AdvanceDialogue();
    }

    public void AdvanceDialogue()
    {
        if (practiceLesson != null) { practiceLesson.Continue(); return; }
        if (!isBriefingOpen) return;

        if (currentStep == CampaignLevelStep.PreviousResults)
        {
            ShowLevelIntroduction();
            return;
        }

        if (currentStep == CampaignLevelStep.Introduction)
        {
            ShowActorIntroduction();
            return;
        }

        if (currentStep == CampaignLevelStep.IntroduceActor)
        {
            StartActorPractice();
            return;
        }

        if (currentStep == CampaignLevelStep.ActorPracticeComplete)
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
                TutorialUIManager.Instance.ShowBossDialogue("Those guides are there whenever you need them. Press <color=red>[P]</color> for a reminder on posing, matching shots, or soft natural light.", TutorialUIManager.Instance.poseHappy, true, false);
            }
            else
            {
                TutorialUIManager.Instance.ShowBossDialogue("You've got the whole toolkit in the Almanac now. For Haraya, the trick is making all those choices serve the same idea.", TutorialUIManager.Instance.poseHappy, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Lambormini's signed off on your commercial: <color=yellow>" + previousGrade + "</color>! You've had a go at shaping a car with light. Let's put someone in front of the camera next.", TutorialUIManager.Instance.poseHappy, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Kape Kultura's happy! Your commercial earned a <color=yellow>" + previousGrade + "</color>. Getting those different shots to feel like one scene takes care.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowLevelIntroduction()
    {
        currentStep = CampaignLevelStep.Introduction;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Time to bring someone onto our set. In <color=yellow>Level 4</color>, we'll pose an Actor and tell a little more of the story with wide, medium, and close-up shots.", TutorialUIManager.Instance.poseBoss, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("One last brief: <color=yellow>Level 5</color>. This one's going to bring your skills together. Take it a shot at a time; the contract is our guide.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowActorIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceActor;
        isBriefingOpen = true;

        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockKnowledge("hiring_and_posing_actors");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Let's give our scene a performance. We'll walk to the tablet, place an Actor, and choose a pose together. I'll guide each step.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void StartActorPractice()
    {
        currentStep = CampaignLevelStep.PracticeActor;
        isBriefingOpen = false;
        var director = FindObjectOfType<DirectorTerminal>();
        Transform target = null;
        if (tutorialManager != null && tutorialManager.availableTargets != null)
            foreach (var candidate in tutorialManager.availableTargets)
                if (string.Equals(candidate.targetName, "director", System.StringComparison.OrdinalIgnoreCase))
                    target = candidate.targetTransform;

        var player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (target != null && player != null)
        {
            Vector3 towardPlayer = player.transform.position - target.position;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude < 0.01f) towardPlayer = Vector3.forward;
            Vector3 position = target.position + towardPlayer.normalized * 1.2f;
            position.y = player.transform.position.y;
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 4f))
                position.y = floor.point.y;
            directorPracticeMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            directorPracticeMarker.name = "Director Practice Standing Circle";
            directorPracticeMarker.transform.position = position + Vector3.up * 0.03f;
            directorPracticeMarker.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
            Destroy(directorPracticeMarker.GetComponent<Collider>());
            directorPracticeMarker.GetComponent<Renderer>().material.color = new Color(1f, 0.76f, 0.08f);
        }

        var steps = new System.Collections.Generic.List<GuidedPracticeLesson.Step>();
        if (directorPracticeMarker != null)
            steps.Add(new GuidedPracticeLesson.Step(
                "Walk onto the circle beside the Director Tablet. This is where we'll prepare the performance.",
                "Walk onto the circle beside the Director Tablet",
                () => GuidedPracticeLesson.AtMarker(directorPracticeMarker.transform)));
        steps.Add(new GuidedPracticeLesson.Step(
            "Look at the Director Tablet and press <color=red>[E]</color> to open it.",
            "[E] Open the Director Tablet",
            () => director != null && director.IsTerminalActive()));
        steps.Add(new GuidedPracticeLesson.Step(
            "Choose one Actor card. Move the Actor onto the stage and click to place them. Leave space for the product.",
            "Choose one Actor card, then click the stage to place them",
            () => currentStep == CampaignLevelStep.ActorPlaced || currentStep == CampaignLevelStep.ActorPosed));
        steps.Add(new GuidedPracticeLesson.Step(
            "Select your Actor and click <color=yellow>POSE ACTOR</color>. Choose a performance that suits the scene; keep the product visible.",
            "Select the Actor and click POSE ACTOR",
            () => currentStep == CampaignLevelStep.ActorPosed));
        steps.Add(new GuidedPracticeLesson.Step(
            "Good. Close the tablet with <color=red>[E]</color> and look at the pose from the studio. Keep that pose consistent when you change camera angles.",
            "[E] Close the Director Tablet",
            () => director != null && !director.IsTerminalActive()));
        if (tutorialManager != null) tutorialManager.PointLineAt("director");
        practiceLesson = new GuidedPracticeLesson(tutorialManager, steps, () =>
        {
            practiceLesson = null;
            RemoveDirectorPracticeMarker();
            if (tutorialManager != null) tutorialManager.PointLineAt("");
            ShowActorPracticeComplete();
        });
    }

    private void RemoveDirectorPracticeMarker()
    {
        if (directorPracticeMarker == null) return;
        Destroy(directorPracticeMarker.GetComponent<Renderer>().sharedMaterial);
        Destroy(directorPracticeMarker);
        directorPracticeMarker = null;
    }

    private void ShowActorPracticeComplete()
    {
        currentStep = CampaignLevelStep.ActorPracticeComplete;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetDynamicGlow("director", false);
            TutorialUIManager.Instance.ShowBossDialogue("Now our scene has someone in it. Leave the product visible, and keep the Actor's pose and screen side consistent when you change shots.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void ShowContractIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceContract;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("We've got a coffee brief from <color=yellow>Kape Kultura</color>: a brown set, coffee, an Actor, and soft light. They want three shot sizes that feel like one scene.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Our final client is <color=yellow>Haraya</color>. Their brief brings an Actor, a product, and a vehicle onto a teal set, with three-point lighting. Four shots, one 20-second commercial.", TutorialUIManager.Instance.poseOpenHand, true, false);
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
                TutorialUIManager.Instance.ShowBossDialogue(CampaignProgression.GetContractName(activeLevel) + " is putting up " + payment.ToString("N0") + " B-Coins upfront. Shall we take the job? Press <color=red>[SPACE]</color> to accept.", TutorialUIManager.Instance.poseBoss, true, false);
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
        if (DevTutorialBypass.Disabled) { StartContract(); return; }
        isBriefingOpen = true;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("We're on the coffee job. Before we build, open the Almanac with <color=red>[P]</color>. Let's look at how different shots can fit together.", TutorialUIManager.Instance.posePointUp, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Haraya's counting on us. Before you spend the budget, open <color=red>[P]</color> and look through the workflow and quality checklist. A little planning will help here.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void ShowAlmanacIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceAlmanac;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Open <color=red>[P]</color> when we finish chatting. Actor Blocking, Shot Coverage, Continuity, and Soft Natural Lighting will help you plan this scene.", TutorialUIManager.Instance.posePoint, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Take a look in <color=red>[P]</color> after our chat. Creative Brief Planning, Integrated Production, and Final Delivery will help you break this bigger job down.", TutorialUIManager.Instance.posePoint, true, false);
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
                "- Review Hiring, Blocking & Posing Actors",
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
            TutorialUIManager.Instance.ShowBossDialogue("Let's make that coffee scene: brown set, product, and posed Actor. Keep the action consistent across wide, medium, and close-up shots. Press <color=red>[SPACE]</color> when you're ready.", TutorialUIManager.Instance.poseBoss, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Give the Haraya brief another look before you buy. Plan four shots with a shared set, lighting style, and message. Ready to make it yours? Press <color=red>[SPACE]</color>.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("Five jobs, from your first vase shoot to Haraya. You've come a long way. Take a bow! The Almanac's on <color=red>[P]</color> if you fancy another run.", TutorialUIManager.Instance.poseEndWave, true, false);
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
