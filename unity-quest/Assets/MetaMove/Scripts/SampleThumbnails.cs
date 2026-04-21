using UnityEngine;

namespace MetaMove.Sandbox
{
    // Holds references to Meta sample thumbnail Sprites so Unity's build pipeline
    // actually packages them. The asset lives in a Resources folder and its
    // references are resolved via AssetDatabase at build time by the editor
    // script SampleThumbnailsAuthor.
    [CreateAssetMenu(menuName = "MetaMove/Sample Thumbnails")]
    public class SampleThumbnails : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string sceneName;
            public Sprite sprite;
        }

        public Entry[] entries;

        public Sprite Get(string sceneName)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].sceneName == sceneName)
                    return entries[i].sprite;
            return null;
        }
    }
}
