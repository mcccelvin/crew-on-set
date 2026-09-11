using UnityEditor;
using UnityEngine;
using TMPro;

// One-time read-only verification after these scripts load in Play Mode.
[InitializeOnLoad]
public static class BalanceRuntimeVerification
{
    static BalanceRuntimeVerification() { EditorApplication.update += Verify; }
    private static void Verify()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (SessionState.GetBool("BalanceRuntimeVerification_20260910_v1", false))
        { EditorApplication.update -= Verify; return; }
        int labels = 0;
        foreach (TMP_Text text in Object.FindObjectsOfType<TMP_Text>(true))
            if (text.name == "Quantity" && text.transform.parent != null && text.transform.parent.name == "BCoins") labels++;
        if (labels == 0) return;
        GameFeedback.RefreshBalance();
        string expected = Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)).ToString("N0");
        bool matches = true;
        foreach (TMP_Text text in Object.FindObjectsOfType<TMP_Text>(true))
            if (text.name == "Quantity" && text.transform.parent != null && text.transform.parent.name == "BCoins")
                matches &= text.text == expected;
        System.IO.File.WriteAllText("C:/Users/mckel/OneDrive/Documents/Kelvin/balance-fix/runtime-check.txt",
            "HUD labels: " + labels + "\nSaved balance: " + expected + "\nHUD matches save: " + matches);
        SessionState.SetBool("BalanceRuntimeVerification_20260910_v1", true);
        EditorApplication.update -= Verify;
    }
}
