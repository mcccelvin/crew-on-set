using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

// Multiplayer-only overlay; original PSD career interfaces remain untouched.
public sealed partial class RoleSelectionUI : MonoBehaviour
{
    public static bool Open = true;
    private Vector2 scroll;
    private float openedAt;
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private void Awake() { Open = true; openedAt = Time.unscaledTime; CreateLobby(); }
    private void LateUpdate()
    {
        UpdateLobby();
        if (MultiplayerAuthoredUI.Instance != null && MultiplayerAuthoredUI.Instance.Ready && Crew?.State != null && Crew.State.phase != "lobby") return;
        var key = Keyboard.current;
        if (key != null && (key.tabKey.wasPressedThisFrame || key.escapeKey.wasPressedThisFrame)) Open = !Open;
        if (Crew?.State == null || Crew.State.phase == "lobby" || Crew.State.phase == "briefing") Open = true;
        Cursor.lockState = Open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = Open;
    }
    private void Send(string action, int value = 0) => Crew.Send(new CrewCommand { action = action, value = value });
    private void OnGUI()
    {
        if (lobbyRoot != null && (Crew?.State == null || Crew.State.phase == "lobby")) return;
        if (MultiplayerAuthoredUI.Instance != null && MultiplayerAuthoredUI.Instance.Ready && Crew?.State != null && Crew.State.phase != "lobby") return;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
        var crew = Crew; if (crew == null) return;
        bool oldWrap = GUI.skin.label.wordWrap;
        GUI.skin.label.wordWrap = true;
        var state = crew.State;
        GUI.Box(new Rect(12, 12, 1256, 58), "CREW STUDIO  |  ROOM " + (PhotonNetwork.CurrentRoom?.Name ?? "—") +
            "  |  TEAM B " + (state?.budget ?? 0) + "\n" + crew.Notice);
        var control = MultiplayerCrewController.Local;
        if (!Open)
        {
            GUI.Label(new Rect(635, 348, 24, 24), "+");
            GUI.Box(new Rect(180, 643, 920, 62), (control?.Hint ?? "Spawning player...") + "\n" +
                (state != null && state.recording ? "RECORDING " + state.shotSize + "  " + ((int)(PhotonNetwork.Time - state.recordStarted)) + "s | R stop" : "Director: Z cycles animations, B/N marks, K walk, J return, O stop | Camera: R record, wheel zoom"));
            GUI.skin.label.wordWrap = oldWrap;
            return;
        }
        GUILayout.BeginArea(new Rect(230, 82, 820, 622), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("CREW ROLES — choose one or more; each role belongs to one player.");
        if (state == null || !PhotonNetwork.InRoom || !state.members.Any(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber))
        {
            GUILayout.Label(PhotonNetwork.InRoom ? "Joining the host's crew lobby..." : "You are not in a room. Return to Main Menu and join using the room code.");
            if (PhotonNetwork.InRoom && Time.unscaledTime - openedAt > 15)
                GUILayout.Label("The host has not sent the crew setup. Both players need the updated game; have the host create a fresh room.");
            if (PhotonNetwork.InRoom && GUILayout.Button("RETRY LOBBY CONNECTION", GUILayout.Height(36))) crew.RequestRoomState();
        }
        else
        {
            foreach (var member in state.members)
            {
                PhotonNetwork.CurrentRoom.Players.TryGetValue(member.id, out var player);
                GUILayout.Label((player?.NickName ?? "Player " + member.id) + "  •  " + MultiplayerContractManager.RoleName((CrewRole)member.roles) + (member.ready ? "  READY" : ""));
            }
            GUILayout.BeginHorizontal();
            foreach (CrewRole role in new[] { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor })
                if (GUILayout.Button((crew.HasRole(role) ? "✓ " : "") + MultiplayerContractManager.RoleName(role), GUILayout.Height(32))) Send("role", (int)role);
            GUILayout.EndHorizontal();
            if (state.phase == "lobby")
            {
                GUILayout.Label("2–4 players. Cover all four roles, lock roles, then ready up. No career progress or money is used.");
                var local = state.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
                if (GUILayout.Button(local.rolesLocked ? "UNLOCK ROLES" : "LOCK ROLES", GUILayout.Height(36))) Send("lockRoles", local.rolesLocked ? 0 : 1);
                if (GUILayout.Button("READY / NOT READY", GUILayout.Height(36))) Send("ready");
                if (PhotonNetwork.IsMasterClient && GUILayout.Button("START COFFEE COMMERCIAL", GUILayout.Height(36))) Send("start");
            }
            else if (state.phase == "briefing") GUILayout.Label(MultiplayerAuthoredUI.Instance?.Message ?? "Preparing the authored station UI. If this remains, stop Play Mode and use Crew-On-Set > Refresh Multiplayer UI Copies.");
            else if (control != null)
            {
                MultiplayerWorkbench.Instance?.Draw();
                if (MultiplayerWorkbench.Instance == null || MultiplayerWorkbench.Instance.ShowRoleTools)
                {
                GUILayout.Label("Stage a coffee commercial. Buy your kit in SHOP. Record Wide, Medium and Close takes; Editor assembles five seconds of each. ALMANAC explains the controls.");
                foreach (CrewRole role in new[] { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor })
                    if (crew.HasRole(role)) GUILayout.Label(MultiplayerContractManager.Tasks(state, role));
                if (crew.HasRole(CrewRole.Director))
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Hire actor — B900")) control.BeginPlace("actor");
                    if (GUILayout.Button("CALL ACTION")) Send("action");
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Select actor with LMB. Aim at a chair/machine/product and click to cue interaction. H clears selection. T previews a new actor position; LMB confirms. O releases the prop and stops the pose.");
                }
                if (crew.HasRole(CrewRole.AVTechnician))
                {
                    GUILayout.BeginHorizontal();
                    foreach (string kind in new[] { "chair", "cup", "product", "table", "backdrop", "interior" })
                        if (GUILayout.Button(kind + "\nB" + MultiplayerContractManager.Price(kind))) control.BeginPlace(kind);
                    GUILayout.EndHorizontal();
                }
                if (crew.HasRole(CrewRole.AVTechnician))
                {
                    if (GUILayout.Button("Add light — B750")) control.BeginPlace("light");
                    GUILayout.Label("Select a light: Z/X Kelvin, C/V intensity, F power, T move. Aim direction can be adjusted below.");
                    var lamp = crew.Object(control.Selected);
                    if (lamp?.kind == "light")
                    {
                        GUILayout.Label(lamp.kelvin + " K | Intensity " + lamp.intensity);
                        float yaw = GUILayout.HorizontalSlider(lamp.rotation.y, 0, 360);
                        float tilt = GUILayout.HorizontalSlider(lamp.rotation.x, 0, 90);
                        GUILayout.Label("Diffusion " + (int)lamp.diffusion + "%");
                        float diffusion = GUILayout.HorizontalSlider(lamp.diffusion, 0, 100);
                        if (Mathf.Abs(yaw - lamp.rotation.y) > 1 || Mathf.Abs(tilt - lamp.rotation.x) > 1 || Mathf.Abs(diffusion - lamp.diffusion) > 1)
                            crew.Send(new CrewCommand { action = "light", id = lamp.id, value = lamp.powered ? 1 : 0, number = lamp.kelvin, number2 = lamp.intensity, index = (int)diffusion, rotation = new Vector3(tilt, yaw, 0) });
                    }
                }
                if (crew.HasRole(CrewRole.Camera))
                {
                    GUILayout.BeginHorizontal();
                    foreach (string size in new[] { "Wide", "Medium", "Close" })
                        if (GUILayout.Button((control.ShotSize == size ? "✓ " : "") + size)) control.ShotSize = size;
                    GUILayout.EndHorizontal();
                }
                if (crew.HasRole(CrewRole.Editor)) GUILayout.Label("Open FOOTAGE / EDIT to download, preview, trim, order and submit your commercial.");
                }
                if (GUILayout.Button("RETURN TO STUDIO  [TAB]", GUILayout.Height(36))) Open = false;
            }
        }
        var spawner = FindObjectOfType<PUNSpawner>();
        if (spawner != null && !string.IsNullOrEmpty(spawner.Status)) GUILayout.Label(spawner.Status);
        if (GUILayout.Button(PhotonNetwork.InRoom ? "LEAVE ROOM" : "RETURN TO MAIN MENU", GUILayout.Height(32))) { if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(); else UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu"); }
        GUILayout.EndScrollView(); GUILayout.EndArea();
        GUI.skin.label.wordWrap = oldWrap;
    }
    private void OnDestroy() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
}
