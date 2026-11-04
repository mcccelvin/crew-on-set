using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Applies the owning player's public equipped IDs to this player's existing
// animated skeleton. Never reads another player's outfit from local preferences.
public sealed class MultiplayerCharacterAppearance : MonoBehaviourPunCallbacks
{
    private GameObject visual;
    private SkinnedMeshRenderer[] originals;
    private bool[] originalStates;
    private readonly List<GameObject> wearing = new List<GameObject>();
    private CCoinService cosmetics;
    private string applied;

    public void Initialize(GameObject character)
    {
        visual=character;
        originals=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        originalStates=new bool[originals.Length];
        for(int i=0;i<originals.Length;i++)originalStates[i]=originals[i].enabled;
        if(photonView.IsMine)
        {
            cosmetics=CCoinService.Ensure();
            cosmetics.Changed+=Refresh;
        }
        Refresh();
    }
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player target,Hashtable changed)
    {
        if(target!=null && target.ActorNumber==photonView.OwnerActorNr && changed.ContainsKey(CrewPlayerIdentity.OutfitKey))Refresh();
    }
    private void Refresh()
    {
        if(visual==null || originals==null)return;
        string[] parts=photonView.IsMine && cosmetics!=null ? cosmetics.EquippedParts : CrewPlayerIdentity.Outfit(photonView.Owner);
        Array.Sort(parts,StringComparer.Ordinal);
        string key=string.Join("|",parts);
        if(applied==key)return;
        var rig=CharacterCosmeticRig.Load();
        if(rig==null)return;
        foreach(var part in wearing)if(part!=null){part.SetActive(false);Destroy(part);}
        wearing.Clear();
        RestoreDefault();
        bool hasBody=Array.Exists(parts,id=>CharacterCosmetics.Find(id)?.kind=="body" && rig.Contains(id));
        if(hasBody)
        {
            int count=rig.Apply(visual,parts);
            if(count==0)
            {
                RestoreDefault();
                Debug.LogWarning("The equipped outfit could not bind to this multiplayer character. Default appearance retained.",this);
            }
            else
            {
                var defaults=new HashSet<SkinnedMeshRenderer>(originals);
                foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(!defaults.Contains(skin) && skin.gameObject.activeSelf)wearing.Add(skin.gameObject);
            }
        }
        // First person retains body shadows; other players see the complete outfit.
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.gameObject.layer=gameObject.layer;
            renderer.shadowCastingMode=photonView.IsMine?UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly:UnityEngine.Rendering.ShadowCastingMode.On;
        }
        applied=key;
    }
    private void RestoreDefault()
    {
        for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].enabled=originalStates[i];
    }
    private void OnDestroy()
    {
        if(cosmetics!=null)cosmetics.Changed-=Refresh;
    }
}
