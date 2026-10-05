# Saved games

Choose **Play → + → Single**, name the game, and create it. Select an existing save box and press **START** to continue. Additional saves appear in the scrolling grid; there are no page arrows. The original creation panel also offers **Multi** for hosting, while **JOIN** opens the existing room-code flow. Each single-player career has independent money, contract completion, grades, rewards and handbook progress.

Saves use commercial checkpoints. Creating a game saves its starting checkpoint. Passing a commercial automatically saves the next commercial's checkpoint. Continuing restores that checkpoint's budget and progression; it restarts the studio lesson, equipment placement, recordings and unfinished edit. Spending during the unfinished commercial does not reduce the checkpoint budget.

The original shared local career is imported once as **Existing local game**. Original PlayerPrefs are retained. An unfinished first-commercial import uses the existing retry lesson so it can rebuild the studio with a usable budget.

## Storage and accounts

- Local files: `Application.persistentDataPath/CareerSaves/<account hash>/<save GUID>.json`, with `.bak` backups.
- The successful PlayFab login result supplies the player ID. Account identity and display options remain global; gameplay preferences use `GameSavePrefs` while a career is selected. A remembered player ID/name is not authentication: LOG OUT appears only while the save owner matches the SDK's live session. Otherwise SIGN IN opens the existing login screen while retaining local saves.
- Guests have a separate local folder. Another signed-in account has its own folder; guest saves are not silently transferred between accounts.
- Signed-in careers sync through PlayFab Client `GetUserData` and `UpdateUserData`, with private keys named `CrewCareer_v1_<save GUID>`. No server secret key is used.
- Cloud download precedes upload. Failed sync leaves the local save usable and displays a retry message. Conflicting local/cloud checkpoints are kept as separate copies. This is single-player checkpoint storage, not transactional multiplayer state.
- The shared profile's **SYNC ACCOUNT** action uses this same transport for account stats/achievements and B-Coin budgets. `Profile.CareerOrigin.v1` follows a checkpoint into cloud copies so the account profile does not add overlapping lifetime histories twice. This metadata is retained during commercial budget retries; existing save IDs and cloud keys are unchanged. C-Coins are refreshed separately through the authoritative wallet service and are not written into checkpoint cash.
- F12 opens the developer command panel. Its confirmed **RESET CURRENT CAREER** command resets only the current career, retaining other careers, login identity and display options. See `Docs/DeveloperCommands.md`.
- The save list includes SIGN IN TO SYNC / SYNC NOW. Valid SDK sessions automatically reconnect to the save manager; reconnecting the same account preserves the selected career. Session loss stops cloud requests but keeps local progress. Rejected session tickets require sign-in again; ordinary network failures retry automatically. Delayed callbacks are checked against the current SDK identity and manager generation before uploading or acknowledging saves.
- Authentication is not persisted as a password or session ticket in PlayerPrefs. After restarting the application, a remembered name may still be displayed, but signing in again is required to establish the SDK session. Reopening/focusing the application retries cloud sync when an authenticated session is available.

## Verification

`Tools/Tests/RunGameSaveSessionChecks.ps1` executes the real `GameSaveManager.cs`
against a fake SDK and in-memory repository. It checks remembered versus authenticated
identity, automatic binding, same-account career preservation, queued uploads,
network retries, expired authentication, account switches and stale callbacks.
It does not log into PlayFab or change real save files.

Source compilation passed with both editor and player defines. Isolated Unity tests covered independent games/accounts, checkpoint money restoration, completed-level autosave, switching careers, backup recovery, simulated cloud conflicts and the real main-menu Play route. The routing test used minimal destination scenes to isolate the save flow. The new save panel was rendered over the authored main menu and visually checked.

Live cloud round-trip still requires a signed-in test session: create a game, finish a commercial, return to the save list and wait for **Stats and B-Coin budgets synced with PlayFab**. Log in to the same account on a second installation and continue that checkpoint. Disconnect networking to confirm local START remains usable after a failed sync, then reconnect and reopen the save list to retry. No live account credentials were used during automated tests.

API references: [GetUserData](https://learn.microsoft.com/en-us/rest/api/playfab/client/player-data-management/get-user-data), [UpdateUserData](https://learn.microsoft.com/en-us/rest/api/playfab/client/player-data-management/update-user-data).
