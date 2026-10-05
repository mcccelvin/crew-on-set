using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// Learning support, not a second grader. Only saved warning/correction notes
// trigger targeted advice; creative questions never claim to be measured results.
public static class FeedbackLearningGuide
{
    private static readonly Regex Tags = new Regex("<[^>]+>");
    private sealed class Revision
    {
        public string key, title, why, action, check, evidence;
    }

    public static string Department(int page, int level, string evidence)
    {
        var text = new StringBuilder();
        var revisions = new List<Revision>();
        foreach (string line in (evidence ?? "").Split('\n'))
        {
            string plain = Tags.Replace(line, "").Trim();
            if (!IsCorrection(line, plain)) continue;
            var revision = Match(page, level, plain);
            if (revision == null || revisions.Exists(r => r.key == revision.key)) continue;
            revision.evidence = line.Trim();
            revisions.Add(revision);
            if (revisions.Count == 2) break;
        }
        if (revisions.Count > 0)
        {
            Heading(text, "REVISION PRIORITIES");
            for (int i = 0; i < revisions.Count; i++)
            {
                var r = revisions[i];
                if (i > 0) text.AppendLine();
                text.AppendLine("<b>" + (i + 1) + ". " + r.title + "</b>");
                text.AppendLine("Recorded note: " + r.evidence);
                text.AppendLine("<b>Why it matters:</b> " + r.why);
                text.AppendLine("<b>Try this:</b> " + r.action);
                text.AppendLine("<b>Check your revision:</b> " + r.check);
            }
        }
        else
        {
            Heading(text, "DEVELOP YOUR PRACTICE");
            text.AppendLine("Use the saved checks below as evidence, not as a complete artistic critique. " + Practice(page));
        }
        Heading(text, "LEARNING FOCUS");
        text.AppendLine(Focus(page));
        Heading(text, "SAVED ASSESSMENT NOTES");
        text.AppendLine(evidence);
        Heading(text, "CREATIVE SELF-REVIEW / NOT AN EXTRA GRADE");
        text.AppendLine(CreativeQuestion(page, level));
        return text.ToString().TrimEnd();
    }

    public static string Budget(string evidence)
    {
        var text = new StringBuilder();
        Heading(text, "RECORDED BUDGET");
        text.AppendLine(evidence);
        Heading(text, "PRODUCER'S REVIEW / COACHING ONLY");
        text.AppendLine("Separate reusable investments from consumables. An owned light may serve several productions; buying another one is not automatically a solution to a weak shot.");
        text.AppendLine("Before spending again, check whether repositioning, reframing or a new edit addresses the saved correction. Reserve cash for required products, actors and recording media.");
        text.AppendLine("For your production journal, compare your original purchase plan with these recorded costs and explain one trade-off between creative impact and cost. A planned budget or profit margin is not supplied here when the game did not save it.");
        text.AppendLine("Incomplete tracking cannot prove cost efficiency. Low spending alone is not a technical or artistic grade.");
        return text.ToString().TrimEnd();
    }

    public static string Decision(string evidence, bool passed, int level)
    {
        var text = new StringBuilder(evidence);
        Heading(text, passed ? "NEXT PORTFOLIO STEP" : "REVISION PLAN BEFORE RESUBMISSION");
        text.AppendLine(passed
            ? "Keep the approved cut as your baseline. Choose one deliberate refinement from the department papers, compare both versions, and record why your chosen version communicates better."
            : "Read the saved contract and quality gates first. Fix missing deliverables before cosmetic polishing. If a note concerns the original recording, retake it; if it concerns timing or layout, revise the edit. Then watch the complete export before resubmitting.");
        if (level == 4)
        {
            Heading(text, "KAPE KULTURA: REQUIRED VS CREATIVE");
            text.AppendLine("Required: coffee-shop set, actor, cup AND packaging; a valid Product Overview before a valid Coffee Use take; continuous 30-45s edit from 0s; EVERY segment at least 2s; no overlays. Both required takes must contain the required subjects throughout their ORIGINAL recordings. Trimming cannot repair invalid recording evidence.");
            text.AppendLine("Creative choices: shot sizes, set color, light model, music, transitions and color grade. Three-point lighting and extra shot-size variety are not mandatory for this contract. Department minimums still apply.");
        }
        Heading(text, "SHORT REFLECTION / NOT SCORED");
        text.AppendLine("Who is the audience? What should they understand or feel? Cite one moment in your cut, explain the design choice, and identify one change supported by the saved feedback. For a crew production, discuss how your role affected the shared result.");
        text.AppendLine("The game checks selected settings and recorded evidence. It does not fully assess originality, audience response, sound mixing or every aspect of visual storytelling.");
        return text.ToString().TrimEnd();
    }

