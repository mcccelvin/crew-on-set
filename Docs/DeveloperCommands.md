# Developer commands

Press **F12** in the Unity Editor or a **Development Build** to open/close the command panel. Escape and CLOSE also dismiss it. Normal release builds do not install the menu or allow these command methods. Commands are disabled in multiplayer rooms.

The old F4, F5, F6, F8, F9, F10, F11 and Caps Lock + 1–4 cheat bindings are retired. F12 no longer resets a career. F2 remains the camera's ordinary settings control, not a cheat.

Buttons:

- Fast dialogue ON/OFF: instant Boss text and practice waits, with tasks still active.
- Disable tutorials: bypass for this session.
- Skip tutorial step: invokes the existing studio/editor step completion, retaining transition/readiness guards.
- +1,000 / -1,000 B-Coins: changes the current production budget, with no negative balance.
- +100 TEST C-Coins: starts/adds to an isolated session-only test wallet.
- Create 12-second clip: editing-scene test recording.
- Spawn test SD card: studio test card.
- Restore real C-wallet: discards test coins, purchases and equipment and restores the previous account state.
- Load contract 1–4: the existing level-jump commands.
- Reset current career: requires a second click within eight seconds. Another command or closing the panel cancels confirmation. Uses the existing career-scoped reset, not global Unity PlayerPrefs deletion.

`DevCommandsPanel` owns its overlay and remembers the previous pause, time scale, audio pause and cursor state. It blocks input behind the sheet and restores those states on close, including when opened over an already-paused game. Scene-changing/tutorial commands close it before invoking the existing gameplay method. `PauseManager` and `AlmanacManager` ignore the closing Escape frame so it cannot also close an underlying menu.

## Test C-Coins are not paid currency

`CCoinService.DevAddCoins` is compiled only for Editor/Development builds. Test mode replaces the displayed wallet with an in-memory 36-item cosmetic catalog; BUY and EQUIP can be tested offline. Items still cost 10 C-Coins and duplicate purchases do not debit again. It does not change the real account's coins or ownership.

The original state (including pending operations) is retained separately. Test mode blocks cache writes, cloud refresh, reward claims, receipt validation and paid checkout. Late cloud callbacks are invalidated when entering/exiting. Account changes discard test mode; restarting the game also discards it. Restore real C-wallet returns to the retained state and requests ordinary sync. A test balance is labelled **TEST WALLET** in the shop status and never treated as a server-verified balance. Complete test productions in a disposable career: ordinary B-Coin/progress cheats still affect that career's saved gameplay state.

Real paid grants require a deployed, authorized server tool; no client minting API or payment secret is introduced. The PlayFab/PayMongo backend remains undeployed.

Validation: compile with actual Editor and standalone references; test real currency service with release/development defines for persistence, network isolation, duplicate purchases, account changes and late callbacks. Isolated native UI tests cover command dispatch (game handlers stubbed), pause restoration, reset confirmation, room restrictions and four screen sizes. This is not a full game build or live payment test.
