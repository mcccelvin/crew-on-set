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
    private bool coffeeLessonStarted;
    private float nextCoffeeLessonTick;
    private CubeActor featureActor;
    private int observedActorActions;
    private Quaternion actorFeatureRotation;
    private Vector3 actorFeaturePosition;

    private bool ObserveActorActions()
    {
        var actors = FindObjectsOfType<CubeActor>();
        if (actors.Length != 1) return false;
        if (featureActor != actors[0]) { featureActor = actors[0]; observedActorActions = 0; }
        if (featureActor.GetPoseName() == "Wave") observedActorActions |= 1;
        if (featureActor.GetPoseName() == "Action") observedActorActions |= 2;
        if (observedActorActions != 3) return false;
        actorFeatureRotation = featureActor.transform.rotation;
        actorFeaturePosition = featureActor.transform.position;
        return true;
    }

    private bool ObserveActorTurn()
    {
        if (featureActor == null)
        {
            featureActor = FindObjectOfType<CubeActor>();
            if (featureActor != null) actorFeatureRotation = featureActor.transform.rotation;
            return false;
        }
        if (Quaternion.Angle(actorFeatureRotation, featureActor.transform.rotation) < 10f) return false;
        actorFeaturePosition = featureActor.transform.position;
        return true;
    }

    private bool ObserveActorMove()
    {
        if (featureActor == null)
        {
            featureActor = FindObjectOfType<CubeActor>();
            if (featureActor != null) actorFeaturePosition = featureActor.transform.position;
            return false;
        }
        Vector3 offset = featureActor.transform.position - actorFeaturePosition;
        offset.y = 0f;
        return offset.sqrMagnitude >= .01f;
    }

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
        if (coffeeLessonStarted && Time.unscaledTime < nextCoffeeLessonTick) return;
        nextCoffeeLessonTick = Time.unscaledTime + .2f;
        practiceLesson?.Tick();
    }

    public void OnCoffeeTakeRecorded(Player.Equipment.SDCardItem card)
    {
        if (activeLevel != 4 || card == null || card.campaignLevel != 4 || DevTutorialBypass.Disabled) return;
        string size = card.shotType == 1 ? "WIDE" : card.shotType == 2 ? "MEDIUM" : "CLOSE-UP";
        string hint = card.videoDuration < 5f ? "Record at least 5 seconds for the edit." :
            !card.requiredSubjectsVisible ? "Retake: keep both subjects fully visible throughout." :
            !card.usedSoftLight ? "Retake: power and aim the Soft Light at both subjects." :
            string.IsNullOrEmpty(card.actorPose) || card.actorPose == "Neutral" ? "Retake: choose Wave or Action on the tablet." :
            Mathf.Abs(card.screenDirection) <= .1f ? "Retake: separate the Actor and coffee in the frame." :
            "Collect this card. Keep the same pose and screen side.";
        GameFeedback.Show(size + " TAKE SAVED\n" + hint);
    }

    private void OnDestroy()
    {
        practiceLesson?.Release();
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
            TutorialUIManager.Instance.ShowBossDialogue("Terrari's signed off on your commercial: <color=yellow>" + previousGrade + "</color>! You've had a go at shaping a car with light. Let's put someone in front of the camera next.", TutorialUIManager.Instance.poseHappy, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("In <color=yellow>Level 4</color>, we will add an Actor to the product story. Keep your camera and Soft Light from the previous job. We will practice hiring, placing and posing the Actor, then learn wide, medium and close-up shots together. Each shot has a purpose; you do not need a dolly or extra lighting equipment.", TutorialUIManager.Instance.poseBoss, true, false);
        }
        else
        {
            TutorialUIManager.Instance.ShowBossDialogue("Welcome to <color=yellow>Level 5</color>. We will combine familiar tools: your camera records the shot, the SD card carries the take, and the lights reveal the product. Reuse equipment from delivery before buying more. Start with the set, then lighting, then camera and finally editing. The contract and Almanac explain each part; no new grip tools are required.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowActorIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceActor;
        isBriefingOpen = true;

        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockKnowledge("hiring_and_posing_actors");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Let's hire an Actor bot. Rookie costs 750, Trained 2,250, and Expert 4,500 B-Coins at the default rates. Better actors deliver smoother, more expressive gestures. Every tier can meet the brief. We'll place one on a mark and choose their repeating action together.", TutorialUIManager.Instance.poseOpenHand, true, false);
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
            "Select your Actor and click <color=yellow>POSE ACTOR</color>. Wave starts an animated greeting; Action starts a product-presentation gesture. The bot performs automatically while staying on its mark. Choose an action that keeps the coffee visible.",
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
                "- Select the contract, read the brief, then close it to continue"
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
                int payment = ProductionEconomy.Advance(activeLevel);
                TutorialUIManager.Instance.ShowBossDialogue(CampaignProgression.GetContractName(activeLevel) + " is putting up " + payment.ToString("N0") + " B-Coins upfront. Shall we take the job? Press <color=red>[SPACE]</color> to accept.", TutorialUIManager.Instance.poseBoss, true, false);
            }
        }
    }

    public void AcceptContract()
    {
        string acceptedKey = CampaignProgression.GetAcceptedKey(activeLevel);
        int upfrontPayment = ProductionEconomy.Advance(activeLevel);

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
        if (coffeeLessonStarted && practiceLesson != null) return;
        currentStep = CampaignLevelStep.LevelActive;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            ShowLevelTasks();
        }

        if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
        if (activeLevel == 4 && !DevTutorialBypass.Disabled && !coffeeLessonStarted)
            StartCoffeeProductionLesson();
    }

    private void StartCoffeeProductionLesson()
    {
        coffeeLessonStarted = true;
        var director = FindObjectOfType<DirectorTerminal>();
        var steps = new System.Collections.Generic.List<GuidedPracticeLesson.Step>
        {
            new GuidedPracticeLesson.Step(
                "Let's build a welcoming coffee scene. Open the Director Tablet, use <color=yellow>CHOOSE SET</color>. Pick Cafe Corner, Living Room, or Plain Backdrop; each option shows its price. Furnished interiors start warm brown. You can still paint the wall and floor with HEX <color=yellow>#80502E</color>. Warm surroundings suggest a comfortable morning; the product must still stand out.",
                "CHOOSE SET, then use a warm brown (try #80502E)",
                () => director != null && director.HasWall() && IsCoffeeBrown(director.currentWallColor)),
            new GuidedPracticeLesson.Step(
                "Place <color=yellow>one KAPE KULTURA PRODUCT</color> beside <color=yellow>one Actor</color>. Keep the Actor from our practice, or place a replacement. Select the Actor and use POSE ACTOR for Wave or Action. Separate them enough that neither hides the other, then close the tablet.",
                "Place 1 coffee + 1 posed Actor; close the tablet",
                () => CoffeeSubjectsReady() && director != null && !director.IsTerminalActive()),
            new GuidedPracticeLesson.Step(
                "Your Actor is an autonomous performer. <color=yellow>Rookie</color> uses smaller, slower gestures; <color=yellow>Trained</color> is smoother; <color=yellow>Expert</color> is more expressive. The card shows the hire fee before you buy. Higher skill buys performance polish, not an automatic better grade. Keep your current Actor: every tier can meet this brief.",
                "Keep your hired Actor; all skill tiers can meet the contract",
                () => true),
            new GuidedPracticeLesson.Step(
                "Let's try both animated actions. Reopen the tablet, select your Actor and click <color=yellow>POSE ACTOR</color> until you see <color=yellow>Wave</color>. Watch the greeting. Click again for <color=yellow>Action</color>: a product-presentation gesture. Neutral returns to idle breathing. The Actor stays on its mark while performing.",
                "On the tablet, try Wave and Action on the same Actor",
                ObserveActorActions),
            new GuidedPracticeLesson.Step(
                "<color=yellow>Blocking</color> means choosing where the performer stands and faces. With the Actor selected, press <color=red>[R]</color> to turn 15 degrees. Aim the performance toward the camera without hiding the coffee. This changes the stage direction; it does not change the selected action.",
                "Select the Actor and press [R] to turn",
                ObserveActorTurn),
            new GuidedPracticeLesson.Step(
                "Now select the Actor and press <color=red>[T]</color>. Move them a little, then click the stage to place them. Leave space beside the coffee so animated hands do not block it. You can refine this placement before filming; keep it fixed across your three takes.",
                "[T] Move the Actor, then click to place them",
                () => ObserveActorMove() && director != null && !director.IsPlacingProp()),
            new GuidedPracticeLesson.Step(
                "Choose Wave or Action for the commercial, then close the tablet with <color=red>[E]</color>. Each recording restarts that animation from the beginning to help matching shots. Keep the same action and screen side in every take. Pause also pauses the bot. Watch a full gesture before recording and leave room around the moving hands.",
                "Choose Wave or Action, then [E] close the tablet",
                () => CoffeeSubjectsReady() && director != null && !director.IsTerminalActive()),
            new GuidedPracticeLesson.Step(
                "Try <color=yellow>motivated lighting</color>: let the Soft Light suggest a window beside the scene. Place it in front and to one side, power it on, and aim between the Actor and coffee. Start near 75% output and at least 50% diffusion. The broad highlight should reveal detail while leaving a gentle shadow on the far side.",
                "Power and aim the Soft Light at the Actor and coffee",
                CoffeeLightReady),
            new GuidedPracticeLesson.Step(
                "<color=yellow>WIDE</color> establishes where we are. <color=yellow>MEDIUM</color> connects the Actor to the coffee. <color=yellow>CLOSE-UP</color> gives the product emphasis. In this game, even the close shot must keep both subjects fully inside the frame. The camera's focus readout now names your shot size. Move or zoom to change it.",
                "Plan a Wide, Medium and Close-Up of the same scene",
                () => true),
            new GuidedPracticeLesson.Step(
                "Keep the same pose and set positions for all three takes. Stay on the same side of the Actor-product line so they do not swap screen sides: this is <color=yellow>continuity</color>. Record about 6 seconds per shot, holding still with both visible and the Soft Light aimed at them. Use a fresh SD card for each take and collect the recorded cards. Check the WIDE / MEDIUM / CLOSE-UP readout before recording.",
                "Record all 3 sizes: same pose/side, both visible, Soft Light on",
                () => HasCoffeeCoverage(GetCoffeeFootage(false))),
            new GuidedPracticeLesson.Step(
                "You have matching coverage. Insert those cards into the computer. In the editor, try Wide -> Medium -> Close-Up, trimmed to <color=yellow>5 seconds each</color>, joined from 0 for a 15-second story. The order moves from context to connection to product. Add exactly 2 readable animated graphics; choose motion, transition and music in Branding. For a warm grade try Brightness 1.05, Contrast 1.15 and Saturation 1.15. Preview before export.",
                "Insert the matching Wide, Medium and Close-Up cards into the computer",
                () => HasCoffeeCoverage(GetCoffeeFootage(true)))
        };
        practiceLesson = new GuidedPracticeLesson(tutorialManager, steps, () =>
        {
            practiceLesson = null;
            TutorialUIManager.Instance?.HideTasks();
            if (tutorialManager != null) tutorialManager.UnfreezePlayerMovement();
        });
    }

    private static bool IsCoffeeBrown(Color color)
    {
        return color.r >= .3f && color.r > color.g && color.g > color.b && color.b <= .4f;
    }

    private static bool CoffeeSubjectsReady()
    {
        int products = 0;
        foreach (var product in FindObjectsOfType<CampaignProduct>())
            if (product.campaignLevel == 4) products++;
        var actors = FindObjectsOfType<CubeActor>();
        return products == 1 && actors.Length == 1 && actors[0].GetPoseName() != "Neutral";
    }

    private static bool CoffeeLightReady()
    {
        if (!CoffeeSubjectsReady()) return false;
        var actor = FindObjectOfType<CubeActor>();
        CampaignProduct coffee = null;
        foreach (var product in FindObjectsOfType<CampaignProduct>())
            if (product.campaignLevel == 4) coffee = product;
        Bounds bounds = new Bounds(actor.transform.position, Vector3.zero);
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        foreach (var renderer in coffee.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        foreach (var light in FindObjectsOfType<Player.Equipment.FilmLightItem>())
        {
            if (!light.IsPoweredOn() || light.spotlight == null ||
                (light.EquipmentName != "Level 3 Soft Light" && light.forcesHardLight)) continue;
            Vector3 offset = bounds.center - light.spotlight.transform.position;
            if (offset.magnitude <= 12f && Vector3.Dot(light.spotlight.transform.forward, offset.normalized) >= .45f)
                return true;
        }
        return false;
    }

    private static System.Collections.Generic.List<FootageData> GetCoffeeFootage(bool insertedOnly)
    {
        var clips = new System.Collections.Generic.List<FootageData>();
        foreach (var computer in FindObjectsOfType<ComputerStation>()) clips.AddRange(computer.GetInsertedFiles());
        if (!insertedOnly)
            foreach (var card in FindObjectsOfType<Player.Equipment.SDCardItem>(true))
                if (card.isUsedCard && card.videoDuration >= 5f)
                    clips.Add(new FootageData { fileName = card.recordedFileName, campaignLevel = card.campaignLevel,
                        shotType = card.shotType, screenDirection = card.screenDirection, actorPose = card.actorPose,
                        requiredSubjectsVisible = card.requiredSubjectsVisible, usedSoftLight = card.usedSoftLight });
        return clips;
    }

    // Match the grader's evidence; accept any complete matching trio, not just the first takes.
    internal static bool HasCoffeeCoverage(System.Collections.Generic.IEnumerable<FootageData> clips)
    {
        var groups = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var clip in clips)
        {
            if (clip == null || clip.campaignLevel != 4 || string.IsNullOrEmpty(clip.fileName) ||
                !clip.requiredSubjectsVisible || !clip.usedSoftLight || string.IsNullOrEmpty(clip.actorPose) ||
                clip.actorPose == "Neutral" || Mathf.Abs(clip.screenDirection) <= .1f || clip.shotType < 1 || clip.shotType > 3) continue;
            string key = clip.actorPose + (clip.screenDirection > 0 ? ":right" : ":left");
            groups.TryGetValue(key, out int coverage);
            coverage |= 1 << clip.shotType;
            if (coverage == 14) return true;
            groups[key] = coverage;
        }
        return false;
    }

    private void ShowLevelTasks()
    {
        if (TutorialUIManager.Instance == null) return;
        if (coffeeLessonStarted && practiceLesson != null)
        {
            practiceLesson.ShowCurrentTask();
            return;
        }
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


