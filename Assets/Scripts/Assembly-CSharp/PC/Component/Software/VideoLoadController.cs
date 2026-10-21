using UnityEngine;
using UnityEngine.Video;

namespace PC.Component.Software
{
    /// <summary>Runs outside the UI window so minimized videos still react to
    /// the same PC's overload. Mining income is not tied to playback speed.</summary>
    public sealed class VideoLoadController : MonoBehaviour
    {
        private OS.OperatingSystem os;
        private VideoPlayer video;
        private bool userWantsPlayback;
        public bool PausedByLoad { get; private set; }

        public void SetUserPlayback(bool playing)
        {
            userWantsPlayback = playing;
            if (!playing) PausedByLoad = false;
        }


        public void Bind(OS.OperatingSystem system, VideoPlayer player)
        {
            os = system;
            video = player;
        }

        private void Update()
        {
            if (video == null) return;
            float speed = os != null && os.Ready
                ? Mathf.Clamp(os.UiResponsiveness, .30f, 1f) : 1f;
            if (video.canSetPlaybackSpeed)
            {
                video.playbackSpeed = speed;
                return;
            }
            // Some hardware decoders (notably on mobile) reject playbackSpeed.
            // Alternate short pauses instead, without resuming a user-paused
            // movie or restarting one that reached its end.
            if (!userWantsPlayback || !video.isPrepared) return;
            if (video.length > 0 && video.time >= video.length - .05f)
            {
                userWantsPlayback = false;
                PausedByLoad = false;
                return;
            }
            bool stall = speed < .98f &&
                Mathf.Repeat(Time.unscaledTime, .30f) < .30f * (1f - speed);
            if (stall && video.isPlaying)
            {
                video.Pause();
                PausedByLoad = true;
            }
            else if (!stall && PausedByLoad)
            {
                PausedByLoad = false;
                video.Play();
            }
        }
    }
}
