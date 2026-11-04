# Optional contract tutorials

The shared tutorial progress overlay is temporarily disabled in `TutorialUIManager` for every contract and editor lesson. Existing overlays are hidden and new ones are not built. Internal progress and tutorial restrictions continue to work.

Each solo contract start, retry or replay asks through the existing Boss dialogue whether to play that contract's tutorial. CONTINUE (Space) runs the level's lessons; SKIP TUTORIAL (Tab) opens the ordinary contract offer, or resumes production if that offer was already accepted. Both buttons support mouse input. The contract folder's usual Tab action is blocked while the choice is open, and the answer is consumed before starting the next dialogue.

`ContractTutorialChoice` presents the choice. `ContractTutorialPolicy` holds one attempt's answer in memory. New games and retries clear it. The answer stays active across the editor and back, without asking again or restarting the studio lesson when returning from editing. Selecting Play can replay studio guidance even when the contract is already accepted. Owned equipment and the existing advance/loan guards are preserved.

Skip joins the existing tutorial-bypass checks for lessons, restrictions, highlights and editor prompts. It does not change the developer switch, grades, tutorial completion evidence or multiplayer unlock. Acceptance uses the normal contract budget. Training room and multiplayer do not show this solo tutorial question.

F12's ENABLE TUTORIALS clears either the developer bypass or an attempt's Skip choice. In the studio it reloads the current contract and consumes a one-shot Play request on the new studio scene, restarting guidance without asking the same question again. In the editor it selects Play for that scene and restarts the appropriate existing lesson in place. New games and retry/reset paths clear pending Play requests.

The choice reuses the Boss canvas and existing green/blue button art. It temporarily reserves room below the Boss text, freezes movement and look, and releases the cursor; closing the question restores those states and the text margins. Pausing suspends choice input.

`Tools/Tests/RunContractTutorialChoiceChecks.ps1` checks attempt isolation, retry/reset behavior and the studio/editor scene distinction against the actual policy source. UI layout and full gameplay progression require Play Mode verification.
