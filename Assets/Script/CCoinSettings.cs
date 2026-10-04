using System;
using UnityEngine;

// Public function name only. The existing PlayFab SDK owns the authenticated
// destination/title; no website URL receives the game's session ticket.
[CreateAssetMenu(menuName = "Crew-On-Set/C-Coins Connection", fileName = "CCoinSettings")]
public sealed class CCoinSettings : ScriptableObject
{
    public string functionName = "CrewCCoins";
    public string websiteUrl = "https://crewon-set-web.vercel.app/";
    [Tooltip("Enable only after the website's PayMongo checkout/webhook is deployed and tested.")]
    public bool websitePurchasesEnabled;
    public static bool WebsitePurchasesEnabled => Resources.Load<CCoinSettings>("CCoinSettings")?.websitePurchasesEnabled ?? false;
    public static string WebsiteUrl
    {
        get
        {
            string value = Environment.GetEnvironmentVariable("CREW_CCOINS_WEBSITE_URL");
            if (string.IsNullOrWhiteSpace(value)) value = Resources.Load<CCoinSettings>("CCoinSettings")?.websiteUrl;
            return IsApprovedWebsite(value) ? value : "";
        }
    }
    public static bool IsApprovedWebsite(string value) => Uri.TryCreate(value,UriKind.Absolute,out var uri)
        && uri.Scheme == "https" && uri.Host == "crewon-set-web.vercel.app" && uri.Port == 443
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    public static string FunctionName
    {
        get
        {
            string value = Environment.GetEnvironmentVariable("CREW_CCOINS_FUNCTION");
            if (string.IsNullOrWhiteSpace(value)) value = Resources.Load<CCoinSettings>("CCoinSettings")?.functionName ?? "CrewCCoins";
            return CCoinRules.ValidId(value) ? value : "";
        }
    }
}
