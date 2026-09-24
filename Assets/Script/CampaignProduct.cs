using UnityEngine;

public class CampaignProduct : MonoBehaviour
{
    public int campaignLevel = 4;
    public Transform Holder { get; private set; }
    public bool TryClaim(Transform holder)
    {
        if (holder == null || Holder != null) return false;
        Holder = holder;
        return true;
    }
    public void Release(Transform holder)
    {
        if (Holder == holder) Holder = null;
    }
    private void Start()
    {
        if (campaignLevel == 4) Contract4Interactable.Attach(gameObject, Contract4Interactable.Action.Product);
    }
}
