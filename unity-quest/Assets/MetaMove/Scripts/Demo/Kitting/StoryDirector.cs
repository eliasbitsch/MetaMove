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
            [Tooltip("Hide after this many seconds; 0 = until the next beat.")]
            public float hold;
        }

        public KittingCell cell;
        public ProximitySpeedController speed;
        public StoryCard card;
        public List<Beat> beats = new List<Beat>();

        string _current;
        bool _welcomed, _storyTold, _stood, _recovered, _emptyAsked;
        float _shownAt, _boundAt = -1f;
        Beat _beat;

        void OnEnable() { if (cell != null) cell.Cue += OnCue; }
        void OnDisable() { if (cell != null) cell.Cue -= OnCue; }

        public static List<Beat> DefaultBeats() => new List<Beat>
        {
            new Beat { key = "welcome", title = "The line is running", caption = "This arm is building kits: an ABB GoFa, life-size, picking gears at full speed. Keep an eye on the tray. It is running low." },
            new Beat { key = "story", title = "No fence", caption = "Arms like this used to stand behind a fence. The safety standard now lets them share the floor with you, on one condition: the machine has to know where you are.", hold = 14f },
            new Beat { key = "refill", title = "Bring the next batch", caption = "The tray is almost empty. The next batch is waiting on the trolley behind you. Pick it up and load it into the rack, before the line stops." },
            new Beat { key = "standstill", title = "Standstill", caption = "It stopped. Not because anything broke. You are inside its reach.", hold = 6f },
            new Beat { key = "recover", title = "Trust comes back slowly", caption = "Step back. Notice that it does not pick up straight away? Slowing down is allowed to be abrupt. Speeding up is not.", hold = 9f },
            new Beat { key = "empty", title = "Clear the empty box", caption = "The robot has given up on the empty box. Take it out at the front and put it on the return lane below." },
            new Beat { key = "guide", title = "Show it the new tray", caption = "The arm still believes in the old box. Pinch the glowing point on the tool and bring it over the new box, from where you stand. Roughly is enough. Then press Teach." },
            new Beat { key = "aligned", title = "Taught", caption = "It found the box and lined itself up. You gave the intent, the machine added the precision.", hold = 6f },
            new Beat { key = "release", title = "Hand it back", caption = "Let go. The line runs again from the box you just showed it, at full speed now that the floor is clear. You never touched the arm.", hold = 12f },
        };

        Beat Find(string key) { foreach (var b in beats) if (b.key == key) return b; return null; }

        void Play(string key)
        {
            var b = Find(key);
            if (b == null || card == null) return;
            _beat = b; _current = key; _shownAt = Time.time;
            int idx = beats.IndexOf(b) + 1;
            card.Show(b.title, b.caption, b.image, b.clip, idx + " / " + beats.Count);
            Debug.Log("[Story] " + key);
        }

        void OnCue(string key)
        {
            switch (key)
            {
                case "low": Play("refill"); break;
                case "guide": Play("guide"); break;
                case "aligned": Play("aligned"); break;
                case "resume": Play("release"); break;
                case "restart": _welcomed = _storyTold = _stood = _recovered = _emptyAsked = false; _boundAt = Time.time; break;
            }
        }

        void Update()
        {
            if (cell == null || card == null || !cell.IsBound) return;
            if (_boundAt < 0f) _boundAt = Time.time;

            if (!_welcomed && Time.time - _boundAt > 1.5f) { _welcomed = true; Play("welcome"); }
            if (_welcomed && !_storyTold && _current == "welcome" && Time.time - _shownAt > 12f) { _storyTold = true; Play("story"); }

            float f = speed != null ? speed.Factor : 1f;
            if (!_stood && f <= 0.001f && cell.State == KittingCell.Phase.Running) { _stood = true; Play("standstill"); }
            if (_stood && !_recovered && _current == "standstill" && f > 0.5f) { _recovered = true; Play("recover"); }

            if (!_emptyAsked && cell.AllowEmptyRemoval && _current != "standstill")
            {
                var ca = cell.trayA != null ? cell.trayA.GetComponent<CarryableTray>() : null;
                if (ca != null && ca.State == CarryableTray.Phase.Waiting) { _emptyAsked = true; Play("empty"); }
            }

            if (_beat != null && _beat.hold > 0f && Time.time - _shownAt > _beat.hold) { card.Hide(); _beat = null; }
        }
    }
}
