using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Unity.VisualScripting;
using TMPro;
using System.Collections;
using UnityEngine.UIElements;


public class Menu : MonoBehaviour
{
    [SerializeField] CinemachineCamera menuCamera;
    [SerializeField] CinemachineCamera playCamera;

    [SerializeField] GameObject canvas;

    [SerializeField] GameObject menuPanel;
    [SerializeField] GameObject upgradesPanel;

    [SerializeField] float fadeSpeed;

    [SerializeField] BoatSpawner boatSpawner;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        upgradesPanel.SetActive(false);
        menuCamera.Priority = 20;
        playCamera.Priority = 10;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Play()
    {
        SwitchToPlayCamera();
        StartCoroutine(DisableMenu());
    }

    public void Quit()
    {
        Application.Quit();
    }

    void SwitchToPlayCamera()
    {
        menuCamera.Priority = 10;
        playCamera.Priority = 20;
    }

    void SwitchToMenuCamera()
    {
        menuCamera.Priority = 20;
        playCamera.Priority = 10;
    }

    IEnumerator DisableMenu()
    {
        var fadeElements = menuPanel.GetComponentsInChildren<TMP_Text>();
        for (float alpha = 1f; alpha >= 0; alpha -= fadeSpeed * Time.deltaTime)
        {
            foreach (var element in fadeElements)
            {
                element.alpha = alpha; 
            }
            yield return null;
        }
        foreach (var element in fadeElements)
            element.alpha = 0f;

        menuPanel.SetActive(false);

        var unfadeElements = upgradesPanel.GetComponentsInChildren<TMP_Text>();
        foreach (var element in unfadeElements)
            element.alpha = 0f;
        upgradesPanel.SetActive(true);

        for (float alpha = 0f; alpha <= 1f; alpha += fadeSpeed * Time.deltaTime)
        {
            foreach (var element in unfadeElements)
            {
                element.alpha = alpha;
            }
            yield return null;
        }
        foreach (var element in unfadeElements)
            element.alpha = 1f;
        boatSpawner.StartLevel();
    }

}
