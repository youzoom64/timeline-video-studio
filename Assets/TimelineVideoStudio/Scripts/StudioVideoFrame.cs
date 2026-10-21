using System;
using UnityEngine;
using UnityEngine.Video;

namespace TimelineVideoStudio
{
    [RequireComponent(typeof(VideoPlayer))]
    public class StudioVideoFrame : MonoBehaviour
    {
        VideoPlayer player;
        public bool Ready => player != null && player.isPrepared && !pending;
        bool pending = true;
        double requested = -1;

        void Awake()
        {
            player = GetComponent<VideoPlayer>();
            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFrameReady;
        }

        void OnPrepared(VideoPlayer source) { pending = true; }
        void OnFrameReady(VideoPlayer source, long frame) { pending = false; }

        public void Show(double seconds)
        {
            if (!player.isPrepared) return;
            seconds = Math.Max(0, seconds);
            if (Math.Abs(requested - seconds) < 0.5 / Math.Max(1, player.frameRate)) return;
            requested = seconds;
            pending = true;
            player.time = seconds;
            player.Play();
        }
    }
}
