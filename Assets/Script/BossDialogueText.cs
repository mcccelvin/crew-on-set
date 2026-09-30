using UnityEngine;

[System.Serializable]
public sealed class BossDialogueText
{
    [SerializeField, HideInInspector] private string step;
    [SerializeField, HideInInspector] private string original;
    [TextArea(3, 10)] public string text;
    public BossDialogueText(string step, string original)
    {
        this.step = step;
        this.original = original;
        text = original;
    }
    public static string Resolve(BossDialogueText[] entries, string original)
    {
        if (entries != null)
            foreach (var entry in entries)
                if (entry != null && entry.original == original)
                    return string.IsNullOrWhiteSpace(entry.text) ? original : entry.text;
        return original;
    }
    public static BossDialogueText[] Defaults() => new BossDialogueText[]
    {
            new BossDialogueText("Studio / Intro", "Hey, welcome to Crew-On-Set! I run the studio, but you can call me Boss. I'll be here to guide you through your first commercial."),
            new BossDialogueText("Studio / WaitForPrompt", "First day on set? Let me show you around.\n<color=red>[SPACE]</color> Show me the ropes   <color=red>[TAB]</color> Skip the tutorial"),
            new BossDialogueText("Studio / LearnMovement", "Use <color=red>[WASD]</color> to walk, <color=red>[SPACE]</color> to jump, and <color=red>[SHIFT]</color> to sprint. Hold <color=red>[CTRL]</color> for slow, silent footsteps—handy on set."),
            new BossDialogueText("Studio / OfferFirstContract", "Here's our first job: an Artisan Flower Vase commercial. Click <color=red>SELECT</color> to open the brief. Read what the client needs, then close the folder to begin."),
            new BossDialogueText("Studio / SetTrainingObjectAndMoney", "The client needs a few changes. Have a look at the feedback; I've topped your budget back up to <color=yellow>9,000 B-Coins</color> for another take."),
            new BossDialogueText("Studio / SetTrainingObjectAndMoney", "All right, we're on the job. You've got <color=yellow>9,000 B-Coins</color> to work with, and the Floral Vase is ready. Let's build its set."),
            new BossDialogueText("Studio / ExplainPreProduction", "Before we roll, we get the set ready. That's pre-production. Let's start with a backdrop and a place for our vase."),
            new BossDialogueText("Studio / BuildStageWall", "Let's start at the Director Tablet. Walk up to it and press <color=red>[E]</color> when the prompt appears."),
            new BossDialogueText("Studio / ExplainDirectorTablet", "The client asked for a pink backdrop. Let's give that vase a set of its own."),
            new BossDialogueText("Studio / Tablet_AddWall", "We'll need a backdrop behind the vase. Click <color=red>ADD WALL</color> to put one on the stage."),
            new BossDialogueText("Studio / Tablet_SelectWall", "Click the wall to select it. That tells the color sliders which object we're painting."),
            new BossDialogueText("Studio / Tablet_PaintWall", "Let's give the backdrop that pink the client asked for. Set Red to 255, Green to 140, and Blue to 175."),
            new BossDialogueText("Studio / Tablet_SpawnCube", "The vase needs a little height. Click the <color=red>Table</color> card to pick up a display stand with your cursor."),
            new BossDialogueText("Studio / Tablet_MoveCube", "Bring the table over to the stage marker, then <color=red>[Left Click]</color> to set it down."),
            new BossDialogueText("Studio / Tablet_PaintCube", "Keep the table in its original wood finish. Next, place the vase on top."),
            new BossDialogueText("Studio / Tablet_SpawnProp", "Now for the star of the shot. Click the <color=red>Floral Vase</color> card to pick it up with your cursor."),
            new BossDialogueText("Studio / Tablet_MovePropToCube", "Move the vase onto the table, then <color=red>[Left Click]</color> to place it. Leave the whole vase in view."),
            new BossDialogueText("Studio / TabletPracticeFinished", "That's our set! Need to reposition anything? Select it and press <color=red>[T]</color>. When you're done, close the tablet with <color=red>[E]</color> or <color=red>[ESC]</color>."),
            new BossDialogueText("Studio / BuyLight_WalkToShop", "Let's give the flower some light. Head to the Equipment Shop and press <color=red>[E]</color>; we're buying one Stage Light."),
            new BossDialogueText("Studio / BuyLight_AddToCart", "One Stage Light will do for this shot. Find it and click <color=red>ADD TO CART</color>."),
            new BossDialogueText("Studio / BuyLight_Checkout", "That's the one. Click <color=red>CONFIRM</color> to place the order."),
            new BossDialogueText("Studio / BuyLight_CloseShop", "Your light's here! Press <color=red>[SPACE]</color> to finish our chat, then <color=red>[E]</color> to leave the shop. Let's collect it."),
            new BossDialogueText("Studio / PickUpLight", "There's your light on the delivery table. Look at it and press <color=red>[E]</color> to pick it up."),
            new BossDialogueText("Studio / WalkToStageWithLight", "Bring it over to the marker on the pink stage. We'll aim it from there."),
            new BossDialogueText("Studio / TurnOnLight", "Point it toward the flower and click <color=red>[Left Click]</color> once to switch it on."),
            new BossDialogueText("Studio / PracticeLight_Intensity", "Try the <color=red>[Scroll Wheel]</color>. See how the brightness changes? Watch the petals; we don't want to lose their detail."),
            new BossDialogueText("Studio / AdjustLight_Intensity", "Let's settle on 45% for this shot. Use the <color=red>[Scroll Wheel]</color> to dial it in."),
            new BossDialogueText("Studio / PracticeLight_Tilt", "Try tilting the light with the <color=red>[Up/Down Arrows]</color>. Follow the bright patch as it moves across the set."),
            new BossDialogueText("Studio / AdjustLight_Tilt", "Let's aim a little higher. Use the <color=red>[Up/Down Arrows]</color> to bring the tilt to -5°."),
            new BossDialogueText("Studio / DropLight", "There we go. Press <color=red>[G]</color> to set the light down and keep that aim."),
            new BossDialogueText("Studio / ExplainProduction", "The set's built and the light's in place. Now we're into production: getting our shot on camera."),
            new BossDialogueText("Studio / BuyCamera_WalkToShop", "Time to get a camera on this set. Head back to the shop and press <color=red>[E]</color>; we'll need a Film Camera and an SD Card."),
            new BossDialogueText("Studio / BuyCamera_AddToCart", "Find the Film Camera and click <color=red>ADD TO CART</color>. That's our next tool."),
            new BossDialogueText("Studio / BuySDCard_AddToCart", "Don't forget something to record onto. Click <color=red>ADD TO CART</color> under the SD Card."),
            new BossDialogueText("Studio / BuyCamera_Checkout", "Camera and card? We're set. Click <color=red>CONFIRM</color> to order them."),
            new BossDialogueText("Studio / BuyCamera_CloseShop", "Our gear's at the delivery table. Press <color=red>[SPACE]</color> to finish here, then <color=red>[E]</color> to close the shop."),
            new BossDialogueText("Studio / PickUpCamera", "Let's grab the camera first. Look at it on the delivery table and press <color=red>[E]</color>."),
            new BossDialogueText("Studio / PickUpSDCard", "Grab the SD Card with <color=red>[E]</color> too. It'll fit in another hotbar slot."),
            new BossDialogueText("Studio / InsertSDCard", "Select the camera in your hotbar, then press <color=red>[C]</color> to pop the SD Card in."),
            new BossDialogueText("Studio / WalkToStageWithCamera", "Head over to Point C, the Director's mark. Let's see how our set looks through the camera."),
            new BossDialogueText("Studio / EquipCameraView", "With the camera selected, click <color=red>[Left Click]</color> to look through the viewfinder. This is what your audience will see."),
            new BossDialogueText("Studio / PracticeCameraZoom", "Autofocus keeps the vase sharp. Advanced settings unlock in later levels. Try zooming with <color=red>[Scroll]</color>, leaving room for the whole vase."),
            new BossDialogueText("Studio / PracticeCameraPedestal", "Let's try a different height. Hold <color=red>[Q]</color> or <color=red>[E]</color> to raise or lower the camera, and try both directions."),
            new BossDialogueText("Studio / FrameSubject", "For our first shot, put the flower right in the center. Give it enough room so nothing gets cut off."),
            new BossDialogueText("Studio / RecordVideo", "Ready? Press <color=red>[R]</color> to roll. Hold that centered shot for 10 seconds, then press <color=red>[R]</color> again to cut."),
            new BossDialogueText("Studio / PickUpUsedSDCard", "And cut! Your take is on the card the camera just ejected. Look at it and press <color=red>[E]</color> to collect it."),
            new BossDialogueText("Studio / InsertToComputer", "Your recorded SD Card is in your inventory. Select its hotbar slot, look at the computer tower, and press <color=red>[F]</color> to insert it."),
            new BossDialogueText("Studio / OpenComputer", "The card's in. Look at the monitor and press <color=red>[E]</color>; let's see what we shot."),
            new BossDialogueText("Studio / ExplainComputerEditor", "Before we edit, let's watch the take. We're checking the framing, the light, and whether we recorded enough footage."),
            new BossDialogueText("Studio / OpenRecordingsFolder", "Open the <color=red>Recordings</color> folder. Your new take should be in there."),
            new BossDialogueText("Studio / ClickVideoClip", "There's our take. Click the video clip so we can have a look."),
            new BossDialogueText("Studio / PlayVideoClip", "Hit <color=red>PLAY</color>. Watch the vase throughout the take: can you see it clearly, centered and evenly lit?"),
            new BossDialogueText("Studio / ClickBack", "All right, let's get to the edit. Click <color=red>Close</color> or <color=red>Back</color> to return to the computer's main menu."),
            new BossDialogueText("Studio / ClickEditorApp", "Open the <color=red>Editor</color> app. This is where we'll put the commercial together."),
            new BossDialogueText("Studio / ClickConfirmEditor", "Happy with your footage? Click <color=red>CONFIRM</color> to head into editing. This commits the take; we can't return to the studio afterward."),
            new BossDialogueText("Studio / Complete", "That's a wrap on the shoot. Let's head into the edit!"),
            new BossDialogueText("Studio / PostEditComplete", "Your first commercial! You took it all the way from an empty stage. I've unlocked the <color=yellow>Production Almanac</color>; press <color=red>[P]</color> whenever you need a refresher."),
            new BossDialogueText("Studio / OfferLevel1", "One commercial down. I've got another brief on my desk; ready to hear about it?"),
            new BossDialogueText("Studio / Game Explanation 1", "We make short commercials here. You'll build the set, shoot the product, then bring the footage together in the edit."),
            new BossDialogueText("Studio / Game Explanation 2", "Before we touch any gear, we read the <color=red>contract</color>. That's our brief: what the client wants, and what we need to deliver."),
            new BossDialogueText("Studio / Game Explanation 3", "Keep that brief close. Meeting it earns your grade and payment, and I'll walk you through this first job."),
            new BossDialogueText("Editor / ExplainGokePostProduction", "We've got Goke's footage. This time we'll give the commercial a beginning, a product moment, and an ending. You'll add a 2-second intro and a 2-second outro inside the 12-second cut, then finish the sound and color."),
            new BossDialogueText("Editor / ExplainGokePacing", "An ad hasn't got long to catch someone's eye. Let's start the footage at 0 seconds and keep the opening free of dead time."),
            new BossDialogueText("Editor / ExplainGokeVisualHierarchy", "Remember that open space beside the can? That's where our graphics belong. The product should still catch your eye first."),
            new BossDialogueText("Editor / ExplainGokeGraphicTiming", "Give each message a moment to land. Show the Main Logo from 0-5 seconds, then the End Logo from 5-10."),
            new BossDialogueText("Editor / ExplainGokeColorSeparation", "We want that Goke red to stand out without losing the can's detail. We'll balance brightness first, then contrast, then saturation."),
            new BossDialogueText("Editor / ExplainPostProduction", "Welcome to the editor! The Clips panel holds your recorded takes. The large Program Monitor shows the picture at the playhead, the red line on the timeline below. The timeline is your commercial arranged from left to right in seconds. We will place a clip, trim it, add messages and sound, then adjust its colors together."),
            new BossDialogueText("Editor / DragVideoToTimeline", "Drag your take from the Clips panel onto the Video Track below. A clip is one piece of footage. The bank keeps your source; the timeline decides which parts the audience sees and in what order."),
            new BossDialogueText("Editor / PlayPreview", "Press PLAY to watch the timeline. The red playhead moves through time and the Program Monitor shows that moment. Pause stops there; dragging the playback bar lets you inspect a moment. For this first preview, watch the whole take before we trim it."),
            new BossDialogueText("Editor / DoubleClickToTrim", "Let's tidy up the take. Double-click the clip on the timeline to open the Trim Inspector."),
            new BossDialogueText("Editor / TrimLeftHandle", "Try pulling the left pink handle inward. You're choosing where the shot begins, leaving the unwanted opening frames out."),
            new BossDialogueText("Editor / TrimRightHandle", "Now pull the right pink handle inward. That decides where we cut away at the end."),
            new BossDialogueText("Editor / TrimTo10Seconds", "The brief calls for 10.0 seconds. Adjust the handles to that length, then click <color=red>[X]</color> to close the inspector."),
            new BossDialogueText("Editor / PositionVideoAtStart", "Slide the trimmed clip left until it starts at 0.0 seconds. We want the picture there the moment the ad begins."),
            new BossDialogueText("Editor / GoToBrandingPhase", "Let's put the client's name on this. Click the <color=red>BRANDING</color> tab."),
            new BossDialogueText("Editor / ExplainBrandingPhase", "The graphics should help sell the product, without hiding it. Keep them readable and inside the title-safe guide so the edges won't get cut off."),
            new BossDialogueText("Editor / DragLogoToScreen", "Start with <color=red>ECCENTRIC CENTERPIECE</color>, the advertising line. Place it below the product, inside title-safe. The brand name comes second."),
            new BossDialogueText("Editor / ExplainBrandingTimeline", "See that new pink clip? It decides when your graphic appears and how long it stays."),
            new BossDialogueText("Editor / TrimBranding", "Give this first message five seconds. Drag its handles so it starts at 0.0 and ends at 5.0 seconds."),
            new BossDialogueText("Editor / PlayBrandingPreview", "Hit <color=red>PLAY</color> and check the timing. The first graphic should leave at 5 seconds."),
            new BossDialogueText("Editor / DragToOtherTimeline", "Now bring in the second graphic. Find another spot inside title-safe where it won't cover the product."),
            new BossDialogueText("Editor / PositionSecondBranding", "Let the second message take over at 5.0 seconds and end at 10.0. We don't want both talking at once."),
            new BossDialogueText("Editor / ExplainPlayerEditTools", "Now let's shape how the commercial feels. In BRANDING, the four effect buttons control picture movement, text entrances, transitions and music. Click a button to cycle its choices. These choices are used in playback and the final review. We will try each tool, then watch the complete result together."),
            new BossDialogueText("Editor / ChooseCameraMotion", "CAMERA MOTION moves or zooms the recorded picture in the edit; it does not move the studio camera. Slow Push In brings attention toward the product and can make a reveal feel important. Pull Out widens the view. Pan Left or Right shifts attention sideways. These effects crop the image, so keep the whole product safely in frame. Click to try a motion; OFF keeps the original framing."),
            new BossDialogueText("Editor / ChooseGraphicAnimation", "GRAPHIC ANIMATION changes how your advertising line and brand name enter the picture. Fade appears gently and can suit a calm product. Slide Up guides the eye toward the message. Pop feels lively, but can compete with the product. CUT shows the graphic immediately. Choose an entrance, and leave enough time afterward for someone to read the words."),
            new BossDialogueText("Editor / ChooseTransition", "A TRANSITION controls how pictures begin, end or change. Fade In / Out gives the opening and ending a softer finish. Dip to Black briefly darkens the picture, and can separate clips when there is more than one. Straight Cut changes immediately. Transitions temporarily hide the picture, so check that your short commercial still gives the product and messages enough clear screen time."),
            new BossDialogueText("Editor / ChooseMusic", "MUSIC gives the same pictures a different mood and sense of pace. Clean is lighter and relaxed, Energy has a faster beat, and Cinematic feels slower and more dramatic. Try a soundtrack that fits the product: a gentle flower commercial may feel different with a fast beat. Music does not change clip duration. OFF removes the added music. Listen during our preview, not just to the option's name."),
            new BossDialogueText("Editor / PreviewCommercialFinish", "Press PLAY and watch the whole commercial. Notice whether the motion keeps the product in frame, whether each animated message is easy to read, and whether transitions hide anything important. Listen to how the music changes the mood. These choices will carry into the final review. You can revisit the buttons to change your treatment; stronger effects do not automatically make a better advertisement."),
            new BossDialogueText("Editor / PrepareForColorGrade", "One last look at the layout: clear product, readable graphics, safe edges, and one message at a time. Then open <color=red>COLOR GRADE</color>."),
            new BossDialogueText("Editor / ExplainColorGrading", "Color grading changes the look of your recorded picture. We will try brightness, contrast, then saturation. Watch the product in the Program Monitor. 1.00 means unchanged. Every value available on these controls earns full color credit in this first lesson; the controls limit extreme adjustments. Choose your own look and use Before / After to check that petals, shadows and vase edges stay readable."),
            new BossDialogueText("Editor / AdjustBrightness", "Brightness makes the whole image lighter or darker. Move the slider right to brighten or left to darken, or type a number in its value box. Try a small change and pause to see it. Keep detail in the petals and shadows: avoid washed-out whites or a product that disappears into darkness. You can reset to 1.00 afterward."),
            new BossDialogueText("Editor / AdjustContrast", "Contrast controls the difference between light and dark areas. More contrast makes the picture punchier, but can hide detail in shadows. Less contrast softens the picture, but too little looks flat. Try this slider and watch the leaves and vase edges. Choose what looks clear to you, then pause to inspect it."),
            new BossDialogueText("Editor / AdjustSaturation", "Saturation controls color strength. Lower it for softer colors; raise it for richer colors. Too much can make the flowers look fluorescent. It does not fix a dark image: use brightness for that. Try a small change and watch the petals and background, then pause to inspect your result."),
            new BossDialogueText("Editor / ExplainColorSettings", "You have tried all three controls. Before / After compares your grade with the original picture. Reset returns the controls to 1.00. You can keep the original look if it reads best. Take your time comparing the product, not just the background. When ready, press SPACE for the delivery lesson."),
            new BossDialogueText("Editor / ClickExport", "Export prepares your timeline as the finished commercial. You can still adjust your grade before clicking it. Check that the product is clear, the text is readable, and the colors suit the brief. Click EXPORT when you are satisfied; the next screen lets you watch it before submitting to the client."),
            new BossDialogueText("Editor / ExplainReviewPanel", "Here's your final cut. Watch it through once: check the opening, the product, graphic timing, sound, and color. This is our last look before delivery."),
            new BossDialogueText("Editor / ReviewAndSubmit", "Happy it matches the brief? Click <color=red>SUBMIT VIDEO</color> and let's see what the client thinks."),
    };
}
