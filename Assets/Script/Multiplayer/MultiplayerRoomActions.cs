using System.Linq;
using Photon.Pun;
using UnityEngine;

// Called only by the master. UI restrictions are repeated at the command boundary.
public static class MultiplayerRoomActions
{
    public static bool IsEquipment(string kind) => kind == "camera" || kind == "megaphone" || kind == "sd" || kind == "light" || kind == "softlight" || kind == "audio";
    public static bool Holding(CrewSession s, int player, string kind) => s.objects.Any(o => o.holder == player && o.kind == kind);
    public static bool Equipped(CrewSession s, int player, string kind) => s.members.Any(m => m.id == player && s.objects.Any(o => o.id == m.equipped && o.holder == player && o.kind == kind));
    public static bool Handle(MultiplayerRoleManager manager, CrewCommand c, int sender, out string error)
    {
        error = null;
        var s = manager.State;
        var member = s.members.Find(m => m.id == sender);
        if (c.action == "storeEquipment" || c.action == "retrieveEquipment")
        {
            var cabinet=EquipmentStorageCabinet.Active;
            var player=Object.FindObjectsOfType<MultiplayerPlayerController>().FirstOrDefault(p=>p.photonView.OwnerActorNr==sender);
            var item=s.objects.Find(o=>o.id==c.id);
            if(member==null || cabinet==null || player==null || !cabinet.CanReach(player.transform.position))
            {error="Move closer to the equipment cabinet.";return true;}
            if(item==null || !IsEquipment(item.kind) || !manager.HasRole(sender,MultiplayerContractManager.RoleFor(item.kind)))
            {error="That equipment belongs to another role.";return true;}
            if(s.recording || item.loadedInto!=0)
            {error="Stop recording and eject any loaded SD card before storing equipment.";return true;}
            if(c.action=="storeEquipment")
            {
                if(item.holder!=sender || item.action=="stored")
                {error="Pick up the equipment before putting it away.";return true;}
                if(s.objects.Any(o=>o.loadedInto==item.id))
                {error="Eject the SD card [C] before storing the camera.";return true;}
                item.holder=item.slot=0;item.action="stored";item.powered=false;
                item.position=cabinet.transform.position;
                if(member.equipped==item.id)member.equipped=0;
                s.message="Equipment stored in the cabinet.";
            }
            else
            {
                if(item.action!="stored" || item.holder!=0)
                {error="This item is no longer in storage.";return true;}
                int slot=Enumerable.Range(1,5).FirstOrDefault(n=>!s.objects.Any(o=>o.holder==sender && o.slot==n));
                if(slot==0){error="Your hotbar is full. Store an item first.";return true;}
                item.holder=sender;item.slot=slot;item.action="idle";member.equipped=item.id;
                s.message="Equipment added to your hotbar.";
            }
            item.revision++;return true;
        }
        if (c.action == "checkout")
        {
            if (member == null) { error = "Join the crew before purchasing equipment."; return true; }
            // Retransmitting an acknowledged purchase must never charge the crew twice.
            if (c.index > 0 && c.index <= member.shopReceipt) return true;
            if (c.index != member.shopReceipt + 1) { error = "Refresh the shop before purchasing again."; return true; }
            if (s.recording || c.items == null || c.items.Length == 0 || c.items.Length > 48 || s.objects.Count + c.items.Length > 48)
            { error = "Finish the take and make space for the equipment before checkout."; return true; }
            if (c.items.Any(k => !IsEquipment(k) || !manager.HasRole(sender, MultiplayerContractManager.RoleFor(k))))
            { error = "Your role cannot purchase one of these items."; return true; }
            if (c.items.Any(k => !MultiplayerContractManager.ShopItemAvailable(k, s.contractLevel)))
            { error = "This equipment isn't available for the current contract."; return true; }
            if (c.items.Any(k => MultiplayerContractManager.ShopItemOwned(s, k)) ||
                c.items.GroupBy(k => k).Any(group => MultiplayerContractManager.UniqueShopItem(group.Key) && group.Count() > 1))
            { error = "The camera or megaphone is already owned or already in your cart."; return true; }
            if (c.items.Any(k => !manager.Studio.CanCreate(k))) { error = "An equipment model is missing. No money spent."; return true; }
            int cost = c.items.Sum(MultiplayerContractManager.Price);
            if (s.budget < cost) { error = "Not enough team B-Coins."; return true; }
            var refs = Resources.Load<GameObject>("CrewUI/Studio")?.GetComponent<MultiplayerUIReferences>();
            var shop = Object.FindObjectsOfType<ShopTerminal>(true).FirstOrDefault(terminal => terminal.gameObject.scene == manager.gameObject.scene);
            if ((shop == null || shop.deliveryZone == null) && refs == null)
            { error = "The shop delivery point is missing. No money spent."; return true; }
            Vector3 delivery = shop != null && shop.deliveryZone != null ? shop.deliveryZone.position : refs.deliveryPosition;
            s.budget -= cost;
            CrewProductionResults.BudgetChanged(s, -cost);
            foreach (string kind in c.items)
            {
                int offset = s.objects.Count(o => o.action == "delivery");
                s.objects.Add(new CrewObject { id = s.nextId++, revision = 1, kind = kind, action = "delivery",
                    position = shop != null && shop.deliveryZone != null ? StudioDeliveryBay.Position(shop.deliveryZone,offset) :
                        delivery + new Vector3((offset % 3 - 1) * .45f, .25f + offset / 3 * .2f, 0) });
                if (!s.equipment.Contains(kind)) s.equipment.Add(kind);
            }
            member.shopReceipt = c.index;
            s.message = StudioDeliveryBay.CollectionMessage; return true;
        }
        if (c.action == "equip")
        {
            var equipped = s.objects.Find(o => o.id == c.id && o.holder == sender && o.loadedInto == 0);
            member.equipped = equipped != null ? equipped.id : 0; return true;
        }
        if (c.action == "pickup" || c.action == "drop" || c.action == "insertCard" || c.action == "deliverTape")
        {
            var item = s.objects.Find(o => o.id == c.id);
            if (item == null || !IsEquipment(item.kind) || !manager.HasRole(sender, MultiplayerContractManager.RoleFor(item.kind)))
            { error = "That equipment belongs to another role."; return true; }
            if (s.recording) { error = "Stop recording before handling equipment."; return true; }
            if (c.action == "pickup")
            {
                if (item.holder != 0 || item.loadedInto != 0 || item.action == "stored") { error = "Use the cabinet for stored equipment; this item may already be in use."; return true; }
                var player = Object.FindObjectsOfType<MultiplayerPlayerController>().FirstOrDefault(p => p.photonView.OwnerActorNr == sender);
                if (player == null || Vector3.Distance(player.transform.position, item.position) > 5) { error = "Move closer to pick it up."; return true; }
                int slot = Enumerable.Range(1, 5).FirstOrDefault(n => !s.objects.Any(o => o.holder == sender && o.slot == n));
                if (slot == 0) { error = "Hotbar full. Drop an item first."; return true; }
                item.holder = sender; item.slot = slot; member.equipped = item.id;
            }
            else if (item.holder != sender) { error = "Pick up the item first."; return true; }
            else if (c.action == "drop")
            {
                if (!manager.Studio.ValidPosition(c.position)) { error = "Drop equipment near the studio."; return true; }
                item.position = c.position; item.rotation = c.rotation; item.holder = item.slot = 0;
                if (member.equipped == item.id) member.equipped = 0;
            }
            else if (c.action == "insertCard")
            {
                var loaded = s.objects.Find(o => o.kind == "sd" && o.loadedInto == item.id);
                if (item.kind == "camera" && loaded != null)
                {
                    loaded.loadedInto = 0; loaded.revision++;
                    s.message = "SD card removed. Select it in your hotbar."; return true;
                }
                var card = s.objects.Find(o => o.kind == "sd" && o.holder == sender && o.tape == 0 && o.loadedInto == 0);
                if (item.kind != "camera" || card == null || s.objects.Any(o => o.loadedInto == item.id))
                { error = "Hold the camera and collect an unused SD card first."; return true; }
                card.loadedInto = item.id; card.revision++;
            }
            else
            {
                var take = s.shots.Find(t => t.id == item.tape);
                if (item.kind != "sd" || take == null || !take.footageReady) { error = "Wait for the recording to finish saving."; return true; }
                take.uploaded = true; s.objects.Remove(item); member.equipped = 0;
                s.message = "SD card inserted. The Editor can open this take on the computer."; return true;
            }
            item.action = "idle"; item.revision++; s.message = "Equipment updated."; return true;
        }
        if (c.action == "setColor" || c.action == "clearStage")
        {
            if (!manager.HasRole(sender, CrewRole.Director) || s.recording) { error = "Only the Director can change the set between takes."; return true; }
            if (c.action == "setColor")
            {
                if (!MultiplayerContractManager.Finite(c.position)) return true;
                s.backdropColor = new Color(Mathf.Clamp01(c.position.x), Mathf.Clamp01(c.position.y), Mathf.Clamp01(c.position.z));
                foreach (var backdrop in s.objects.Where(o => o.kind == "backdrop")) { backdrop.color = s.backdropColor; backdrop.revision++; }
            }
            else
            {
                s.objects.RemoveAll(o => !IsEquipment(o.kind) && o.kind != "backdrop" && o.kind != "interior");
                foreach (var actor in s.objects.Where(o => o.kind == "actor")) { actor.target = actor.heldProduct = 0; actor.revision++; }
            }
            s.message = "Director updated the stage."; return true;
        }
        if (c.action == "grade")
        {
            if (!manager.HasRole(sender, CrewRole.Editor) || s.recording || !MultiplayerContractManager.Finite(c.position)) { error = "Only the Editor can change the commercial grade."; return true; }
            s.brightness = Mathf.Clamp(c.position.x, .75f, 1.25f); s.contrast = Mathf.Clamp(c.position.y, .75f, 1.5f); s.saturation = Mathf.Clamp(c.position.z, .65f, 1.4f);
            s.commercialTitle = (c.kind ?? "KAPE KULTURA").Substring(0, Mathf.Min(80, (c.kind ?? "KAPE KULTURA").Length));
            s.message = "Commercial branding and color updated."; return true;
        }
        if (c.action == "audio")
        {
            var item = s.objects.Find(o => o.id == c.id && o.kind == "audio");
            if (item == null || !manager.HasRole(sender, CrewRole.AVTechnician) || float.IsNaN(c.number) || float.IsInfinity(c.number)) { error = "AV Technician controls the audio monitor."; return true; }
            item.powered = c.value != 0; item.intensity = Mathf.Clamp(c.number,0,4); item.revision++; s.message = "Audio monitor updated."; return true;
        }
        return false;
    }
}
