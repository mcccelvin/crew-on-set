using Photon.Pun;
using UnityEngine;
using System.Linq;

public sealed class NetworkStageObject : MonoBehaviour
{
    public int Id, Revision;
    public ActorBot Bot => GetComponent<ActorBot>();
    private CrewObject data;
    private Renderer[] visuals;
    public void Apply(CrewObject item, NetworkStudioFactory factory)
    {
        if (Revision == item.revision) return;
        Revision = item.revision; data = JsonUtility.FromJson<CrewObject>(JsonUtility.ToJson(item));
        var bot = Bot;
        if (bot != null) bot.StopFurnitureAction();
        transform.SetPositionAndRotation(item.position, Quaternion.Euler(item.rotation));
        if (visuals == null) visuals = GetComponentsInChildren<Renderer>(true);
        if (item.kind == "backdrop")
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in visuals)
            {
                string name = renderer.name.ToLowerInvariant();
                if (name.Contains("stand") || name.Contains("pole") || name.Contains("tripod")) continue;
                renderer.GetPropertyBlock(block); block.SetColor("_BaseColor", item.color); block.SetColor("_Color", item.color); renderer.SetPropertyBlock(block);
            }
        }
        if (item.kind == "light")
        {
            var lamp = transform.Find("Crew Spotlight")?.GetComponent<Light>();
            if (lamp != null) { lamp.enabled = item.powered; lamp.intensity = item.intensity; lamp.color = Mathf.CorrelatedColorTemperatureToRGB(item.kelvin); lamp.spotAngle = Mathf.Lerp(25, 100, item.diffusion / 100); }
        }
        if (bot == null) return;
        bot.SetCrewMotion(item.position, Quaternion.Euler(item.rotation), false);
        bot.SetPerformance(item.performance);
        if (item.action == "furniture") bot.PerformFurnitureAction(factory.Interaction(item.target, item.targetIndex));
        if (item.heldProduct != 0 && factory.Objects.TryGetValue(item.heldProduct, out var product))
        {
            bot.HoldProduct(product.GetComponent<CampaignProduct>());
            if (item.performance == 2) bot.DrinkCoffee();
        }
    }
    private void Update()
    {
        if (data != null && MultiplayerRoomActions.IsEquipment(data.kind))
        {
            var crew = MultiplayerRoleManager.Instance;
            var member = crew?.State?.members.Find(m => m.id == data.holder);
            bool visible = data.loadedInto == 0 && (data.holder == 0 || member != null && member.equipped == data.id);
            foreach (var renderer in visuals) renderer.enabled = visible;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = data.holder == 0 && data.loadedInto == 0;
            if (data.holder != 0)
            {
                var player = FindObjectsOfType<MultiplayerPlayerController>().FirstOrDefault(p => p.photonView.OwnerActorNr == data.holder);
                if (player != null)
                {
                    Transform anchor = player.photonView.IsMine && player.playerCamera != null ? player.playerCamera.transform : player.transform;
                    transform.position = anchor.TransformPoint(new Vector3(.35f, player.photonView.IsMine ? -.32f : 1.2f, .8f));
                    transform.rotation = anchor.rotation;
                }
            }
        }
        if (data == null || data.action != "walk" || Bot == null) return;
        float distance = Vector3.Distance(data.start, data.end);
        float travel = Mathf.Max(0, (float)(PhotonNetwork.Time - data.walkTime)) * .9f;
        var direction = data.end - data.start;
        Bot.SetCrewMotion(Vector3.MoveTowards(data.start, data.end, travel), direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : transform.rotation, travel < distance);
    }
}
