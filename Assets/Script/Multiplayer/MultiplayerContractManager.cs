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
}

[Serializable] public sealed class CrewSession
{
    public int schema = 1, revision, nextId = 1, budget = 20000, recorder;
    public int briefingPage;
    public string phase = "lobby", message = "Choose your roles, lock them, then press READY.";
    public bool takeArmed, recording;
    public double recordStarted;
    public int samples, goodSamples;
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
}

public static class MultiplayerContractManager
{
    public static readonly string[] Briefing = {
        "Welcome, crew! Make a coffee commercial together. Your team shares B20,000. Each role has its own station and equipment.",
        "DIRECTOR: use the tablet to choose the set, place actors and add products. Buy the megaphone, then cue the actor's movement and performance.",
        "CAMERA: buy a camera and SD cards at the shop. Pick them up, hold the camera and press C to insert a card. Director calls ACTION; R records.",
        "AV TECHNICIAN: buy and position lights. Z/X changes Kelvin; C/V changes intensity. Aim your lights so the coffee and actor read clearly.",
        "EDITOR: receive recorded SD cards at the computer. Open the recordings, trim and arrange your shots, add branding and adjust the color.",
        "Film Wide, Medium and Close coverage, five seconds each. P opens the Almanac; Tab shows the contract. Visit the shop and prepare your station."
    };
    public static CrewRole RoleFor(string kind)
    {
        if (kind == "sd") return CrewRole.Camera | CrewRole.Editor;
        if (kind == "camera") return CrewRole.Camera;
        if (kind == "editing") return CrewRole.Editor;
        if (kind == "light" || kind == "audio") return CrewRole.AVTechnician;
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
            case "camera": return 4000;
            case "megaphone": return 900;
            case "sd": return 150;
            case "audio": return 750;
            case "editing": return 500;
            case "actor": return 900;
            case "chair": case "product": case "cup": return 250;
            case "table": return 400;
            case "light": return 1200;
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
            case CrewRole.Director: return "Place an actor. Cue a performance or seat/product interaction. Call ACTION before each take.";
            case CrewRole.Camera: return "Film WIDE, MEDIUM and CLOSE shots, at least 5 seconds each. Keep actor and coffee readable.";
            case CrewRole.AVTechnician: return "Buy and position lights/audio equipment. Z/X Kelvin; C/V intensity; F power. Aim lights at the actor and product.";
            case CrewRole.Editor: return "Use the computer to receive footage, trim and arrange takes, add the brand title and grade the commercial. Submit five seconds each of Wide, Medium and Close.";
            default: return "Choose a crew role in the crew menu. With 2–3 players, claim more than one job.";
        }
    }
}
