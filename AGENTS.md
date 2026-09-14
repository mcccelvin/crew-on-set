# Working in Crew On Set

Unity 2022.3.62f3. This directory is the game project. Start with `Docs/Architecture.md` only when the owning file is unclear; read just the relevant subsystem document afterward.

## Focused investigation

- Search first-party code first: `rg -n "symbol" Assets/Script Assets/UI/Scripts Assets/Player Assets/Editor -g '*.cs'`.
- Use `rg --files` to locate files, then read the matching method and its callers. Avoid dumping entire tutorial managers, scenes, or the repository.
- Do not search generated folders (`Library`, `Temp`, `Logs`, `obj`) or SDKs (`Assets/Photon`, `Assets/PlayFabSDK`, `Assets/PlayFabEditorExtensions`, `Assets/Plugins`) unless the issue points there. Inspect bounded compiler errors when needed.
- Scene/prefab wiring: get the script GUID from its `.meta`, then search that GUID in the specific scene or prefab. Scene names and UI hierarchy names are runtime lookup contracts.
- Keep changes within the requested feature. Update the relevant map entry if responsibility moves. Do not read every document for each task.

## Compatibility

- Preserve existing `.meta` GUIDs, serialized field names, public UnityEvent methods, scene names/build indices, resource keys, save keys and cloud payload formats. Keep user artwork and the PSD-based UI.
- Existing dirty files may contain the user's work. Inspect changes; never reset or overwrite unrelated edits. Keep vendor/generated files untouched for ordinary gameplay work.
- Use the Input System (`Keyboard.current`, input actions), not legacy `UnityEngine.Input` polling.
- `GameSavePrefs` owns selected-career values; account identity and `Options.*` remain global. Continue restores a commercial checkpoint, not unfinished scene/timeline state.
- Internal `Lambormini*` names are compatibility identifiers; the visible product name is Terrari. Do not bulk-rename them.
- New MonoBehaviour types should have matching filenames. Keep runtime and editor-only APIs separated with `#if UNITY_EDITOR` where required. Avoid adding assembly definitions without checking all dependencies.

## Verification

- Compile affected code with both editor and actual standalone-player references. An editor assembly with player defines is not a player-build check.
- Run focused checks for the changed behavior. For UI work, inspect a render or the live view; compilation alone does not verify layout or button wiring.
- State which checks actually ran. Do not claim a full Unity build, live PlayFab sync, or multiplayer session from a script compilation.
