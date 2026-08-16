using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GokeLevelManager : MonoBehaviour
{
    public static GokeLevelManager Instance;

    private enum GokeLevelStep
    {
        Recap,
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
        IntroduceAlmanac,
        OpenAlmanac,
        CloseAlmanac,
        IntroduceContract,
        OfferContract,
        IntroduceTechniques,
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
    private GokeLevelStep currentStep;
    private TutorialManager tutorialManager;
    private ContractUIManager contractUIManager;
    private int level2CameraItemIndex = -1;
    private int sdCardItemIndex = -1;
    private bool hasPickedUpLevel2Camera = false;
    private bool hasPickedUpSDCard = false;

    private string[] gokeTasks = new string[]
    {
        "- STAGE: Build a <color=red>RED</color> backdrop and move Goke Cola away from the wall",
        "- CAMERA: Frame Goke Cola on the left or right third",
        "- LIGHT: Set up a Key, Fill, and Back Light",
        "- EDIT: Add 3 graphics and use strong contrast"
    };

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
        currentStep = GokeLevelStep.Recap;

        CampaignProgression.SetCurrentLevel(2);

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        CleanUpStudio();
        SetupLevel2Camera();
        SetupContractUI();

        if (PlayerPrefs.GetInt("GokeContractAccepted", 0) == 1)
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
            TutorialUIManager.Instance.ShowBossDialogue("Welcome back! You completed your first commercial from start to finish: You built the set, arranged the props, shaped the lighting, recorded a 10-second shot, and finished the edit with branding and color grading.", TutorialUIManager.Instance.poseHappy, true, false);
        }

        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        StartCoroutine(UnlockPlayerAfterSpace());
    }

    public void CloseBriefing()
    {
        if (!isBriefingOpen) return;

        if (currentStep == GokeLevelStep.Recap)
        {
            ShowCameraIntroduction();
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
            ShowAlmanacIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceAlmanac)
        {
            StartAlmanacIntroduction();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceContract)
        {
            OfferContract();
            return;
        }

        if (currentStep == GokeLevelStep.OfferContract)
        {
            AcceptContract();
            return;
        }

        if (currentStep == GokeLevelStep.IntroduceTechniques)
        {
            StartQualificationsIntroduction();
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
        return isBriefingOpen;
    }

    public bool IsEquipmentIntroductionActive()
    {
        return isLevelStarted && currentStep != GokeLevelStep.LevelActive;
    }

    public bool CanOpenAlmanac()
    {
        return currentStep == GokeLevelStep.OpenAlmanac ||
               currentStep == GokeLevelStep.CloseAlmanac ||
               currentStep == GokeLevelStep.IntroduceTechniques ||
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
        if (currentStep == GokeLevelStep.BuyCamera)
        {
            if (itemIndex != level2CameraItemIndex)
            {
                if (tutorialManager != null) tutorialManager.ShowWarning("Add the Level 2 Camera first!");
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
                if (tutorialManager != null) tutorialManager.ShowWarning("Now add an SD Card to your cart!");
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
            if (tutorialManager != null) tutorialManager.ShowWarning("You have everything you need. Confirm the purchase!");
            return false;
        }

        if (tutorialManager != null) tutorialManager.ShowWarning("Follow the current task first!");
        return false;
    }

    public bool CanConfirmPurchase()
    {
        if (currentStep == GokeLevelStep.Checkout) return true;

        if (currentStep == GokeLevelStep.BuyCamera)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Add the Level 2 Camera before checkout!");
            return false;
        }

        if (currentStep == GokeLevelStep.BuySDCard)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Add an SD Card before checkout!");
            return false;
        }

        return true;
    }

    public bool CanCancelPurchase()
    {
        if (currentStep != GokeLevelStep.BuyCamera &&
            currentStep != GokeLevelStep.BuySDCard &&
            currentStep != GokeLevelStep.Checkout) return true;

        if (tutorialManager != null)
        {
            bool alreadyOwnsCamera = PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1;
            tutorialManager.ShowWarning(alreadyOwnsCamera ? "Keep the SD Card in your cart, then confirm the purchase!" : "Keep the Camera and SD Card in your cart, then confirm the purchase!");
        }
        return false;
    }

    public bool CanInsertSDCard(string equipmentName)
    {
        if (currentStep != GokeLevelStep.InsertSDCard)
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Follow the current task first!");
            return false;
        }

        if (equipmentName != "Level 2 Camera")
        {
            if (tutorialManager != null) tutorialManager.ShowWarning("Insert the SD Card into the Level 2 Camera!");
            return false;
        }

        return true;
    }

    public void OnShopOpened()
    {
        if (currentStep != GokeLevelStep.BuyCamera && currentStep != GokeLevelStep.BuySDCard) return;

        if (TutorialUIManager.Instance != null)
        {
            if (currentStep == GokeLevelStep.BuyCamera)
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add the Level 2 Camera to your cart", "- Add an SD Card before checkout" });
            else
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Add a blank SD Card to your cart and confirm purchase" });
        }
    }

    public void OnEquipmentBought()
    {
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
        if (currentStep != GokeLevelStep.CloseShop) return;

        currentStep = GokeLevelStep.IntroducePickup;
        isBriefingOpen = true;

        if (tutorialManager != null) tutorialManager.PointLineAt("");

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Purchase complete. The shop delivered both items to the delivery table. Pick up the <color=yellow>Level 2 Camera</color> first, then I will show you how to prepare it.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    public void OnAlmanacOpened()
    {
        if (currentStep == GokeLevelStep.OpenAlmanac)
        {
            currentStep = GokeLevelStep.CloseAlmanac;

            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Review the equipment guides", "- Press <color=red>[P]</color> or CLOSE when finished" });
            }
            return;
        }

    }

    public void OnAlmanacClosed()
    {
        if (currentStep == GokeLevelStep.CloseAlmanac)
        {
            ShowContractIntroduction();
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
                tutorialManager.ShowWarning("Pick up the Level 2 Camera from the delivery table!");
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
            TutorialUIManager.Instance.ShowBossDialogue("SD Card inserted. This camera has a new production viewfinder. Click <color=red>[Left Mouse Button]</color> to look through it and inspect the new guides.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    public void OnCameraViewEntered(string equipmentName)
    {
        if (currentStep != GokeLevelStep.IntroduceCameraView && currentStep != GokeLevelStep.OpenCameraView) return;
        if (equipmentName != "Level 2 Camera") return;

        currentStep = GokeLevelStep.InspectCameraFeatures;
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[]
            {
                "- Find the Rule of Thirds grid",
                "- Find the green subject-tracking box",
                "- Watch the live focus-distance display",
                "- Click <color=red>[Left Mouse Button]</color> again when finished"
            });
        }
    }

    public void OnCameraViewExited(string equipmentName)
    {
        if (currentStep != GokeLevelStep.InspectCameraFeatures) return;
        if (equipmentName != "Level 2 Camera") return;

        currentStep = GokeLevelStep.ExplainCameraFeatures;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("You found the new tools. The grid helps you place a subject on the left or right third, the tracking box follows the product, and the focus display confirms the autofocus distance. Those features will be important for your next contract.", TutorialUIManager.Instance.poseHappy, true, false);
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

        if (TutorialUIManager.Instance != null)
        {
            bool alreadyOwnsCamera = PlayerPrefs.GetInt("Level2CameraPurchased", 0) == 1;
            string message = alreadyOwnsCamera
                ? "Your <color=yellow>Level 2 Camera</color> is still yours and has been returned to the delivery table. Buy one blank SD Card from the Equipment Shop so we can prepare it for this contract."
                : "Before your next assignment, I've unlocked the <color=yellow>Level 2 Camera</color>. It costs <color=yellow>10,000 B-Coins</color> and adds autofocus, subject tracking, detailed recording feedback, and a Rule of Thirds grid. Buy the camera and one blank SD Card from the Equipment Shop.";
            TutorialUIManager.Instance.ShowBossDialogue(message, TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void ShowAlmanacIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceAlmanac;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Your Production Almanac has also been updated. It contains the controls and features for the Director Tablet, LED Panel, NONY FX Camera, SD Card, and your new Level 2 Camera. Press <color=red>[P]</color> after this message to open it and review the new camera entry.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void StartAlmanacIntroduction()
    {
        isBriefingOpen = false;

        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();

        if (AlmanacManager.Instance == null)
        {
            OfferContract();
            return;
        }

        currentStep = GokeLevelStep.OpenAlmanac;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[P]</color> to open the Production Almanac" });
        }
    }

    private void OfferContract()
    {
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
                TutorialUIManager.Instance.ShowBossDialogue("Goke Cola is offering a 60,000 B-Coin contract requiring a RED stage, Rule of Thirds, 3-Point Lighting, three graphics, and a high-contrast edit. Press Space to accept.", TutorialUIManager.Instance.poseBoss, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("A new contract just arrived from <color=yellow>Goke Cola</color>. This will not use the same centered composition and single-light setup as your Flower Vase commercial. The client wants a different stage, <color=yellow>Rule of Thirds</color> framing, and a full <color=yellow>3-Point Lighting</color> setup. Review the contract board carefully before accepting.", TutorialUIManager.Instance.poseBoss, true, false);
        }
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
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Contract accepted. Rule of Thirds and 3-Point Lighting are now available in your Almanac. You can also press [TAB] during this contract to open the qualification sheet. Let's review it once before you begin.", TutorialUIManager.Instance.posePointUp, true, false);
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
            TutorialUIManager.Instance.ShowBossDialogue("That's the new camera. It still needs recording media, so pick up the <color=yellow>SD Card</color> from the delivery table next.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void ShowSDCardInsertionIntroduction()
    {
        currentStep = GokeLevelStep.IntroduceSDInsert;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Now equip the Level 2 Camera and press <color=red>[C]</color> to insert the blank SD Card. The viewfinder and recording controls stay locked until the camera has media.", TutorialUIManager.Instance.poseBoss, true, false);
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

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Click <color=red>[Left Mouse Button]</color> to open the Level 2 Camera viewfinder" });
        }
    }

    private void StartProductionTutorial()
    {
        currentStep = GokeLevelStep.ExplainStage;
        isBriefingOpen = true;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Now let's plan the Goke Cola shoot. First, open the Director Tablet, build a backdrop, and paint it <color=red>RED</color>. Spawn the Goke Cola prop, then drag it forward so it is clearly separated from the wall.", TutorialUIManager.Instance.posePointUp, true, false);
        }
    }

    private void ShowCompositionTutorial()
    {
        currentStep = GokeLevelStep.ExplainComposition;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Next is composition. Look through the Level 2 Camera and place Goke Cola near the <color=yellow>left or right vertical third</color> of the frame. Do not leave the product directly in the center. Press <color=red>[TAB]</color> after the briefing whenever you need to review the qualification sheet.", TutorialUIManager.Instance.posePoint, true, false);
        }
    }

    private void ShowLightingTutorial()
    {
        currentStep = GokeLevelStep.ExplainLighting;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Now build a 3-Point Lighting setup. Use a strong <color=yellow>Key Light</color> from one side, a softer <color=yellow>Fill Light</color> from the opposite side, and a <color=yellow>Back Light</color> behind the Cola. The back light should separate the product from the red backdrop.", TutorialUIManager.Instance.poseOpenHand, true, false);
        }
    }

    private void ShowPostProductionTutorial()
    {
        currentStep = GokeLevelStep.ExplainPostProduction;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("After the set, camera, and lighting are ready, record your take and bring the SD Card to the editing computer. Goke Cola wants <color=yellow>three graphics</color> and a bold, high-contrast finish. Keep the colors vibrant, but do not raise contrast so far that the shadows are crushed.", TutorialUIManager.Instance.poseBoss, true, false);
        }
    }

    private void ShowContractBriefing()
    {
        currentStep = GokeLevelStep.ContractBriefing;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("That's the full production plan. I will pin the four contract objectives on your screen. Complete them in order, and press <color=red>[TAB]</color> whenever you need to review Rule of Thirds or 3-Point Lighting. Press Space when you are ready to begin.", TutorialUIManager.Instance.poseHappy, true, false);
        }
    }

    private void StartContract()
    {
        isBriefingOpen = false;
        currentStep = GokeLevelStep.LevelActive;

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.SetupTasks(gokeTasks);
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

            if (sdCardItemIndex == -1) Debug.LogWarning("SD Card could not be found in the Equipment Shop.");
        }
        else
        {
            Debug.LogWarning("Level 2 Camera could not be added to the Equipment Shop.");
        }
    }

    private void SetupContractUI()
    {
        contractUIManager = FindObjectOfType<ContractUIManager>();
        if (contractUIManager == null) contractUIManager = gameObject.AddComponent<ContractUIManager>();
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
