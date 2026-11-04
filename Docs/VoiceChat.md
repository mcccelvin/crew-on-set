# Multiplayer crew voice

`CrewVoiceChat` is installed by `MultiplayerRoleManager` in the main-menu waiting
room and survives role selection and studio scene loads. It uses the existing
Photon room connection; no additional service account or voice App ID is needed.
All players must run the updated version.

Hold LEFT ALT to talk by default. Audio Options can switch the microphone off,
use push-to-talk or enable open mic; PTT also offers Right Alt and F8. Audio has
incoming voice volume, microphone gain, input-device selection, mute, a local
microphone level test, and a wheel-scroll area with no visible scrollbar.
Changes preview immediately. SAVE persists Options.Voice.* global preferences;
closing or disabling the panel cancels the draft and stops the test. Test audio
is never looped back to speakers. Outside a room no microphone packets are sent.

`CrewVoiceBadge` is a shared clickable microphone icon/status for waiting-room,
role-selection and pause crew cards. It distinguishes no mic, microphone off,
muted, ready and talking. Click your own icon to mute/unmute; click a teammate
to mute/unmute them for yourself. Room-local peer mutes are cleared on leave.
`COS.Mic.V1` is a small availability state on each Photon player. Speaking is
inferred locally from recent audio rather than repeatedly publishing properties.

The transport is a basic 16kHz mono IMA-ADPCM channel, not an extra SDK dependency.
Independently decodable 60ms frames are 490 bytes. Event 178 is unreliable,
encrypted, uncached and sent only to other current-room members on channel 1,
separate from reliable footage transfers (events 180/181). Capture is gated by
room membership, application focus, microphone mode/mute and the PTT key.
Pre-PTT samples are discarded. Open mic has a short voice activity hangover.
Capture stops on focus loss, room exit or microphone disable. Device changes
restart capture; missing/denied devices report unavailable without blocking play.

Per-speaker streaming AudioSources are non-spatial and remain audible during
local pause. They use voice volume independently of SFX/music and still respect
master volume. `CrewVoiceBuffer` provides 120ms prefill, short loss concealment,
duplicate/reordering rejection, wrap-safe sequences, underflow fade and bounded
backlog. Audio-thread reads make no Unity API calls. Oversized/invalid packets,
stale/future timestamps and excess per-sender packet rates are rejected. Voice
frames are not room-cached, written to disk or included in career saves.
This basic channel does not provide acoustic echo cancellation; headset use
avoids speaker-to-microphone feedback.

Validation: `Tools/Tests/RunCrewVoiceChecks.ps1` compiles the real codec and jitter
buffer and runs 75 quality, malformed-data, timestamp/sequence wrap, packet-loss,
bounded-latency and concurrent audio-thread checks. Editor and standalone-player
script references are compiled separately. These checks do not exercise a live
microphone, two-client routing, device permissions or network conditions.

API references: [Photon events](https://doc.photonengine.com/pun/current/gameplay/rpcsandraiseevent/),
[Unity microphone position](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Microphone.GetPosition.html),
[Unity streaming clips](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioClip.Create.html).
