using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

// Multiplayer-only overlay; original PSD career interfaces remain untouched.
public sealed class RoleSelectionUI : MonoBehaviour
{
    public static bool Open = true;
    private Vector2 scroll;
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private void Awake() { Open = true; }
    private void Update()
    {
        var key = Keyboard.current;
        if (key != null && (key.tabKey.wasPressedThisFrame || key.escapeKey.wasPressedThisFrame)) Open = !Open;
        if (Crew?.State == null || Crew.State.phase == "lobby") Open = true;
        Cursor.lockState = Open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = Open;
    }
    private void Send(string action, int value = 0) => Crew.Send(new CrewCommand { action = action, value = value });
    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
        var crew = Crew; if (crew == null) return;
        var state = crew.State;
        GUI.Box(new Rect(12, 12, 1256, 58), "CREW STUDIO  |  ROOM " + (PhotonNetwork.CurrentRoom?.Name ?? "—") +
            "  |  TEAM B " + (state?.budget ?? 0) + "\n" + crew.Notice);
        var control = MultiplayerCrewController.Local;
        if (!Open)
        {
            GUI.Label(new Rect(635, 348, 24, 24), "+");
            GUI.Box(new Rect(180, 643, 920, 62), (control?.Hint ?? "Spawning player...") + "\n" +
                (state != null && state.recording ? "RECORDING " + state.shotSize + "  " + ((int)(PhotonNetwork.Time - state.recordStarted)) + "s | R stop" : "Director: Z/X/C poses, B/N marks, K walk, J return, O stop | Camera: R record, wheel zoom"));
            return;
        }
        GUILayout.BeginArea(new Rect(230, 82, 820, 622), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("CREW ROLES — choose one or more; each role belongs to one player.");
        if (state == null) GUILayout.Label("Waiting for room state...");
        else
        {
            foreach (var member in state.members)
            {
                PhotonNetwork.CurrentRoom.Players.TryGetValue(member.id, out var player);
                GUILayout.Label((player?.NickName ?? "Player " + member.id) + "  •  " + (CrewRole)member.roles + (member.ready ? "  READY" : ""));
            }
            GUILayout.BeginHorizontal();
            foreach (CrewRole role in new[] { CrewRole.Director, CrewRole.Camera, CrewRole.Lighting, CrewRole.SetProps })
                if (GUILayout.Button((crew.HasRole(role) ? "✓ " : "") + role, GUILayout.Height(32))) Send("role", (int)role);
            GUILayout.EndHorizontal();
            if (state.phase == "lobby")
            {
                GUILayout.Label("2–4 players. Cover all four roles, ready up, then the host starts. No career progress or money is used.");
                if (GUILayout.Button("READY / NOT READY", GUILayout.Height(36))) Send("ready");
                if (PhotonNetwork.IsMasterClient && GUILayout.Button("START COFFEE COMMERCIAL", GUILayout.Height(36))) Send("start");
            }
            else if (control != null)
            {
                GUILayout.Label("Brief: stage a coffee commercial. Record Wide, Medium and Close takes of at least 5 seconds each. Takes are scored shot data, not exported video.");
                foreach (CrewRole role in new[] { CrewRole.Director, CrewRole.Camera, CrewRole.Lighting, CrewRole.SetProps })
                    if (crew.HasRole(role)) GUILayout.Label(MultiplayerContractManager.Tasks(state, role));
                if (crew.HasRole(CrewRole.Director))
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Hire actor — B900")) control.BeginPlace("actor");
                    if (GUILayout.Button("CALL ACTION")) Send("action");
                    if (GUILayout.Button("SUBMIT TEAM COVERAGE")) Send("submit");
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Select actor with LMB. Aim at a chair/machine/product and click to cue interaction. H clears selection. T previews a new actor position; LMB confirms. O releases the prop and stops the pose.");
                }
                if (crew.HasRole(CrewRole.SetProps))
                {
                    GUILayout.BeginHorizontal();
                    foreach (string kind in new[] { "chair", "cup", "product", "table", "backdrop", "interior" })
                        if (GUILayout.Button(kind + "\nB" + MultiplayerContractManager.Price(kind))) control.BeginPlace(kind);
                    GUILayout.EndHorizontal();
                }
                if (crew.HasRole(CrewRole.Lighting))
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
                foreach (var shot in state.shots) GUILayout.Label(shot.size + " — " + shot.duration.ToString("0.0") + "s — " + Mathf.RoundToInt(shot.score) + "/100");
                if (GUILayout.Button("RETURN TO STUDIO  [TAB]", GUILayout.Height(36))) Open = false;
            }
        }
        if (GUILayout.Button("LEAVE ROOM", GUILayout.Height(32))) { if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(); else UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu"); }
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }
    private void OnDestroy() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
}
