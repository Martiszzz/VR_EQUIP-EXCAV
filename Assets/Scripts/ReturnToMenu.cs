using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToMenu : MonoBehaviour
{
    public string menuScene = "Menu";
    public void Go() { SceneManager.LoadScene(menuScene); }
}