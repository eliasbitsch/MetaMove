using UnityEngine;

namespace MetaMove.AI
{
    // Minimal bench for JarvisTtsClient. Attach alongside JarvisTtsClient to a GameObject,
    // press the keys in Play Mode to hear Jarvis speak.
    [RequireComponent(typeof(JarvisTtsClient))]
    public class JarvisTtsTester : MonoBehaviour
    {
        [Header("Keys → sentences")]
        public KeyCode keyEnglish = KeyCode.Alpha1;
        [TextArea] public string englishText =
            "Welcome home. All systems are operating within nominal parameters.";

        public KeyCode keyGerman = KeyCode.Alpha2;
        [TextArea] public string germanText =
            "Willkommen zu Hause. Alle Systeme arbeiten im Normalbereich.";

        public KeyCode keyRussian = KeyCode.Alpha3;
        [TextArea] public string russianText =
            "Добро пожаловать домой. Все системы работают в пределах нормы.";

        [Header("Variant")]
        public JarvisTtsClient.Variant variant = JarvisTtsClient.Variant.Helmet;

        JarvisTtsClient _client;

        void Awake()
        {
            _client = GetComponent<JarvisTtsClient>();
        }

        void Update()
        {
            if (Input.GetKeyDown(keyEnglish)) _client.Speak(englishText, "en", variant);
            if (Input.GetKeyDown(keyGerman))  _client.Speak(germanText,  "de", variant);
            if (Input.GetKeyDown(keyRussian)) _client.Speak(russianText, "ru", variant);
        }
    }
}
