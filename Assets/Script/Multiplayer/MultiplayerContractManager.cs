using System;
using System.Collections.Generic;
using UnityEngine;

// Session data belongs to the Photon room. It never writes career or PlayerPrefs data.
[Flags] public enum CrewRole { None = 0, Director = 1, Camera = 2, Lighting = 4, SetProps = 8 }

[Serializable] public sealed class CrewMember
{
    public int id, roles;
    public bool ready;
}

[Serializable] public sealed class CrewObject
{
    public int id, revision, tier, performance, target, targetIndex, heldProduct;
    public string kind, action = "idle";
    public Vector3 position, rotation, start, end;
    public bool hasStart, hasEnd, powered = true;
    public float intensity = 3f, kelvin = 4500f, diffusion = 75f;
    public double walkTime;
}

[Serializable] public sealed class CrewShot
{
    public string size;
    public float duration, score;
}

[Serializable] public sealed class CrewSession
{
    public int schema = 1, revision, nextId = 1, budget = 20000, recorder;
    public string phase = "lobby", message = "Choose your crew roles, then ready up.";
    public bool takeArmed, recording;
    public double recordStarted;
    public int samples, goodSamples;
    public string shotSize;
    public List<CrewMember> members = new List<CrewMember>();
    public List<CrewObject> objects = new List<CrewObject>();
    public List<CrewShot> shots = new List<CrewShot>();
}

[Serializable] public sealed class CrewCommand
{
    public string action, kind;
    public int id, target, index, value;
    public Vector3 position, rotation;
    public float number, number2;
}

public static class MultiplayerContractManager
{
    public static CrewRole RoleFor(string kind)
    {
        if (kind == "actor") return CrewRole.Director;
        if (kind == "light") return CrewRole.Lighting;
        return CrewRole.SetProps;
    }

    public static int Price(string kind)
    {
        switch (kind)
        {
            case "actor": return 900;
            case "chair": case "product": case "cup": return 250;
            case "table": return 400;
            case "light": return 750;
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
            if (member.roles == 0 || !member.ready) return false;
            roles |= member.roles;
            count++;
        }
        return count >= 2 && roles == 15;
    }

    public static string Tasks(CrewSession state, CrewRole role)
    {
        switch (role)
        {
            case CrewRole.Director: return "Place an actor. Cue a performance or seat/product interaction. Call ACTION before each take.";
            case CrewRole.Camera: return "Film WIDE, MEDIUM and CLOSE shots, at least 5 seconds each. Keep actor and coffee readable.";
            case CrewRole.Lighting: return "Place a light and aim it toward the subjects. Z/X Kelvin; C/V intensity; F power. Aim and diffusion are in the crew menu.";
            case CrewRole.SetProps: return "Choose a backdrop or coffee interior. Add the coffee product and furniture. Keep a clear acting area.";
            default: return "Choose a crew role in the crew menu. With 2–3 players, claim more than one job.";
        }
    }
}
