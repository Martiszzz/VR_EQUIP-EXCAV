using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelMenu : MonoBehaviour
{
    public bool IsLoading {get;private set;}
    // Iškvieskite iš Button → OnClick, perduodami scenos pavadinimą (pvz. "SampleScene", "Level2")
    public void Load(string sceneName)
    {
        if(IsLoading)return;
        var presentation=GetComponent<ExcavatorMenuPresentation>();
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            if(presentation)presentation.ShowError("Nepavyko atidaryti lygio. Patikrink scenų sąrašą.");
            Debug.LogError("Scena '" + sceneName + "' nerasta. Pridėkite ją į Build Profiles → Scene List.", this);
            return;
        }
        IsLoading=true;
        StartCoroutine(LoadScene(sceneName,presentation));
    }

    IEnumerator LoadScene(string name,ExcavatorMenuPresentation presentation)
    {
        if(presentation)presentation.ShowLoading(name);
        var operation=SceneManager.LoadSceneAsync(name);
        if(operation==null){IsLoading=false;if(presentation)presentation.RestoreAfterLoadFailure("Nepavyko įkelti lygio. Bandyk dar kartą.");yield break;}
        operation.allowSceneActivation=false;
        float elapsed=0;
        while(operation.progress<0.9f || elapsed<0.4f)
        {
            elapsed+=Time.unscaledDeltaTime;
            if(presentation)presentation.SetProgress(Mathf.Min(operation.progress/0.9f,elapsed/.4f));
            yield return null;
        }
        if(presentation)presentation.SetProgress(1);
        for(float t=0;t<.18f;t+=Time.unscaledDeltaTime)
        {if(presentation && presentation.Group)presentation.Group.alpha=1-t/.18f;yield return null;}
        operation.allowSceneActivation=true;
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