# Multiplayer lobby scene

Create/join routes through `MultiplayerLobby`, appended to Build Settings so existing scene indices remain unchanged. Its Help Desk canvas retains the studio's original Start/Help/GameSetting artwork, but contains no studio, player spawner or gameplay HUD. `MultiplayerRoleManager` installs the lobby camera/session at scene load and only creates studio services outside this scene.

Role selection, locking, readiness and contract choice use the existing authoritative room snapshot. After a valid host START, the manager waits for the room-property callback before closing room entry and synchronously loading `MultiStudio`. The new studio manager restores the snapshot (roles, budget, contract and briefing) rather than keeping scene objects alive. Leaving/disconnecting returns to Main Menu; host migration republishes the room state through the same start path. Legacy direct studio entry retains its lobby fallback.

Verification: Editor and actual standalone-reference compilation passed; the new scene's local file references were checked for dangling IDs. Live two-client host/join/ready/start, host migration, leave/rejoin and build loading still require a multiplayer smoke test with both clients updated.
