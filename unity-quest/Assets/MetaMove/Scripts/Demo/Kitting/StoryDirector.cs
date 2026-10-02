using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // Drives the story card through the beats of the demo script (Regie, Fassung 3.1),
    // from what actually happens in the cell - never on a timer alone, so the presenter
    // can talk alongside and a slow visitor is never talked over.
    //
    // Every beat has an optional picture or clip; empty slots show the tutorial placeholder.
    public class StoryDirector : MonoBehaviour
    {
        [Serializable]
        public class Beat
        {
            public string key;
            public string title;
            [TextArea(2, 4)] public string caption;
            public Sprite image;
            public VideoClip clip;
            [Tooltip("Spoken line (Qwen3-TTS, cloned voice), same text as the caption.")]
            public AudioClip voice;
            [Tooltip("Hide after this many seconds; 0 = until the next beat.")]
            public float hold;
        }

        public KittingCell cell;
        public ProximitySpeedController speed;
        public StoryCard card;
        [Tooltip("Plays the spoken lines; 2D, it is the narrator in the visitor's ear.")]
        public AudioSource voiceSource;
        public List<Beat> beats = new List<Beat>();

        string _current;
        bool _welcomed, _storyTold, _slowTold, _stood, _recovered, _emptyAsked, _grabTold;
        float _shownAt, _boundAt = -1f;
        Beat _beat;

        void OnEnable() { if (cell != null) cell.Cue += OnCue; }
        void OnDisable() { if (cell != null) cell.Cue -= OnCue; }

        public static List<Beat> DefaultBeats() => new List<Beat>
        {
            new Beat { key = "welcome", title = "The line is running", caption = "Welcome to MetaMove. This is an ABB GoFa, a collaborative robot, life-size, packing gear kits. Watch it work for a moment." },
            new Beat { key = "story", title = "No fence", caption = "Arms like this used to stand behind a fence. The safety standard now lets them share the floor with you, on one condition: the machine has to know where you are.", hold = 14f },
            new Beat { key = "refill", title = "Bring the next batch", caption = "The tray is getting low. The next batch is waiting on the trolley behind you, in the orange box. Pick it up and load it into the rack, while the arm keeps working." },
            new Beat { key = "slow", title = "It knows you are there", caption = "Notice the arm slowing down as you come closer? It measures your distance all the time. The closer you are, the slower it moves.", hold = 9f },
            new Beat { key = "standstill", title = "Standstill", caption = "It stopped. Not because anything broke. You are inside its reach.", hold = 6f },
            new Beat { key = "recover", title = "Trust comes back slowly", caption = "Step back. Notice that it does not pick up straight away? Slowing down is allowed to be abrupt. Speeding up is not.", hold = 9f },
            new Beat { key = "empty", title = "Clear the empty box", caption = "The first box is empty. The arm noticed and switched to the reserve box on the left, so the line keeps running. Take the empty box out at the front and put it on the return lane below. Watch the arm while your hands are close." },
            new Beat { key = "guide", title = "Show it the new tray", caption = "The arm still believes in the old box. Pinch the glowing point on the tool and bring it over the new box, from where you stand. Roughly is enough. Then press Teach." },
            new Beat { key = "grab", title = "Hand guiding", caption = "Steady. I will follow your hand anywhere, except into the rack. Someone has to be sensible here.", hold = 7f },
            new Beat { key = "aligned", title = "Taught", caption = "It found the box and lined itself up. You gave the intent, the machine added the precision.", hold = 6f },
            new Beat { key = "release", title = "Hand it back", caption = "Let go. The line runs again from the box you just showed it, at full speed now that the floor is clear. You never touched the arm.", hold = 12f },
        };

        Beat Find(string key) { foreach (var b in beats) if (b.key == key) return b; return null; }

        // Beats wait for the line being spoken to finish; only the standstill cuts in,
        // because that one has to be heard while it is happening.
        readonly Queue<string> _pending = new Queue<string>();

        void Play(string key)
        {
            bool urgent = key == "standstill";
            if (!urgent && voiceSource != null && voiceSource.isPlaying) { if (!_pending.Contains(key)) _pending.Enqueue(key); return; }
            PlayNow(key);
        }

        void PlayNow(string key)
        {
            var b = Find(key);
            if (b == null || card == null) return;
            _beat = b; _current = key; _shownAt = Time.time;
            int idx = beats.IndexOf(b) + 1;
            card.Show(b.title, b.caption, b.image, b.clip, idx + " / " + beats.Count, b.voice != null ? b.voice.length : 0f);
            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (b.voice != null) { voiceSource.clip = b.voice; voiceSource.Play(); }
            }
            Debug.Log("[Story] " + key + (b.voice != null ? " (" + b.voice.length.ToString("F1") + " s)" : ""));
        }

        void OnCue(string key)
        {
            switch (key)
            {
                case "low": Play("refill"); break;
                case "guide": Play("guide"); break;
                case "aligned": Play("aligned"); break;
                case "resume": Play("release"); break;
                case "restart": _welcomed = _storyTold = _slowTold = _stood = _recovered = _emptyAsked = _grabTold = false; _boundAt = Time.time; _pending.Clear(); break;
            }
        }

        void Update()
        {
            if (cell == null || card == null || !cell.IsBound) return;
            if (_boundAt < 0f) _boundAt = Time.time;
            // Expert run: from memory - no card, no voice.
            if (cell.Expert)
            {
                if (card.Visible) card.Hide();
                if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
                _pending.Clear();
                return;
            }

            if (_pending.Count > 0 && (voiceSource == null || !voiceSource.isPlaying)) PlayNow(_pending.Dequeue());
            // Straight away: "...so we brought the factory to you" lands while the hall grows out of the floor.
            if (!_welcomed && Time.time - _boundAt > 0.3f) { _welcomed = true; Play("welcome"); }
            float welcomeLen = Find("welcome") != null && Find("welcome").voice != null ? Find("welcome").voice.length + 2f : 12f;
            // The story follows the welcome whatever else was said in between (it queues).
            if (_welcomed && !_storyTold && Time.time - _boundAt > 0.3f + Mathf.Max(8f, welcomeLen)) { _storyTold = true; Play("story"); }

            float f = speed != null ? speed.Factor : 1f;
            // The slow-down is the point of the demo: name it the first time it happens.
            // Only once the visitor has been sent towards the cell: standing within 2 m from the
            // start is not "coming closer", and it must not push the story aside.
            if (!_slowTold && _storyTold && cell.RefillDue && f < 0.85f && f > 0.001f && cell.State == KittingCell.Phase.Running && _current != "standstill")
            { _slowTold = true; Play("slow"); }
            if (!_stood && f <= 0.001f && cell.State == KittingCell.Phase.Running) { _stood = true; Play("standstill"); }
            if (_stood && !_recovered && _current == "standstill" && f > 0.5f) { _recovered = true; Play("recover"); }

            if (!_emptyAsked && cell.AllowEmptyRemoval && _current != "standstill")
            {
                var ca = cell.trayA != null ? cell.trayA.GetComponent<CarryableTray>() : null;
                if (ca != null && ca.State == CarryableTray.Phase.Waiting) { _emptyAsked = true; Play("empty"); }
            }

            // First pinch on the tool: a dry remark, but never over another line.
            if (!_grabTold && cell.State == KittingCell.Phase.Guide && cell.GuideHeld && (voiceSource == null || !voiceSource.isPlaying))
            { _grabTold = true; PlayNow("grab"); }

            // A timed beat stays up at least as long as its line is being spoken.
            if (_beat != null && _beat.hold > 0f)
            {
                float speaking = _beat.voice != null ? _beat.voice.length + 1.5f : 0f;
                if (Time.time - _shownAt > Mathf.Max(_beat.hold, speaking)) { card.Hide(); _beat = null; }
            }
        }
    }
}
