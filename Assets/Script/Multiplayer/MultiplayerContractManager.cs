using System;
using System.Collections.Generic;
using UnityEngine;

// Session data belongs to the Photon room. It never writes career or PlayerPrefs data.
[Flags] public enum CrewRole { None = 0, Director = 1, Camera = 2, AVTechnician = 4, Editor = 8 }

[Serializable] public sealed class CrewMember
{
    public int id, roles;
    public int equipped;
    public bool ready, briefed, rolesLocked;
}

[Serializable] public sealed class CrewObject
{
    public int id, revision, tier, performance, target, targetIndex, heldProduct;
    public int holder, slot, tape, loadedInto;
    public Color color = Color.white;
    public string kind, action = "idle";
    public Vector3 position, rotation, start, end;
    public bool hasStart, hasEnd, powered = true;
    public float intensity = 3f, kelvin = 4500f, diffusion = 75f;
    public double walkTime;
}

[Serializable] public sealed class CrewShot
{
    public int id, owner;
    public bool footageReady, uploaded;
    public string size;
    public float duration, score;
    public int contractLevel = 1;
    public float cameraScore, lightScore, screenDirection;
    public string actorPose = "Neutral";
    public bool requiredSubjectsVisible, usedSoftLight, hasThreePointRoles;
    public List<int> cachedBy = new List<int>();
}

[Serializable] public sealed class CrewSession
{
    public int schema = 1, revision, nextId = 1, budget = ProductionEconomy.StartingBudget, recorder;
    public int contractLevel = 1;
    public bool contractPaid;
    public ProductionGrades result;
    public string productionId;
    public ProductionBudgetReview budgetReview;
    public CrewProductionResult resultLog;
    public int contractAttempt;
    public CrewCheckpoint checkpoint;
    public int briefingPage;
    public string phase = "lobby", message = "Choose your roles, lock them, then press READY.";
    public bool takeArmed, recording;
    public double recordStarted;
    public int samples, goodSamples;
    public int visibleSamples, softSamples, threePointSamples;
    public float directionTotal, lightTotal;
    public string recordedPose;
    public string shotSize;
    public List<CrewMember> members = new List<CrewMember>();
    public List<CrewObject> objects = new List<CrewObject>();
    public List<CrewShot> shots = new List<CrewShot>();
    public List<CrewCut> cuts = new List<CrewCut>();
    public List<string> equipment = new List<string>();
    public int nextShot = 1, activeShot;
    public Color backdropColor = Color.white;
    public float brightness = 1, contrast = 1, saturation = 1;
    public string commercialTitle = "KAPE KULTURA";
}

[Serializable] public sealed class CrewCheckpoint
{
    public int budget;
    public List<CrewObject> objects = new List<CrewObject>();
    public List<string> equipment = new List<string>();
}

[Serializable] public sealed class CrewCut
{
    public int shot;
    public float start, end;
}

[Serializable] public sealed class CrewCommand
{
    public string action, kind;
    public int id, target, index, value;
    public Vector3 position, rotation;
    public float number, number2;
    public string[] items;
    public ProductionGrades result;
    public List<CrewCut> cuts;
}

