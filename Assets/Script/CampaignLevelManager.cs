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
    private int megaphoneIntroductionPage;
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
            CoffeeStoryRules.Beat(card.actorPose) == 0 ? "Retake: hold Wave, Action (or Using Machine), or Sitting for the whole take." :
            "Collect this card. Next, record a different story beat.";
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

    public bool IsContract4PracticeActive => activeLevel == 4 && coffeeLessonStarted && practiceLesson != null;

    public bool CanUseContract4PracticeAction(string action)
    {
        if (!IsContract4PracticeActive) return true;
        return !practiceLesson.IsExplaining && practiceLesson.CurrentPermission == action;
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
                    "- Close the tablet; use the Director Megaphone for performances"
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

        if (currentStep == CampaignLevelStep.ActorPosed || currentStep == CampaignLevelStep.ActorPlaced)
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
                "- Place one actor, then close the tablet"
            });
        }
    }

    public void CloseBriefing()
    {
        AdvanceDialogue();
    }

    public void AdvanceDialogue()
    {
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.TryAdvanceBossDialoguePage()) return;
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
                TutorialUIManager.Instance.ShowBossDialogue("Those guides are there whenever you need them. Press <color=red>[P]</color> for a reminder on actor story beats and elliptical editing.", TutorialUIManager.Instance.poseHappy, true, false);
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
                "- Review greeting, coffee moment and seated break",
                "- Review the 15-second story edit",
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
            TutorialUIManager.Instance.ShowBossDialogue("Before your next brief, let's practice sets, actor blocking, and directing with the megaphone.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue(activeLevel == 4 ? "Let's add a practice chair and a free practice actor. Then buy the Director Megaphone at the Equipment Shop to try performance cues." : "Hire one actor with the tablet. Rookie, Trained and Expert offer different performance polish. Any tier can meet the brief; check the card's price.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void StartActorPractice()
    {
        currentStep = CampaignLevelStep.PracticeActor;
        isBriefingOpen = false;
        if (activeLevel == 4) { StartActorEnvironmentLesson(); return; }
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
            () => director != null && director.IsTerminalActive(), () => GuideCoffee(director, "tablet")));
        steps.Add(new GuidedPracticeLesson.Step(
            "Choose one Actor card. Move the Actor onto the stage and click to place them. Leave space for the product.",
            "Choose one Actor card, then click the stage to place them",
            () => currentStep == CampaignLevelStep.ActorPlaced || currentStep == CampaignLevelStep.ActorPosed,
            () => GuideCoffee(director, director != null && director.IsPlacingProp() ? "stage" : "actor")));
        steps.Add(new GuidedPracticeLesson.Step(
            "The tablet places and repositions actors. Performances and walking cues belong to the Director Megaphone, which we will use after placement.",
            "Actor placed: ready for megaphone direction",
            () => currentStep == CampaignLevelStep.ActorPlaced || currentStep == CampaignLevelStep.ActorPosed));
        steps.Add(new GuidedPracticeLesson.Step(
            "Close the tablet with <color=red>[E]</color>. We will direct the actor from the studio using the megaphone.",
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
            TutorialUIManager.Instance.ShowBossDialogue(activeLevel == 4 ? "Practice complete! Actors can also hold products: select an actor, aim at a product and click. [O] returns it. Now let's meet your client." : "Now our scene has someone in it. Leave the product visible, and keep the Actor\'s pose and screen side consistent when you change shots.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private bool ShowMegaphoneIntroduction()
    {
        var ui = TutorialUIManager.Instance;
        if (ui == null) return false;
        string[] pages =
        {
            "The Director Megaphone cues performances. Buy it at the shop for 900 B-Coins. Your camera and Soft Light can be reused.",
            "Press [E] to collect the megaphone from the director table. Its hotbar number equips it again; [G] drops it.",
            "Aim at your actor and click [LMB]. Press [Z] to cycle Neutral, Wave, Action, then back to Neutral.",
            "With an actor selected, aim at a stool or machine and click to cue them. [O] stops the action. We'll practice blocking and walk marks next."
        };
        if (megaphoneIntroductionPage >= pages.Length) return false;
        ui.ShowBossDialogue(pages[megaphoneIntroductionPage++], ui.posePoint, true, false);
        return true;
    }

    private void ShowContractIntroduction()
    {
        currentStep = CampaignLevelStep.IntroduceContract;

        if (TutorialUIManager.Instance == null) return;

        if (activeLevel == 4)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Kape Kultura wants a little story: greeting, coffee moment, seated break. Direct three different performances, then cut away the waiting. Your camera now unlocks exposure: F2, select ISO, Aperture or Shutter with Up/Down, then adjust with Left/Right. Watch the image brightness.", TutorialUIManager.Instance.poseOpenHand, true, false);
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
        if (activeLevel == 4)
        {
            FindObjectOfType<DirectorTerminal>()?.ClearContract4PracticeProps();
            featureActor = null;
            observedActorActions = 0;
        }
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
            TutorialUIManager.Instance.ShowBossDialogue("Open <color=red>[P]</color> when we finish chatting. Actor Blocking, the Coffee Story, and Elliptical Editing will help you plan this scene.", TutorialUIManager.Instance.posePoint, true, false);
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
                "- Review the Three-Beat Coffee Story",
                "- Review the Closing Brand Graphic",
                "- Review Elliptical Editing",
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
            TutorialUIManager.Instance.ShowBossDialogue("Tell three moments: Wave hello, Action with coffee, then Sitting for a break. Film each separately with actor and coffee visible. Press <color=red>[SPACE]</color> when you're ready.", TutorialUIManager.Instance.poseBoss, true, false);
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
        // The actor lesson runs before the contract offer; gameplay starts freely here.
    }

    private void GuideCoffee(DirectorTerminal director, string key)
    {
        var highlighter = TutorialHighlighter.Instance;
        RectTransform ui = null;
        Transform world = null;
        Bounds? furnitureBounds = null;
        var shop = FindObjectOfType<ShopTerminal>();
        if (key == "shop" || key == "cart" || key == "checkout")
        {
            if (shop != null && shop.IsTerminalActive())
                ui = key == "checkout" && shop.TutorialMegaphoneInCart && tutorialManager != null ? tutorialManager.shopCheckoutBtnRect : shop.GetTutorialCartTarget("DIRECTOR MEGAPHONE");
            else if (shop != null) world = shop.transform;
        }
        else if (key == "megaphone")
        {
            foreach (var item in FindObjectsOfType<Player.Equipment.ActorMegaphoneItem>())
                if (item.GetComponentInParent<Player.PlayerController.PlayerController>() == null) { world = item.transform; break; }
        }
        else if (key == "performer" || key == "seat" || key == "machine")
        {
            var held = HeldMegaphone();
            var actor = held != null && held.HasSelectedActor ? held.SelectedActor : FindObjectOfType<ActorBot>();
            if (actor != null) world = actor.transform;
            if (key != "performer" && held != null && held.HasSelectedActor)
            {
                float nearest = float.MaxValue;
                foreach (var item in FindObjectsOfType<Contract4Interactable>())
                {
                    if (item.action != (key == "seat" ? Contract4Interactable.Action.Sit : Contract4Interactable.Action.Machine)) continue;
                    var collider = item.GetComponent<Collider>();
                    if (collider == null || !collider.enabled) continue;
                    float distance = (item.Bounds.center - actor.transform.position).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance; furnitureBounds = item.Bounds; world = item.transform;
                }
            }
        }
        else if (key != "none" && director != null)
        {
            if (director.IsTerminalActive()) ui = director.GetTutorialTarget(key);
            else world = director.transform;
        }
        tutorialManager?.PointLineAtTransform(world);
        if (highlighter == null) return;
        if (ui != null && ui.gameObject.activeInHierarchy) { highlighter.HighlightElement(ui); return; }
        if (world != null)
        {
            Bounds bounds = furnitureBounds ?? new Bounds(world.position, Vector3.one * .4f);
            if (!furnitureBounds.HasValue)
            {
                bool first = true;
                foreach (var renderer in world.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
                    if (first) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                    first = false;
                }
            }
            highlighter.HighlightWorldBounds(bounds, Camera.main);
            return;
        }
        highlighter.HideHighlight();
    }

    private void StartActorEnvironmentLesson()
    {
        coffeeLessonStarted = true;
        var director = FindObjectOfType<DirectorTerminal>();
        var shop = FindObjectOfType<ShopTerminal>();
        GuidedPracticeLesson.Step Teach(string message, string task, System.Func<bool> done, string target, string permission = null)
        {
            if (permission == null)
                permission = target == "tablet" ? "tablet.open" : target == "none" ? "tablet.close" :
                    target == "set" ? "tablet.choose" : target == "practice-preview" ? "tablet.preview" :
                    target == "buy" ? "tablet.use" : target == "chair" ? "tablet.chair" : target == "actor" ? "tablet.actor" :
                    target == "stage" ? (task.Contains("[T]") ? "tablet.move" : "tablet.place") :
                    target == "megaphone" ? "megaphone.pickup" :
                    target == "cart" || target == "checkout" ? "shop.purchase" :
                    target == "seat" ? "megaphone.seat" :
                    task.Contains("[LMB]") || task.StartsWith("Equip megaphone") ? "megaphone.select" :
                    task.Contains("[X]") ? "megaphone.pose.x" : task.Contains("[C]") ? "megaphone.pose.c" :
                    task.Contains("[Z]") ? "megaphone.pose.z" : task.Contains("[R]") ? "megaphone.turn" :
                    task.Contains("[B]") ? "megaphone.mark.start" : task.Contains("[N]") ? "megaphone.mark.end" :
                    task.Contains("[K]") ? "megaphone.walk.rehearse" : task.Contains("[J]") ? "megaphone.walk.return" :
                    task.Contains("[H]") ? "megaphone.walk.clear" : task.Contains("[O]") ? "megaphone.stop" : "megaphone.use";
            return new GuidedPracticeLesson.Step(message, task, done, () => GuideCoffee(director, target), permission);
        }
        bool Placing() => director != null && director.IsPlacingProp();
        bool Pose(string pose)
        {
            var held = HeldMegaphone();
            var actor = held != null && held.SelectedActor != null ? held.SelectedActor.GetComponent<CubeActor>() : null;
            return actor != null && actor.GetPoseName() == pose;
        }
        var steps = new System.Collections.Generic.List<GuidedPracticeLesson.Step>
        {
            Teach("Let's try a small practice set. Open the highlighted tablet with [E].", "[E] Open the Director Tablet",
                () => director != null && director.IsTerminalActive(), "tablet"),
            Teach("Click CHAIR in Elements. This is our free practice chair.", "Click CHAIR in Elements",
                () => Placing(), "chair"),
            Teach("Move the chair onto the stage and click to place it. Leave room beside it for an actor.", "Click the stage to place the chair",
                () => director != null && director.TutorialChairPlaced && !Placing(), "stage"),
            Teach("Click an Actor card. We provide a practice actor before the client brief.", "Click an Actor card",
                () => Placing() || FindObjectOfType<CubeActor>() != null, "actor"),
            Teach("Move the actor onto a clear patch beside the chair, then click. This is blocking: planning a performer's position.", "Click the stage to place the Actor",
                () => !Placing() && FindObjectOfType<CubeActor>() != null, "stage"),
            Teach("Close the tablet with [E]. The tablet places actors; the megaphone cues performances.", "[E] Close the tablet",
                () => director != null && !director.IsTerminalActive(), "none"),
            Teach("Visit the Equipment Shop and buy the Director Megaphone for 900 B-Coins. Checkout delivers it to the studio.", "Buy the Director Megaphone and confirm checkout",
                () => shop != null && shop.MegaphonePurchasedThisSession, "checkout"),
            Teach("Pick up the delivered megaphone with [E]. It will equip in your hotbar.", "[E] Collect and equip the delivered megaphone",
                () => HeldMegaphone() != null, "megaphone"),
            Teach("Aim at your actor and click [LMB]. Selecting an actor tells the megaphone who to direct.", "[LMB] Select the highlighted Actor",
                () => HeldMegaphone() != null && HeldMegaphone().HasSelectedActor, "performer"),
            Teach("Press [Z] until Wave is selected. Watch how the gesture draws attention.", "[Z] Cycle to Wave",
                () => Pose("Wave"), "performer"),
            Teach("Press [Z] again to select Action. Watch the actor respond to your direction.", "[Z] Cycle to Action",
                () => { if (!Pose("Action")) return false; featureActor = HeldMegaphone().SelectedActor.GetComponent<CubeActor>(); actorFeatureRotation = featureActor.transform.rotation; actorFeaturePosition = featureActor.transform.position; return true; }, "performer"),
            Teach("Press [Z] to return to Neutral. Resetting the actor helps you prepare the next cue.", "[Z] Cue Neutral",
                () => Pose("Neutral"), "performer"),
            Teach("Press [R] to turn your selected actor toward the camera.", "[R] Turn the Actor", ObserveActorTurn, "performer"),
            Teach("Keep the megaphone equipped. Press [T] to reposition your selected actor.", "[T] Reposition Actor with megaphone", () => HeldMegaphone() != null && HeldMegaphone().IsRepositioning, "performer", "megaphone.reposition"),
            Teach("Aim at a nearby clear floor spot and click to place the actor. Leave room for gestures.", "[LMB] Place Actor on clear floor", () => ObserveActorMove() && HeldMegaphone() != null && !HeldMegaphone().IsRepositioning, "performer", "megaphone.place"),
            Teach("Equip the megaphone and click your actor again.", "Equip megaphone; [LMB] select Actor", () => HeldMegaphone() != null && HeldMegaphone().HasSelectedActor, "performer"),
            Teach("Press [B] to save START. Marks help an actor repeat movement between takes.", "[B] Save START",
                () => featureActor != null && featureActor.GetComponent<ActorBot>().HasStartMark, "performer"),
            Teach("Press [T] on the megaphone to choose where the walk ends.", "[T] Reposition Actor toward END", () => HeldMegaphone() != null && HeldMegaphone().IsRepositioning, "performer", "megaphone.reposition"),
            Teach("Aim at clear floor at least half a metre away and click to place the actor at END.", "[LMB] Place Actor at END", () => HeldMegaphone() != null && !HeldMegaphone().IsRepositioning, "performer", "megaphone.place"),
            Teach("Equip the megaphone and select the actor.", "Equip megaphone; [LMB] select Actor", () => HeldMegaphone() != null && HeldMegaphone().HasSelectedActor, "performer"),
            Teach("Press [N] to save END. You now have a repeatable route.", "[N] Save END",
                () => featureActor != null && featureActor.GetComponent<ActorBot>().HasWalk, "performer"),
            Teach("Press [K] and watch the whole walk. Rehearsal reveals blocking problems before filming.", "[K] Rehearse; wait for the Actor to reach END",
                () => featureActor != null && featureActor.GetComponent<ActorBot>().WalkCompleted, "performer"),
            Teach("Press [J] to return the actor to START for another take.", "[J] Return to START",
                () => featureActor != null && featureActor.GetComponent<ActorBot>().ReturnedAfterWalk, "performer"),
            Teach("Press [H] to clear the walking route before furniture practice.", "[H] Clear the Actor's walking route",
                () => featureActor != null && !featureActor.GetComponent<ActorBot>().HasWalk, "performer"),
            Teach("Select the actor with your megaphone. Aim at the highlighted chair and click [LMB] to seat them.", "[LMB] Cue the Actor to sit on the highlighted chair",
                () => Pose("Sitting"), "seat"),
            Teach("Press [O] to stop sitting. The actor returns to their previous mark.", "[O] Stop sitting",
                () => HeldMegaphone() != null && HeldMegaphone().HasSelectedActor && HeldMegaphone().SelectedActor.FurniturePoseName == null, "performer")
        };
        practiceLesson = new GuidedPracticeLesson(tutorialManager, steps, () =>
        {
            practiceLesson = null;
            TutorialUIManager.Instance?.HideTasks();
            tutorialManager?.PointLineAtTransform(null);
            ShowActorPracticeComplete();
        });
    }

    private static bool IsCoffeeBrown(Color color)
    {
        return color.r >= .3f && color.r > color.g && color.g > color.b && color.b <= .4f;
    }

    private static bool HasOwnedMegaphone()
    {
        return PlayerPrefs.GetInt("OwnedEquipment.DIRECTOR MEGAPHONE", 0) > 0;
    }

    private static Player.Equipment.ActorMegaphoneItem HeldMegaphone()
    {
        foreach (var megaphone in FindObjectsOfType<Player.Equipment.ActorMegaphoneItem>())
            if (megaphone.GetComponentInParent<Player.Interactor.EquipmentInteractor>() != null) return megaphone;
        return null;
    }

    private static bool CoffeeSubjectsReady(bool requirePose = true)
    {
        int products = 0;
        foreach (var product in FindObjectsOfType<CampaignProduct>())
            if (product.campaignLevel == 4) products++;
        var actors = FindObjectsOfType<CubeActor>();
        return products == 1 && actors.Length == 1 && (!requirePose || actors[0].GetPoseName() != "Neutral");
    }

    private static Player.Equipment.FilmLightItem HeldCoffeeLight()
    {
        foreach (var light in FindObjectsOfType<Player.Equipment.FilmLightItem>())
            if (light.EquipmentName == "Level 3 Soft Light" && light.GetComponentInParent<Player.PlayerController.PlayerController>() != null) return light;
        return null;
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
            if (!light.IsPoweredOn() || light.GetDiffusionPercent() < 50 || light.spotlight == null ||
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
    internal static bool HasCoffeeCoverage(System.Collections.Generic.IEnumerable<FootageData> clips, int requiredCoverage = 14)
    {
        int coverage = 0;
        foreach (var clip in clips)
            if (clip != null && clip.campaignLevel == 4 && !string.IsNullOrEmpty(clip.fileName) && clip.requiredSubjectsVisible)
            {
                int beat = CoffeeStoryRules.Beat(clip.actorPose);
                if (beat > 0) coverage |= 1 << beat;
            }
        return (coverage & requiredCoverage) == requiredCoverage;
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

        shopTerminal.RestoreProductionCamera();

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


