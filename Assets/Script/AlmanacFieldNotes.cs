using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlmanacManager
{
    private RectTransform fieldNotesRoot, fieldKeysRoot;
    private Button fieldNotesToggle;
    private bool detailedFieldNotes, restoreFieldNotes, restoreFieldKeys, restoreFieldToggle;
    private string fieldNotesAnchorId;
    private bool navigationAfterCover, navigationPresentationPending;

    private bool AlmanacTransitionBusy() => bookSelectionAnimating || AlmanacCoverOpening();

    private void UpdateAlmanacPresentation()
    {
        if (!isAlmanacOpen) { navigationAfterCover = navigationPresentationPending = false; return; }
        if (AlmanacTransitionBusy()) return;
        if (navigationAfterCover) { navigationAfterCover = false; BeginNavigationLesson(); }
        if (navigationPresentationPending) { navigationPresentationPending = false; ShowNavigationLesson(); }
    }

    private bool AlmanacCoverOpening()
    {
        if (knowledgePanel == null || knowledgePanel.transform.parent == null) return false;
        var motion = knowledgePanel.transform.parent.GetComponent<ContractFolderMotion>();
        return motion != null && motion.IsPlaying;
    }

    private ContractFolderMotion GetAlmanacCoverMotion()
    {
        if (knowledgePanel == null || knowledgePanel.transform.parent == null) return null;
        var book = knowledgePanel.transform.parent as RectTransform;
        if (book == null || book.GetComponent<Image>() == null) return null;
        var motion = book.GetComponent<ContractFolderMotion>();
        if (motion == null) motion = book.gameObject.AddComponent<ContractFolderMotion>();
        motion.spine = .50f; motion.approachDuration = .42f; motion.duration = .75f;
        return motion;
    }

    private RectTransform AlmanacHudOrigin(Transform parent, out GameObject fallback)
    {
        fallback = null;
        foreach (var image in FindObjectsOfType<Image>())
        {
            if (image.name != "Almanac HUD" || image.transform.IsChildOf(almanacCanvas.transform)) continue;
            return image.rectTransform;
        }
        fallback = new GameObject("Almanac corner motion anchor", typeof(RectTransform));
        var origin = fallback.GetComponent<RectTransform>(); origin.SetParent(parent, false);
        SetRect(origin, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-790, 400), new Vector2(130, 172));
        return origin;
    }

    private void PlayAlmanacCoverOpening(bool fromHud = true)
    {
        var motion = GetAlmanacCoverMotion();
        if (motion == null) return;
        GameObject fallback = null;
        var origin = fromHud ? AlmanacHudOrigin(motion.transform.parent, out fallback) : null;
        motion.Play(ExportUIArt.Get("almanacHud"), origin);
        if (fallback != null) Destroy(fallback);
    }

    private ContractFolderMotion PlayAlmanacCoverClosing(bool returnToHud)
    {
        var motion = GetAlmanacCoverMotion();
        if (motion == null) return null;
        GameObject fallback = null;
        var destination = returnToHud ? AlmanacHudOrigin(motion.transform.parent, out fallback) : null;
        motion.Close(ExportUIArt.Get("almanacHud"), destination);
        if (fallback != null) Destroy(fallback);
        return motion;
    }

    private List<string> BuildFieldNoteBlocks(KnowledgeEntry entry)
    {
        if (!detailedFieldNotes)
        {
            string[] quick = QuickAlmanacNotes(entry.id);
            if (quick != null) return new List<string>(quick);
        }
        var notes = new List<string>();
        string heading = "START HERE";
        foreach (string raw in (entry.description ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Length < 55 && Regex.IsMatch(line, @"^[A-Z][A-Z /&-]{2,}$")) { heading = line; continue; }
            bool bullet = Regex.IsMatch(line, @"^(?:[-•]|\d+\.)\s+");
            line = Regex.Replace(line, @"^(?:[-•]|\d+\.)\s+", "");
            if (bullet && heading == "START HERE") heading = "ON SET";
            foreach (string sentence in Regex.Split(line, @"(?<=[.!?])\s+(?=[A-Z])"))
            {
                string remaining = sentence.Trim();
                while (remaining.Length > 0)
                {
                    int length = Mathf.Min(165, remaining.Length);
                    if (length < remaining.Length)
                    {
                        int split = remaining.LastIndexOf(' ', length - 1, length);
                        if (split > 0) length = split;
                    }
                    notes.Add(heading + "\n" + remaining.Substring(0, length));
                    remaining = remaining.Substring(length).TrimStart();
                }
            }
        }
        if (notes.Count == 0) notes.Add("ON SET\nUse this guide during your next production lesson.");
        if (!detailedFieldNotes && notes.Count > 3)
        {
            // Lead with the idea, a real control/action and one supporting note.
            var quick = new List<string> { notes[0] };
            int control = notes.FindIndex(1, note => note.Contains("["));
            if (control > 0) quick.Add(notes[control]);
            for (int i = 1; i < notes.Count && quick.Count < 3; i++) if (i != control) quick.Add(notes[i]);
            return quick;
        }
        return notes;
    }

    private static string[] QuickAlmanacNotes(string id)
    {
        switch (id)
        {
            case "hiring_and_posing_actors":
            case "actor_megaphone":
                return new[] {
                    "HIRE & PLACE\nChoose an Actor card in the tablet, move them over the stage and click to place. Rookie saves money; higher tiers add acting polish. Every tier can pass.",
                    "CUE THE PERFORMANCE\nEquip the megaphone. [LMB] selects the actor; [Z] cycles Neutral, Wave and Action. Aim at a stool or machine and click to cue it; [O] stops the action.",
                    "BLOCK A CLEAN SHOT\n[T] moves the selected actor; [R] turns them in 15-degree steps. Keep the product clear. Match pose and screen side when cutting between takes."
                };
            case "nony_fx_camera":
                return new[] {
                    "READY THE CAMERA\nSelect your camera in the hotbar. [C] inserts an SD card. [LMB] opens the viewfinder so you can frame the subject.",
                    "GET THE TAKE\n[R] starts or stops recording. [SCROLL] changes zoom; [Q/E] changes height. Keep the whole required subject in the frame.",
                    "MOVE WITH INTENT\nHold [CTRL + WASD] for smooth movement and [CTRL + MOUSE] for fine aim. [F2] opens settings once unlocked. Record the shot your contract needs."
                };
            case "sd_card":
                return new[] {
                    "ONE CARD, MANY TAKES\nEach reusable SD card holds 60 seconds total. Record several clips while space remains; a full card cannot start another take.",
                    "KEEP YOUR SOURCES CLEAR\nCards are numbered in purchase order. The computer labels each clip with its source SD card so you can tell recordings apart.",
                    "MAKE ROOM\nRename or delete clips from the computer. Deleting a take frees its card space. EJECT SD lets you choose which inserted card to return."
                };
            case "led_panel":
                return new[] {
                    "POWER & AIM\nEquip the panel and use [LMB] to switch it on. Aim the light at the subject so the important shape stays readable.",
                    "SHAPE THE LIGHT\n[SCROLL] adjusts intensity. [UP / DOWN] changes tilt; [Q/E] adjusts height. Change one setting at a time and look at the result.",
                    "PLACE WITH PURPOSE\n[G] drops the light in position. Follow the current brief: not every contract needs Key, Fill and Back Lights."
                };
            case "camera_white_balance":
                return new[] {
                    "START CLEAN\n[F2] opens settings. Try WB 5600K and Tint 0 as the game's no-correction baseline; warm scene lights may still look warm.",
                    "COMPARE ON GREY\nTry 3200K for a cooler image and 6500K for a warmer image. Judge the lit grey reference, not the car's paint. Neither is automatically best.",
                    "CHOOSE & KEEP IT\nAdjust WB/Tint for near-neutral whites, then intentional warmth. Keep the same balance across takes. The lamp's Kelvin is a separate control."
                };
            case "rule_of_thirds":
                return new[] {
                    "FIND A CROSSING\nThe thirds grid divides the view into nine boxes. Place the main subject near a crossing rather than centring every shot.",
                    "LEAVE MESSAGE SPACE\nKeep the full product visible. Use the open side for readable branding instead of covering the subject with graphics.",
                    "TRY IT IN CAMERA\nOpen [F2], select Grid and use [RIGHT] for ON. Try opposite crossings. Turn it off with [LEFT] when you want to practise without the guide."
                };
            case "editing_computer":
                return new[] {
                    "IMPORT & REVIEW\nInsert your recorded SD card at the computer. Open Recordings to review takes and check their source cards before editing.",
                    "BUILD THE CUT\nOpen the Editor. Trim and arrange footage in the order required by the brief; add only the graphics that contract requests.",
                    "WATCH BEFORE SUBMITTING\nPreview the complete export. Check timing, subjects and branding. Reopen the contract's Full Guide for exact delivery and grade targets."
                };
            default: return null;
        }
    }

    private void HideFieldNotes()
    {
        // A refreshed/empty category owns its new visibility; do not revive a
        // previous article's cards when an interrupted leaf is restored.
        restoreFieldNotes = restoreFieldKeys = restoreFieldToggle = false;
        if (fieldNotesRoot != null) fieldNotesRoot.gameObject.SetActive(false);
        if (fieldKeysRoot != null) fieldKeysRoot.gameObject.SetActive(false);
        if (fieldNotesToggle != null) fieldNotesToggle.gameObject.SetActive(false);
        if (bookRightText != null) bookRightText.gameObject.SetActive(true);
    }

    private RectTransform NoteBox(Transform parent, string name, Vector2 position, Vector2 size, Color colour, bool paper = false)
    {
        var box = CreatePanel(name, parent, colour);
        var rect = box.GetComponent<RectTransform>();
        SetRect(rect, Vector2.one * .5f, Vector2.one * .5f, position, size);
        if (paper) { CrewPaperStyle.Card(box.GetComponent<Image>()); box.GetComponent<Image>().color = colour; }
        box.GetComponent<Image>().raycastTarget = false;
        return rect;
    }

    private TextMeshProUGUI NoteText(Transform parent, string name, string text, Vector2 position, Vector2 size, float font)
    {
        var label = CreateText(name, parent, text, font, TextAlignmentOptions.TopLeft);
        SetRect(label.rectTransform, Vector2.one * .5f, Vector2.one * .5f, position, size);
        label.font = TMP_Settings.defaultFontAsset; label.color = CrewPaperStyle.Ink;
        label.fontSize = label.fontSizeMax = font; label.fontSizeMin = font - 2;
        label.enableAutoSizing = true; label.enableWordWrapping = true; label.richText = true;
        label.lineSpacing = 4; label.margin = Vector4.zero; label.raycastTarget = false;
        return label;
    }

    private void ShowFieldNotes(KnowledgeEntry entry, string body)
    {
        if (fieldNotesRoot == null || fieldNotesRoot.parent != knowledgePanel.transform)
            fieldNotesRoot = NoteBox(knowledgePanel.transform, "On-set field notes", new Vector2(365, 22), new Vector2(565, 658), Color.clear);
        foreach (Transform child in fieldNotesRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        fieldNotesRoot.gameObject.SetActive(true); bookRightText.gameObject.SetActive(false);
        var heading = NoteText(fieldNotesRoot, "Playbook heading", detailedFieldNotes ? "FULL FIELD NOTES" : "YOUR ON-SET PLAYBOOK", new Vector2(0, 301), new Vector2(540, 32), 21);
        heading.fontStyle = FontStyles.Bold; heading.characterSpacing = 1.3f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        Color accent = new Color32(155, 88, 43, 255);
        string[] notes = body.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Mathf.Min(3, notes.Length); i++)
        {
            var card = NoteBox(fieldNotesRoot, "Field note " + i, new Vector2(0, 193 - i * 180), new Vector2(548, 168), CrewPaperStyle.Paper, true);
            NoteBox(card, "Note accent", new Vector2(-267, 0), new Vector2(4, 130), accent);
            int split = notes[i].IndexOf('\n');
            string title = split >= 0 ? notes[i].Substring(0, split) : "ON SET";
            string instruction = split >= 0 ? notes[i].Substring(split + 1) : notes[i];
            var number = NoteText(card, "Note number", (i + 1).ToString("00"), new Vector2(-238, 55), new Vector2(40, 28), 18);
            number.color = accent; number.fontStyle = FontStyles.Bold;
            var titleText = NoteText(card, "Note title", title, new Vector2(18, 55), new Vector2(445, 28), 19);
            titleText.color = accent; titleText.fontStyle = FontStyles.Bold;
            var text = NoteText(card, "Note instruction", FormatBookArticle(instruction), new Vector2(0, -18), new Vector2(500, 110), 24);
            text.fontSizeMin = 20;
            text.fontStyle = FontStyles.Normal;
        }
        var hint = NoteText(fieldNotesRoot, "Try on set", "ONE SMALL CHANGE. WATCH THE RESULT.", new Vector2(0, -290), new Vector2(540, 30), 17);
        hint.fontStyle = FontStyles.Bold; hint.color = accent; hint.alignment = TextAlignmentOptions.Center;
        ShowFieldKeys(entry, body);
        if (fieldNotesToggle == null)
        {
            var button = NoteBox(knowledgePanel.transform, "Full Almanac notes", new Vector2(-365, -422), new Vector2(248, 42), CrewPaperStyle.Paper, true);
            fieldNotesToggle = button.gameObject.AddComponent<Button>();
            var text = NoteText(button, "Notes mode label", "FULL NOTES", Vector2.zero, new Vector2(236, 36), 20);
            text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
            CrewPaperStyle.ActionButton(fieldNotesToggle);
            fieldNotesToggle.onClick.AddListener(() => {
                if (bookSelectionAnimating || AlmanacCoverOpening() || bookEntries.Count == 0) return;
                fieldNotesAnchorId = bookEntries[bookPage].id; detailedFieldNotes = !detailedFieldNotes; RefreshBookPage();
            });
        }
        fieldNotesToggle.gameObject.SetActive(true);
        fieldNotesToggle.GetComponentInChildren<TextMeshProUGUI>().text = detailedFieldNotes ? "QUICK NOTES" : "FULL NOTES";
    }

    private void ShowFieldKeys(KnowledgeEntry entry, string body)
    {
        if (fieldKeysRoot == null || fieldKeysRoot.parent != knowledgePanel.transform)
            fieldKeysRoot = NoteBox(knowledgePanel.transform, "Quick control badges", new Vector2(-365, -351), new Vector2(558, 76), CrewPaperStyle.Paper, true);
        foreach (Transform child in fieldKeysRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        fieldKeysRoot.gameObject.SetActive(true);
        var title = NoteText(fieldKeysRoot, "Quick keys heading", "QUICK KEYS  /  LESSON " + entry.level, new Vector2(0, 23), new Vector2(526, 20), 13);
        title.fontStyle = FontStyles.Bold; title.color = CrewPaperStyle.MutedInk;
        var keys = new List<string>();
        foreach (Match match in Regex.Matches(body + "\n" + entry.description, @"\[([^\]\r\n]+)\]"))
        {
            string key = match.Groups[1].Value.Trim().ToUpperInvariant()
                .Replace("LEFT MOUSE BUTTON", "LMB").Replace("SCROLL WHEEL", "SCROLL");
            if (!keys.Contains(key) && keys.Count < 5) keys.Add(key);
        }
        float x = -258;
        if (keys.Count == 0)
        {
            var tip = NoteText(fieldKeysRoot, "Practice cue", "Try this in your next take.", new Vector2(0, -10), new Vector2(526, 31), 21);
            tip.fontStyle = FontStyles.Bold;
        }
        var widths = new List<float>();
        float totalWidth = 0;
        foreach (string key in keys)
        {
            float measured = title.GetPreferredValues(key).x * (18f / 13f);
            float width = Mathf.Clamp(measured + 24, 58, 190);
            widths.Add(width); totalWidth += width;
        }
        float fit = totalWidth > 0 ? Mathf.Min(1, (516f - Mathf.Max(0, keys.Count - 1) * 8f) / totalWidth) : 1;
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            float width = widths[i] * fit;
            var badge = NoteBox(fieldKeysRoot, "Key " + key, new Vector2(x + width * .5f, -10), new Vector2(width, 30), CrewPaperStyle.Gold, true);
            var label = NoteText(badge, "Key label", key, Vector2.zero, new Vector2(width - 8, 28), 18);
            label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false; label.fontSizeMin = 12;
            x += width + 8;
        }
    }

    private void RestoreFieldNotesAfterTurn()
    {
        if (restoreFieldNotes && fieldNotesRoot != null) fieldNotesRoot.gameObject.SetActive(true);
        if (restoreFieldKeys && fieldKeysRoot != null) fieldKeysRoot.gameObject.SetActive(true);
        if (restoreFieldToggle && fieldNotesToggle != null) fieldNotesToggle.gameObject.SetActive(true);
        restoreFieldNotes = restoreFieldKeys = restoreFieldToggle = false;
    }

    private void CopyFieldNotesForTurn(Transform paper, bool leftPage)
    {
        if (!leftPage && fieldNotesRoot != null && fieldNotesRoot.gameObject.activeSelf)
        { var copy = Instantiate(fieldNotesRoot, fieldNotesRoot.parent); copy.SetParent(paper, true); fieldNotesRoot.gameObject.SetActive(false); restoreFieldNotes = true; }
        if (leftPage && fieldKeysRoot != null && fieldKeysRoot.gameObject.activeSelf)
        { var copy = Instantiate(fieldKeysRoot, fieldKeysRoot.parent); copy.SetParent(paper, true); fieldKeysRoot.gameObject.SetActive(false); restoreFieldKeys = true; }
        if (leftPage && fieldNotesToggle != null && fieldNotesToggle.gameObject.activeSelf)
        { var copy = Instantiate(fieldNotesToggle.transform, fieldNotesToggle.transform.parent); copy.SetParent(paper, true); fieldNotesToggle.gameObject.SetActive(false); restoreFieldToggle = true; }
    }
}
