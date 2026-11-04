using System.Collections;
using UnityEngine;
using PlayerPrefs=GameSavePrefs;

public partial class TutorialManager
{
    private IEnumerator BeginSelectedContract(int level)
    {
        if(ContractTutorialChoice.NeedsPrompt(level))
        {
            StudioArrivalTour.BeginWelcomeTour(level);
            while(StudioArrivalTour.IsPendingOrPlaying)yield return null;
        }
        if(level!=5)yield return ContractTutorialChoice.Ask(this,level);
        if(level==2)
        {
            var manager=GetComponent<GokeLevelManager>()??gameObject.AddComponent<GokeLevelManager>();manager.BeginLevel(this);
        }
        else if(level==3)
        {
            var manager=GetComponent<Level3Manager>()??gameObject.AddComponent<Level3Manager>();manager.BeginLevel(this);
        }
        else
        {
            var manager=GetComponent<CampaignLevelManager>()??gameObject.AddComponent<CampaignLevelManager>();manager.BeginLevel(this,level);
        }
    }
    private void GrantFirstContractBudget()
    {
        int budgetToAdd=ProductionEconomy.StartingBudget;
        if(isLevel1Retry)budgetToAdd=Mathf.Max(0,ProductionEconomy.StartingBudget-PlayerPrefs.GetInt("PlayerMoney",0));
        else if(PlayerPrefs.GetInt("Level1StartingBudgetGranted",0)==1)budgetToAdd=0;
        if(CareerManager.Instance!=null && budgetToAdd>0)CareerManager.Instance.AddMoney(budgetToAdd,"Training allowance");
        else if(budgetToAdd>0)
        {
            PlayerAnalytics.Begin(1);PlayerAnalytics.TransactionMade(budgetToAdd,"Training allowance","Training allowance");
            PlayerPrefs.SetInt("PlayerMoney",PlayerPrefs.GetInt("PlayerMoney",0)+budgetToAdd);
        }
        PlayerPrefs.SetInt("Level1StartingBudgetGranted",1);PlayerPrefs.Save();
        if(budgetToAdd>0)GameSaveManager.Instance?.SaveBudgetCheckpoint();
    }
    private void StartUnguidedFirstContract()
    {
        isLevel1Retry=PlayerPrefs.GetInt("Level1RetryActive",0)==1;
        DisableForDevTesting();
        if(ContractUIManager.Instance==null)new GameObject("Contract UI Manager").AddComponent<ContractUIManager>();
        if(PlayerPrefs.GetInt("FlowerContractAccepted",0)==1)
        {ContractUIManager.Instance.PrepareFlowerContract();AcceptUnguidedFirstContract();}
        else ContractUIManager.Instance.ShowFlowerContract(AcceptUnguidedFirstContract);
    }
    private void AcceptUnguidedFirstContract()
    {
        PlayerPrefs.SetInt("FlowerContractAccepted",1);
        GrantFirstContractBudget();
        if(CareerManager.Instance!=null)CareerManager.Instance.currentActiveJob=CampaignProgression.GetContractName(1);
        if(AlmanacManager.Instance!=null)AlmanacManager.Instance.UnlockLevel1Knowledge();
        ContractUIManager.Instance?.UnlockQualifications();
        UnfreezePlayerMovement();Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
    }
}
