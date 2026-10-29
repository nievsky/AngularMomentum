using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnEnd : MonoBehaviour
{
    [SerializeField] private TypeWritterEffect endsScene;
    [SerializeField] private int targetSceneBuildIndex = -1;
    private bool _isLoading;

    private void Awake()
    {
        endsScene = FindObjectOfType<TypeWritterEffect>(true);
    }
    private void Update()
    {
        if (!_isLoading && endsScene != null && endsScene.isEnded)
        {
            _isLoading = true;
            int sceneIndex = targetSceneBuildIndex >= 0
                ? targetSceneBuildIndex
                : SceneManager.GetActiveScene().buildIndex + 1;

            SceneTransitionManager.LoadScene(sceneIndex);
        }
    }
    
    
}
