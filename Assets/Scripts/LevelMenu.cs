using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelMenu : MonoBehaviour
{
    // Iškvieskite iš Button → OnClick, perduodami scenos pavadinimą (pvz. "SampleScene", "Level2")
    public void Load(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scena '" + sceneName + "' nerasta. Pridėkite ją į Build Profiles → Scene List.", this);
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}