    private static void Heading(StringBuilder text, string value)
    {
        if (text.Length > 0) text.AppendLine();
        text.Append("<b><color=#25446A>").Append(value).AppendLine("</color></b>");
    }
    private static bool Has(string value, params string[] words)
    {
        foreach (string word in words) if (value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    private static bool IsCorrection(string rich, string plain) =>
        Has(rich, "<color=red>", "<color=yellow>", "<color=#A32323>", "<color=#795012>") ||
        plain.StartsWith("-", StringComparison.Ordinal) || plain.StartsWith("~", StringComparison.Ordinal) ||
        plain.StartsWith("Retake mixed/incomplete", StringComparison.OrdinalIgnoreCase);
    private static Revision Note(string key, string title, string why, string action, string check) =>
        new Revision { key = key, title = title, why = why, action = action, check = check };

    private static Revision Match(int page, int level, string note)
    {
        if (page == 0)
        {
            if (Has(note, "backdrop", "set color", "required set", "coffee-shop", "coffee interior", "cafe corner"))
                return Note("set", "Translate the brief into art direction", "Set and color choices establish the product's context and visual identity.", "Reopen the brief and correct the named set or backdrop requirement before recording." + (level == 4 ? " For Kape Kultura, a plain backdrop cannot replace the coffee-shop set." : ""), "Inspect the set in the viewfinder, then check the new pre-production notes after importing a fresh take.");
            if (Has(note, "actor", "pose", "hire"))
                return Note("actor", "Plan performance, not just placement", "An actor's action should make the product's use understandable.", "Correct the saved actor count or pose requirement. Rehearse the action and walking route before recording; keep required subjects unobstructed.", "Watch the full take rather than a single frame. Confirm the required action and subjects stay visible.");
            if (Has(note, "light", "diffusion", "intensity", "tilt"))
                return Lighting(level);
            if (Has(note, "product", "cup", "packaging", "vase", "vehicle"))
                return Note("product", "Prepare the complete product story", "Missing products prevent the commercial from delivering the client's requested message.", "Use the saved note to correct the product or vehicle count and placement. Check both cup and packaging for Kape Kultura; keep required props on set until import.", "Check the brief against the actual set, then confirm the product in the recorded footage.");
        }
        if (page == 1)
        {
            if (Has(note, "camera", "framing", "zoom", "visibility", "subject"))
                return Note("camera", "Build a clear visual hierarchy", "Framing controls what the audience notices first; zoom alone does not create a strong composition.", level == 4
                    ? "Record one overview with the complete cup and packaging, then a use take with the full actor and cup. Leave edge margins and keep them visible for the entire original take."
                    : "Reframe the required subjects, leave safe edge margins, and adjust camera position before changing zoom. Check negative space for any graphics required by the brief.", "Review the first, middle and last frames, then play the whole take to check moving subjects and obstructions.");
            if (Has(note, "light", "intensity", "diffusion", "aim")) return Lighting(level);
        }
        if (page == 2)
        {
            if (level == 4 && Has(note, "product overview", "coffee use", "original recording", "saved evidence", "mixed/incomplete"))
                return Note("coffee", "Show the product before its use", "Overview establishes what is being advertised; use demonstrates its benefit. The game also checks the original take's saved evidence.", "Set the required action BEFORE recording. Retake invalid footage with all required subjects visible throughout. Place a valid Product Overview before a valid Coffee Use take; a wave or seated rest does not qualify.", "Review both original recordings, not only trimmed sections. Then check their order and at least 2s duration on the timeline.");
            if (Has(note, "screen direction", "continuity", "matching shots"))
                return Note("direction", "Keep the viewer oriented", "A direction reversal can make adjacent shots feel disconnected.", "Compare the subject's screen direction in matching shots. Reposition the camera or retake the mismatched shot while preserving the intended direction.", "Play the cut across each join and check whether the subject appears to continue the same movement.");
            if (Has(note, "wide", "medium", "close-up", "coverage", "separate takes", "different SD", "duplicating", "vehicle footage", "recorded Goke"))
                return Note("coverage", "Give each shot a purpose", "Coverage should add information rather than repeat the same image.", "Correct the specific saved coverage requirement using genuinely new recordings where required. Plan establishing context, product detail and action as distinct visual functions.", "Check source identities and shot types in the new result; then ask whether each cut adds something the previous shot did not.");
            if (Has(note, "timing", "required length", "segment", "gaps", "overlaps", "intro", "outro", "start the first clip"))
                return Note("time", "Control timing and editorial rhythm", "Gaps, overlaps or missing sections interrupt the message; duration limits are delivery constraints, not a universal measure of good pacing.", level == 4
                    ? "Start at 0s, close gaps and overlaps, keep trims inside the source, and keep EVERY segment at least 2s. Aim for a continuous 30-45s cut. Undo split pieces that cannot meet the minimum."
                    : "Correct the duration or sequence named in the saved note. Check source limits and joins before trimming; retain full intro/outro cards wherever the brief requires them.", "Click timeline marks to inspect joins, then watch from beginning to end without stopping. Compare the end time with the saved target.");
            if (Has(note, "contrast", "saturation", "brightness", "color grade"))
                return Note("color", "Separate color correction from color style", "Correction protects legibility and consistency; a creative grade supports the intended mood and brand.", "Adjust one control at a time and compare Before / After. Use the brief's stated ranges when graded; they are game-specific checks, not universal artistic rules.", "Look for lost highlight detail, muddy shadows and unnatural product color. Compare adjoining shots as well as the current frame.");
            if (Has(note, "branding", "graphic", "overlay", "logo", "tagline", "title-safe", "readable", "layout"))
                return Note("graphics", "Design for hierarchy and readability", "Graphics should clarify the message without covering the product or competing with each other.", level == 4
                    ? "Remove EVERY logo, tagline and other overlay from this timeline. This contract tells the story through the recorded product and action instead."
                    : "Fix the saved count, placement, animation or hold issue. Give the main message priority, check contrast against the moving image, and keep graphics inside title-safe margins.", level == 4 ? "Play the complete cut and confirm no overlay remains, including outside the current playhead." : "Read each graphic at playback speed and at the intended viewing size. Check that the product remains visible.");
            if (Has(note, "soundtrack", "music", "silent"))
                return Note("sound", "Make sound support the message", "Music can guide rhythm and tone, but choosing a track alone does not establish a good mix.", "Select the soundtrack requested by the saved note, then audition it with the full commercial. Let the message guide your choice instead of cutting mechanically to every beat.", "Listen through the export for interruptions and unsuitable tone. The game checks track selection here, not professional mixing quality.");
            if (Has(note, "transition", "camera motion", "editorial finish"))
                return Note("finish", "Use effects with a reason", "Motion and transitions should guide attention or mark a change, not distract from the product.", "Correct the named tool selection, then use restrained motion and transitions that support the commercial's beginning, emphasis or ending.", "Compare the cut with and without the effect. Keep the version that communicates more clearly while meeting the brief.");
            if (Has(note, "light", "intensity", "diffusion")) return Lighting(level);
            if (Has(note, "performance", "pose", "visibility", "subject"))
                return Note("performance", "Make performance and visibility intentional", "The audience needs to read both the action and its relationship to the advertised product.", "Correct the saved pose or visibility issue in the original shot. Rehearse, clear obstructions and record a replacement take rather than trying to hide missing evidence in the edit.", "Watch the entire replacement take, then check that the new saved assessment confirms the required evidence.");
        }
        return Note("other", "Turn the saved correction into a test", "A revision is useful when it addresses evidence rather than adding decoration.", "Locate the exact requirement in the recorded note below. Change one relevant setup or editing choice, keeping a comparison version.", "Inspect the affected take or timeline section, then check the new result against that same requirement.");
    }
    private static Revision Lighting(int level) => Note("lighting", "Shape the subject with light", "Light direction, softness and contrast affect depth, texture and separation from the background.",
        level == 4 ? "Check power, aim and intensity with the actor in position. Adjust one variable at a time and watch the cup and face; three-point lighting is optional for Kape Kultura."
        : "Correct the saved power, aim, output or diffusion issue first. Compare the subject before and after moving a light. Assign distinct Key, Fill and Back roles when the contract requires them.",
        "Inspect highlights, shadows and subject separation throughout a take. A setup score is not a complete visual critique of the finished image.");
    private static string Focus(int page) => page == 0
        ? "Turn the brief into intentional art direction and a feasible shot plan."
        : page == 1 ? "Direct attention through framing, camera position and lighting."
        : "Shape the message with shot order, pacing, graphics, color and sound.";
    private static string Practice(int page) => page == 0
        ? "Write a short shot plan: purpose, required subjects, action, setup and expected edit point."
        : page == 1 ? "Compare two compositions of the same subject and explain how camera position, negative space and light direction change the emphasis."
        : "Keep a comparison cut. Identify one edit where timing, a graphic or sound changes the audience's understanding, and explain your choice.";
    private static string CreativeQuestion(int page, int level)
    {
        if (page == 0) return "Does the set support the intended audience and brand? Can you justify a prop, color or performance choice instead of selecting it only to gain points?";
        if (page == 1) return level == 4
            ? "Do overview and use shots make the product easy to identify and its use understandable? Is attention on the actor and cup, not distracting background detail? These are reflection questions, not extra shot-size requirements."
            : "What attracts the eye first? Do subject scale, negative space and lighting reinforce the intended message? Explain one alternative composition and why you rejected it.";
        switch (level)
        {
            case 1: return "Does the flower-vase commercial feel calm, elegant or energetic as intended? Do typography, movement, music and color express the same idea? Selecting a tool is not proof that its artistic effect works.";
            case 2: return "Do the two Goke overlays establish a clear hierarchy without hiding the product? Watch at playback speed: can a viewer read the message? Their timing and duration remain your creative choice.";
            case 3: return "Does each Terrari view reveal something new about the vehicle? Do reflections and color feel consistent across the cut? Three distinct recording sources do not by themselves prove strong shot selection.";
            case 4: return "Without overlays, can a viewer understand what Kape Kultura offers and how it is used? Evaluate the progression from overview to use. Music, transitions and color can support that story, but are optional creative choices.";
            default: return "Does wide-to-detail coverage build an intentional Haraya message rather than a checklist montage? Do performance, screen direction, three-point setup, graphics and color feel consistent? Listen to sound separately; the game does not measure a professional audio mix.";
        }
    }
}
