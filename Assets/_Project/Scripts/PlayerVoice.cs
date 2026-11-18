using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Per-player voice behaviour, on the player prefab's "Voice" child next to
/// VoiceNetworkObject + Recorder + Speaker.
///
/// - Local player: push-to-talk (hold V by default) or open mic.
/// - Everyone: shows a small indicator above the capsule while that player is talking.
/// Voice is 3D: the Speaker's AudioSource is spatial, so closer players are louder.
/// </summary>
[RequireComponent(typeof(VoiceNetworkObject))]
public class PlayerVoice : NetworkBehaviour
{
    public enum TalkMode { PushToTalk, OpenMic }

    [SerializeField] private TalkMode mode = TalkMode.PushToTalk;
    [SerializeField] private Key pushToTalkKey = Key.V;
    [Tooltip("Shown while this player is talking.")]
    [SerializeField] private GameObject speakingIndicator;

    private VoiceNetworkObject _voice;

    /// <summary>Local player's current talk mode; flip at runtime from UI if needed.</summary>
    public TalkMode Mode { get => mode; set => mode = value; }

    private void Awake()
    {
        _voice = GetComponent<VoiceNetworkObject>();
        if (speakingIndicator != null)
        {
            speakingIndicator.SetActive(false);
            if (speakingIndicator.TryGetComponent<Renderer>(out var r))
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.3f, 1f, 0.4f)); // bright green "talking" dot
                r.SetPropertyBlock(block);
            }
        }
    }

    public override void Spawned()
    {
        // Open mic uses voice detection so silence isn't transmitted; push-to-talk sends while held.
        var recorder = GetComponent<Recorder>();
        if (recorder != null && _voice.IsLocal)
        {
            recorder.VoiceDetection = mode == TalkMode.OpenMic;
            recorder.TransmitEnabled = mode == TalkMode.OpenMic;
        }
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;

        if (_voice.IsLocal && _voice.RecorderInUse != null)
        {
            bool transmit = mode == TalkMode.OpenMic
                || (Keyboard.current != null && Keyboard.current[pushToTalkKey].isPressed);

            if (_voice.RecorderInUse.TransmitEnabled != transmit)
                _voice.RecorderInUse.TransmitEnabled = transmit;
        }

        if (speakingIndicator != null)
        {
            // Local: we're sending audio. Remote: their audio is playing on our Speaker.
            bool talking = _voice.IsLocal ? _voice.IsRecording : _voice.IsSpeaking;
            if (speakingIndicator.activeSelf != talking)
                speakingIndicator.SetActive(talking);
        }
    }
}