public static class MultiplayerContractManager
{
    // A room starting mid-campaign has no carried equipment. The starter allowance
    // replaces that missing carry-over without touching any player's career funds.
    public static int InitialBudget(int level) => level <= 1 ? ProductionEconomy.StartingBudget : ProductionEconomy.StartingBudget + ProductionEconomy.Advance(level);
    public static void SaveCheckpoint(CrewSession state)
    {
        state.checkpoint = JsonUtility.FromJson<CrewCheckpoint>(JsonUtility.ToJson(new CrewCheckpoint {
            budget = state.budget, objects = state.objects, equipment = state.equipment }));
        CrewProductionResults.Begin(state);
    }
    public static bool Retry(CrewSession state)
    {
        if (state.checkpoint == null || state.recording) return false;
        var restored = JsonUtility.FromJson<CrewCheckpoint>(JsonUtility.ToJson(state.checkpoint));
        state.budget = restored.budget; state.objects = restored.objects; state.equipment = restored.equipment;
        // Force replica state restoration even when the prior revision happens to match.
        foreach (var item in state.objects) item.revision += state.revision + 1;
        state.contractPaid = false; state.result = default; state.shots.Clear(); state.cuts.Clear();
        state.takeArmed = false; state.contractAttempt++;
        CrewProductionResults.Begin(state);
        foreach (var member in state.members) member.equipped = 0;
        state.phase = "briefing"; state.briefingPage = 0;
        state.message = "Contract restarted with its starting budget and equipment. Offline saves are unchanged.";
        return true;
    }
    public static string Title(int level) => level == 1 ? "FLORA & FORM HOME" : level == 2 ? "GOKE COLA" :
        level == 3 ? "TERRARI" : level == 4 ? "KAPE KULTURA" : "HARAYA";
    public static string Specifications(CrewSession state) => ContractUIManager.DepartmentBrief(state.contractLevel) +
        "\n\n<b>SHARED BUDGET</b>\nStarting advance: " + ProductionEconomy.Advance(state.contractLevel).ToString("N0") +
        " B-Coins. Current team balance: " + state.budget.ToString("N0") +
        " B-Coins. Fresh rooms starting after Contract 1 also receive a 9,000 B-Coin starter allowance to replace missing carried equipment. Continuing to the next contract pays only its advance. All purchases use the shared balance. Reuse equipment; deleting items does not refund money. Completion bonus up to " +
        ProductionEconomy.CompletionBonus(state.contractLevel).ToString("N0") + " B-Coins. Offline career money and progress are not used.";
    public static bool IsPoweredLight(CrewSession state, CrewObject item) =>
        (item.kind == "light" || item.kind == "softlight") && item.powered && item.intensity > 0 &&
        (item.holder == 0 || state.members.Exists(m => m.id == item.holder && m.equipped == item.id));
    public static float PreProduction(CrewSession state, out string feedback)
        => PreProduction(state, out feedback, out _);
    public static float PreProduction(CrewSession state, out string feedback, out bool required)
    {
        var messages = new List<string>();
        float score = 0; bool complete = true;
        int Count(string kind) => state.objects.FindAll(o => o.kind == kind).Count;
        void Award(bool valid, float points, string instruction, bool mandatory = true)
        {
            if (valid) score += points;
            else { messages.Add(instruction); if (mandatory) complete = false; }
        }
        var color = state.backdropColor;
        bool light = state.objects.Exists(o => IsPoweredLight(state, o));
        switch (state.contractLevel)
        {
            case 1:
                Award(Count("backdrop") > 0, 25, "Add the backdrop.");
                Award(Count("product") > 0, 25, "Place the flower vase.");
                Award(Count("backdrop") > 0 && Mathf.Abs(color.r*255-255)<=10 &&
                    Mathf.Abs(color.g*255-TutorialManager.TutorialGreenTarget)<=10 &&
                    Mathf.Abs(color.b*255-TutorialManager.TutorialBlueTarget)<=10, 50, "Match the pink backdrop in the brief.");
                break;
            case 2:
                Award(Count("backdrop") > 0, 15, "Add the backdrop.");
                Award(Count("product") > 0, 20, "Place Goke.");
                Award(color.r > .5f && color.g < .3f && color.b < .3f, 35, "Set the backdrop to red.");
                Award(Count("backdrop") > 0 && Count("product") > 0, 30, "Prepare the product and backdrop.");
                Award(light, 0, "Power at least one light.");
                break;
            case 3:
                Award(Count("product") == 1, 35, "Place exactly one Terrari.");
                var product = state.objects.Find(o => o.kind == "product");
                var soft = state.objects.Find(o => o.kind == "softlight" && IsPoweredLight(state, o) && product != null && Vector3.Distance(o.position, product.position) <= 12);
                Award(soft != null, 35, "Bring a powered Better Light close to the car.");
                Award(soft != null && soft.intensity >= 1.8f, 15, "Raise Better Light output to at least 30%.", false);
                Award(soft != null && soft.diffusion >= 50 && Vector3.Dot(Quaternion.Euler(soft.rotation)*Vector3.forward,
                    (product.position + Vector3.up - soft.position).normalized) >= .6f, 15, "Aim the Better Light at the car with at least 50% diffusion.", false);
                break;
            case 4:
                Award(Count("interior") > 0, 30, "Use the coffee interior, not a plain wall.");
                Award(Count("actor") > 0, 35, "Hire at least one actor.");
                Award(Count("cup") > 0 && Count("product") > 0, 35, "Place the coffee cup and Kape packaging.");
                break;
            default:
                Award(Count("backdrop") > 0, 10, "Add the campaign backdrop.");
                Award(Count("backdrop") > 0 && color.r <= .35f && color.g >= .35f && color.b >= .35f && color.g > color.r && color.b > color.r,
                    15, "Set the backdrop to teal.");
                Award(Count("product") == 1, 15, "Place exactly one Haraya product.");
                Award(Count("vehicle") == 1, 15, "Place exactly one vehicle.");
                Award(Count("actor") == 1, 10, "Hire exactly one actor.");
                Award(state.objects.Exists(o => o.kind == "actor" && (o.performance != 0 || o.action != "idle")), 10, "Prepare a deliberate actor performance.");
                Award(state.objects.FindAll(o => IsPoweredLight(state, o)).Count >= 3, 25, "Power three campaign lights.");
                break;
        }
        feedback = "--- PRE-PRODUCTION ---\n" + (messages.Count == 0 ? "<color=green>+ Required room setup complete.</color>\n" :
            "<color=red>- " + string.Join("\n- ", messages) + "</color>\n");
        required = complete; return score;
    }
    public static readonly string[] Briefing = {
        "Welcome, crew! Make the selected singleplayer contract together. Your team shares its production budget. Each role has its own station and equipment. Tab opens the full contract specifications.",
        "DIRECTOR: use the tablet to build the selected contract's set and add its products. R rotates a placement. For actor contracts, buy the megaphone and cue movement and performance inside the stage.",
        "CAMERA: buy a camera and SD cards at the shop. Pick them up, hold the camera and press C to insert a card. R records. Contracts 4 and 5 also need the Director to call ACTION first.",
        "AV TECHNICIAN: buy and position lights. Z/X changes Kelvin; C/V changes intensity. Aim at the required subjects. Better Lights and three-point lighting are used when the selected brief requires them.",
        "EDITOR: receive recorded SD cards at the computer. Open the recordings, trim and arrange your shots, add branding and adjust the color.",
        "Follow your selected contract's shot types, duration, branding and lighting rules—not a separate multiplayer checklist. P opens the Almanac; Tab shows the full contract."
    };
    public static CrewRole RoleFor(string kind)
    {
        if (kind == "sd") return CrewRole.Camera | CrewRole.Editor;
        if (kind == "camera") return CrewRole.Camera;
        if (kind == "editing") return CrewRole.Editor;
        if (kind == "light" || kind == "softlight" || kind == "audio") return CrewRole.AVTechnician;
        return CrewRole.Director;
    }

