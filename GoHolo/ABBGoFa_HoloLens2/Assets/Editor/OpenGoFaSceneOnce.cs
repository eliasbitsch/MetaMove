using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

public static class OpenGoFaSceneOnce
{
    const string Key = "OpenGoFaSceneOnce.Done";

    [DidReloadScripts]
    static void Run()
    {
        if (SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, true);
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.OpenScene("Assets/GoFa.unity", OpenSceneMode.Single);
        };
    }
}
