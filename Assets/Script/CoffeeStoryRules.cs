using System.Collections.Generic;
using UnityEngine;

// Level 4: product overview followed by actor coffee use. Brief and grading share limits.
public static class CoffeeStoryRules
{
    public const float MinimumSeconds = 30f, MaximumSeconds = 45f;
    public const float MinimumSegmentSeconds = 2f, JoinTolerance = .05f;
    public static string MandatoryChecklist =>
        "1. Choose Cafe Corner or Coffee Interior in the tablet; a plain wall is not enough.\n" +
        "2. Place the coffee cup, Kape packaging and at least one hired actor. Keep them on the set when importing your footage.\n" +
        "3. Record a PRODUCT OVERVIEW: cup and packaging fully visible together.\n" +
        "4. Record COFFEE USE separately: the full actor and cup visible while the actor drinks or uses the coffee machine.\n" +
        "5. In the final edit, show the overview BEFORE coffee use.\n" +
        $"6. Start at 0s; join every clip without gaps or overlaps. EVERY segment, including split pieces, must last at least {MinimumSegmentSeconds:0} seconds.\n" +
        $"7. Deliver {MinimumSeconds:0}-{MaximumSeconds:0} seconds in total. NO logos, taglines or other overlays.";

    public static string RecordingGuide =>
        "OVERVIEW TAKE\nPlace the cup beside its packaging. Keep both completely inside the frame, with a little space from the edges and nothing blocking them. Keep the actor neutral or out of this shot so it is saved as Product Overview.\n\n" +
        "COFFEE-USE TAKE\nEquip the megaphone and click the actor. Click the cup to give it to the actor, then press [Z] until Action to drink. Alternatively, click the coffee machine with the actor selected; keep the cup visible in that shot. Frame the WHOLE actor, including feet, and the cup in the coffee-shop set.\n\n" +
        "START THE ACTION BEFORE RECORDING\nBegin recording only when framing and the required action are ready. Stop recording BEFORE changing action or losing the subjects from view. Visibility and shot type are checked throughout the entire original take. A mixed or incomplete take needs a retake; trimming it later does not repair the saved evidence.\n\n" +
        "Record separate overview and coffee-use takes, with enough usable material for the full edit. Import both recordings at the computer. A wave, neutral pose or seated rest alone is NOT coffee use.";

    public static string EditingGuide =>
        $"FINAL CUT\nPut your Level 4 overview before your Level 4 coffee-use take. Keep at least {MinimumSegmentSeconds:0} seconds of each. Additional shots are welcome, but EVERY timeline segment must also be at least {MinimumSegmentSeconds:0} seconds. Join clips edge to edge from 0s and finish between {MinimumSeconds:0} and {MaximumSeconds:0} seconds. Remove all overlays. No supplied intro or outro is required.\n\n" +
        "EXAMPLE, NOT A FIXED TEMPLATE\n0-10s: product overview. 10-30s: actor drinking coffee in the shop. Total: 30s. Choose your own timings within the required range.\n\n" +
        "EDITOR CONTROLS\nClick a timeline clip to select it. Double-click to trim. Move the red playhead inside the selected clip and press [B] to split there; leave at least 2s on BOTH sides. [Ctrl + Z] undoes an edit. COLOR GRADE changes only the selected clip. [TAB] reopens this brief; [TAB] again returns to editing. Preview the complete cut before exporting and submitting.";

    public const string CreativeChoices =
        "Any actor tier and light model may be used. Set color, shot sizes, music, transitions and color settings are your creative choices. A greeting, seated break, three different shot sizes and three-point lighting are NOT mandatory for this coffee contract. Keep the product readable; no overlays for now.";
    public static int Beat(string pose) => pose == "Wave" ? 1 :
        pose == "Action" || pose == "Using Machine" ? 2 : pose == "Sitting" ? 3 : 0;

    public struct Result
    {
        public bool beats, continuous, duration, brand, overview, coffeeUse;
        public int shortSegments, invalidEvidence;
        public float seconds;
    }

    public static Result Evaluate(Transform timeline, float pixelsPerSecond)
    {
        var result = new Result();
        if (timeline == null || pixelsPerSecond <= 0f) return result;
        var clips = new List<DraggableClip>(timeline.GetComponentsInChildren<DraggableClip>(true));
        clips.RemoveAll(c => !c.isOnTimeline);
        clips.Sort((a, b) => GokeSequence.Left(a).CompareTo(GokeSequence.Left(b)));
        bool overview = false, coffeeUse = false;
        result.continuous = clips.Count > 0;
        float end = 0f;
        for (int i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            float start = GokeSequence.Left(clip) / pixelsPerSecond;
            float seconds = (clip.endFrame - clip.startFrame) / TapeSettings.framesPerSecond;
            if (clip.campaignLevel == 4 && clip.requiredSubjectsVisible && seconds >= MinimumSegmentSeconds)
            {
                overview |= clip.actorPose == "Product Overview";
                coffeeUse |= overview && clip.actorPose == "Coffee Use";
                result.overview |= clip.actorPose == "Product Overview";
                result.coffeeUse |= clip.actorPose == "Coffee Use";
            }
            if (seconds < MinimumSegmentSeconds) result.shortSegments++;
            if (clip.campaignLevel == 4 && (!clip.requiredSubjectsVisible ||
                (clip.actorPose != "Product Overview" && clip.actorPose != "Coffee Use"))) result.invalidEvidence++;
            result.continuous &= Mathf.Abs(start - end) <= JoinTolerance && seconds >= MinimumSegmentSeconds &&
                clip.startFrame >= 0 && clip.endFrame <= clip.totalFrames;
            end = start + seconds;
        }
        result.beats = overview && coffeeUse;
        result.seconds = end;
        result.duration = end >= MinimumSeconds && end <= MaximumSeconds;
        result.brand = true; // Level 4 currently requires no overlays.
        foreach (var graphic in Object.FindObjectsOfType<BrandingClip>(true))
        {
            var overlay = graphic.linkedOverlay;
            if (overlay != null && overlay.isOnTimeline) result.brand = false;
        }
        return result;
    }
}