    public static string RoleName(CrewRole roles)
    {
        if (roles == CrewRole.None) return "Unassigned";
        var names = new List<string>();
        if ((roles & CrewRole.Director) != 0) names.Add("Director");
        if ((roles & CrewRole.Camera) != 0) names.Add("Camera");
        if ((roles & CrewRole.AVTechnician) != 0) names.Add("AV Technician");
        if ((roles & CrewRole.Editor) != 0) names.Add("Editor");
        return string.Join(", ", names);
    }

    public static int Price(string kind)
    {
        switch (kind)
        {
            case "camera": return ProductionEconomy.Camera;
            case "softlight": return ProductionEconomy.SoftLight;
            case "megaphone": return 900;
            case "sd": return 150;
            case "audio": return 750;
            case "editing": return 500;
            case "actor": return ProductionEconomy.ActorBase;
            case "chair": case "product": case "cup": return 250;
            case "table": return 400;
            case "light": return ProductionEconomy.PanelLight;
            case "vehicle": return ProductionEconomy.Vehicle;
            case "backdrop": return 500;
            case "interior": return 2750;
            default: return -1;
        }
    }

    public static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
        float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

    public static bool CanStart(CrewSession state)
    {
        int roles = 0, count = 0;
        foreach (var member in state.members)
        {
            if (member.roles == 0 || !member.rolesLocked || !member.ready) return false;
            roles |= member.roles;
            count++;
        }
        return count >= 2 && count <= 4 && roles == 15;
    }

    public static string Tasks(CrewSession state, CrewRole role)
    {
        switch (role)
        {
            case CrewRole.Director: return state.contractLevel < 4 ? "Build the set and place the product specified in " + Title(state.contractLevel) + ". R rotates a tablet placement." : "Build the required set, place actors and products, cue the performance or interaction, then call ACTION before each take.";
            case CrewRole.Camera: return "Record the shots specified in " + Title(state.contractLevel) + ". Use a different blank SD card for each take; keep the required subjects readable.";
            case CrewRole.AVTechnician: return "Buy and position lights/audio equipment. Z/X Kelvin; C/V intensity; F power. Aim lights at the actor and product.";
            case CrewRole.Editor: return "Use the same singleplayer editor: drag, trim, split, arrange clips, add branding/music and color grade. Follow the selected contract's delivery rules and export for review.";
            default: return "Choose a crew role in the crew menu. With 2–3 players, claim more than one job.";
        }
    }
}
