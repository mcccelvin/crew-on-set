using Photon.Pun;
using UnityEngine;

public sealed class NetworkStageObject : MonoBehaviour
{
    public int Id, Revision;
    public ActorBot Bot => GetComponent<ActorBot>();
    private CrewObject data;
    public void Apply(CrewObject item, NetworkStudioFactory factory)
    {
        if (Revision == item.revision) return;
        Revision = item.revision; data = JsonUtility.FromJson<CrewObject>(JsonUtility.ToJson(item));
        var bot = Bot;
        if (bot != null) bot.StopFurnitureAction();
        transform.SetPositionAndRotation(item.position, Quaternion.Euler(item.rotation));
        if (item.kind == "light")
        {
            var lamp = transform.Find("Crew Spotlight")?.GetComponent<Light>();
            if (lamp != null) { lamp.enabled = item.powered; lamp.intensity = item.intensity; lamp.color = Mathf.CorrelatedColorTemperatureToRGB(item.kelvin); lamp.spotAngle = Mathf.Lerp(25, 100, item.diffusion / 100); }
        }
        if (bot == null) return;
        bot.SetCrewMotion(item.position, Quaternion.Euler(item.rotation), false);
        bot.SetPerformance(item.performance);
        if (item.action == "furniture") bot.PerformFurnitureAction(factory.Interaction(item.target, item.targetIndex));
        if (item.heldProduct != 0 && factory.Objects.TryGetValue(item.heldProduct, out var product)) bot.HoldProduct(product.GetComponent<CampaignProduct>());
    }
    private void Update()
    {
        if (data == null || data.action != "walk" || Bot == null) return;
        float distance = Vector3.Distance(data.start, data.end);
        float travel = Mathf.Max(0, (float)(PhotonNetwork.Time - data.walkTime)) * .9f;
        var direction = data.end - data.start;
        Bot.SetCrewMotion(Vector3.MoveTowards(data.start, data.end, travel), direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : transform.rotation, travel < distance);
    }
}
