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
        [Tooltip("Silence after a line before the next one starts (s) - time to let it sink in.")]
        public float gapAfterLine = 3f;
        [Tooltip("Extra seconds a timed card stays up after its line ends.")]
        public float lingerAfterLine = 3f;
        float _lastVoiceEnd = -10f;

        string _current;
        // Raised when a beat starts (card up, line playing) - StagedReveal shows its objects.
        public event Action<string> BeatStarted;
        // Key of the beat on the card right now (null when the card is down).
        public string Current => card != null && card.Visible ? _current : null;
        // The opening is told: story and tracking have been said and their cards are gone. The
        // cell holds back the "running low" moment until then - one thing at a time.
        public bool IntroDone { get; private set; }
        FactoryReveal _reveal;
        bool _standWait, _standSaid, _stepTold;
        float _standSaidAt = -1f;
        bool _welcomed, _storyTold, _slowTold, _stood, _recovered, _emptyAsked, _grabTold;
        float _shownAt, _boundAt = -1f;
        Beat _beat;

        void OnEnable() { if (cell != null) cell.Cue += OnCue; }
        void OnDisable() { if (cell != null) cell.Cue -= OnCue; }

        public static List<Beat> DefaultBeats() => new List<Beat>
        {
            new Beat { key = "welcome", title = "The line is running", caption = "Welcome to MetaMove. This is an ABB GoFa, a collaborative robot, life-size. Right now, it is packing gear kits. Watch it work for a moment." },
            new Beat { key = "story", title = "No fence", caption = "Arms like this used to stand behind a fence. The safety standard now lets them share the floor with you, on one condition: the machine has to know where you are.", hold = 14f },
            new Beat { key = "tracking", title = "How it knows", caption = "How does it know? The QR code on the post is the spatial anchor. The headset's own SLAM tracking finds it, and the code is measured to the robot base. So your head and hands are known relative to the robot, and that distance sets the speed.", hold = 16f },
            new Beat { key = "refill", title = "Bring the next batch", caption = "The tray is getting low. The next batch is waiting on the trolley on your left, in the orange box. Pick it up and load it into the rack, while the arm keeps working." },
            new Beat { key = "slow", title = "It knows you are there", caption = "Notice the arm slowing down as you come closer? It measures your distance all the time. The closer you are, the slower it moves.", hold = 9f },
            new Beat { key = "standstill", title = "Standstill", caption = "It stopped. Not because anything broke. You are inside its reach.", hold = 6f },
            new Beat { key = "stepback", title = "A step back, please", caption = "Would you be so kind as to take a step back? Then the arm can carry on, and I can finish showing you around.", hold = 7f },
            new Beat { key = "recover", title = "Trust comes back slowly", caption = "Step back. Notice it does not rush off straight away?", hold = 9f },
            new Beat { key = "empty", title = "Clear the empty box", caption = "The first box is empty. The arm noticed and switched to the reserve box on the left, so the line keeps running. Take the empty box out at the front and put it on the return lane below. Watch the arm while your hands are close." },
            new Beat { key = "guide", title = "Show it the new tray", caption = "The arm still believes in the old box. Pinch the glowing point on the tool and bring it over the new box, from where you stand. Roughly is enough. Then raise your left hand, palm up, and a hand menu opens. Press Teach there once you are happy with the position." },
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
            // The standstill never cuts a line short: it waits for the voice to finish and then
            // goes first, without the usual pause (see Update).
            if (key == "standstill")
            {
                if (!ShiftReport.Speaking && (voiceSource == null || !voiceSource.isPlaying)) PlayNow(key);
                else _standWait = true;
                return;
            }
            if (!QuietLongEnough()) { if (!_pending.Contains(key)) _pending.Enqueue(key); return; }
            PlayNow(key);
        }

        bool QuietLongEnough() => !ShiftReport.Speaking &&
            (voiceSource == null || (!voiceSource.isPlaying && Time.time - _lastVoiceEnd >= gapAfterLine));

        void PlayNow(string key)
        {
            var b = Find(key);
            if (b == null || card == null) return;
            _beat = b; _current = key; _shownAt = Time.time;
            if (key == "standstill") { _standSaid = true; _standSaidAt = Time.time + (b.voice != null ? b.voice.length : 0f); }
            int idx = beats.IndexOf(b) + 1;
            card.Show(b.title, b.caption, b.image, b.clip, idx + " / " + beats.Count, b.voice != null ? b.voice.length : 0f);
            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (b.voice != null) { voiceSource.clip = b.voice; voiceSource.Play(); }
            }
            BeatStarted?.Invoke(key);
            Debug.Log("[Story] " + key + (b.voice != null ? " (" + b.voice.length.ToString("F1") + " s)" : ""));
        }

        // The slow-down line is the heart of the demo. If the visitor never walked through the
        // amber zone at the moment it is told (went straight to the red, or stayed away), it is
        // told before the next task instead - no line of the tutorial is lost.
        void SaySlowIfMissed()
        {
            if (_slowTold) return;
            _slowTold = true;
            Play("slow");
        }

        void OnCue(string key)
        {
            switch (key)
            {
                case "low": Play("refill"); break;
                case "guide": SaySlowIfMissed(); Play("guide"); break;
                case "aligned": Play("aligned"); break;
                case "resume": Play("release"); break;
                case "restart": IntroDone = false; _standWait = _standSaid = _stepTold = false; _standSaidAt = -1f; _welcomed = _storyTold = _slowTold = _stood = _recovered = _emptyAsked = _grabTold = false; _boundAt = Time.time; _pending.Clear(); break;
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

            if (voiceSource != null && voiceSource.isPlaying) _lastVoiceEnd = Time.time;
            if (_pending.Count > 0 && QuietLongEnough()) PlayNow(_pending.Dequeue());
            // Straight away: "...so we brought the factory to you" lands while the hall grows out of the floor.
            // A muted headset holds the welcome (VolumeCheck shows where the button is), 20 s at most.
            if (!_welcomed && Time.time - _boundAt > 0.3f && (!VolumeCheck.Blocking || Time.time - _boundAt > 20f))
            { _welcomed = true; _boundAt = Time.time - 0.3f; Play("welcome"); }
            float welcomeLen = Find("welcome") != null && Find("welcome").voice != null ? Find("welcome").voice.length + 4f : 14f;
            // The story follows the welcome whatever else was said in between (it queues).
            if (_reveal == null) _reveal = FindAnyObjectByType<FactoryReveal>();
            bool hallStands = _reveal == null || _reveal.Done || !_reveal.isActiveAndEnabled;
            if (_storyTold && !IntroDone && Current != "story" && Current != "tracking" && !_pending.Contains("story") && !_pending.Contains("tracking"))
                IntroDone = true;
            if (_welcomed && !_storyTold && hallStands && Time.time - _boundAt > 0.3f + Mathf.Max(8f, welcomeLen)) { _storyTold = true; Play("story"); Play("tracking"); }   // the how follows the why

            float f = speed != null ? speed.Factor : 1f;
            // The slow-down is the point of the demo: name it the first time it happens.
            // Only once the visitor has been sent towards the cell: standing within 2 m from the
            // start is not "coming closer", and it must not push the story aside.
            if (!_slowTold && _storyTold && cell.RefillDue && f < 0.85f && f > 0.001f && cell.State == KittingCell.Phase.Running && _current != "standstill")
            { _slowTold = true; Play("slow"); }
            if (_standWait && !ShiftReport.Speaking && (voiceSource == null || !voiceSource.isPlaying))
            {
                _standWait = false;
                if (f <= 0.001f) PlayNow("standstill");
                else _stood = false;   // they stepped back meanwhile: say it the next time it happens
            }
            if (!_stood && f <= 0.001f && cell.State == KittingCell.Phase.Running) { _stood = true; Play("standstill"); }
            if (_stood && _standSaid && !_recovered && f > 0.5f) { _recovered = true; Play("recover"); }
            // still inside the stop zone well after the standstill was explained: ask politely
            if (_standSaid && !_stepTold && !_recovered && f <= 0.001f && _standSaidAt > 0f && Time.time - _standSaidAt > 8f)
            { _stepTold = true; Play("stepback"); }

            if (!_emptyAsked && cell.AllowEmptyRemoval && _current != "standstill")
            {
                var ca = cell.trayA != null ? cell.trayA.GetComponent<CarryableTray>() : null;
                if (ca != null && ca.State == CarryableTray.Phase.Waiting) { _emptyAsked = true; SaySlowIfMissed(); Play("empty"); }
            }

            // First pinch on the tool: a dry remark, but never over another line.
            if (!_grabTold && cell.State == KittingCell.Phase.Guide && cell.GuideHeld)
            { _grabTold = true; Play("grab"); }

            // A timed beat stays up at least as long as its line is being spoken.
            if (_beat != null && _beat.hold > 0f)
            {
                float speaking = _beat.voice != null ? _beat.voice.length + lingerAfterLine : 0f;
                if (Time.time - _shownAt > Mathf.Max(_beat.hold, speaking)) { card.Hide(); _beat = null; }
            }
        }
    }
}
