using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Unity.VisualScripting;
using TMPro;
using System.Collections;


public class Menu : MonoBehaviour
{
    [SerializeField] CinemachineCamera menuCamera;
    [SerializeField] CinemachineCamera playCamera;

    [SerializeField] GameObject canvas;
    [SerializeField] List<TMP_Text> fadeElements;

    [SerializeField] float fadeSpeed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        for (float alpha = 1f; alpha >= 0; alpha -= fadeSpeed * Time.deltaTime)
        {
            foreach (var element in fadeElements)
            {
                element.alpha = alpha; 
            }
            yield return null;
        }

        canvas.SetActive(false);
    }

}
