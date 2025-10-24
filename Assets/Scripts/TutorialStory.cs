using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialStory : MonoBehaviour
{
    [Header("Story Settings")]
    public GameObject[] storyImages;   // Array of GameObjects with UI Images
    public float interval = 2f;        // Time between images

    private bool isPlaying = false;
    public void Start()
    {
        DontDestroyOnLoad(this);
    }
    // Call this function from GameManager or wherever needed
    public void TryPlayStory()
    {
        if (PlayerPrefs.GetInt("TutorialPlayed", 0) == 0 && !isPlaying)
        {
            StartCoroutine(PlayStory());
            PlayerPrefs.SetInt("TutorialPlayed", 1);
            PlayerPrefs.Save();
        }
        else
        {
            // If story already played, ensure images are hidden
            foreach (var img in storyImages)
            {
                if (img != null)
                    img.SetActive(false);
            }
        }
    }

    private IEnumerator PlayStory()
    {
        isPlaying = true;

        // Deactivate all images first
        foreach (var img in storyImages)
        {
            if (img != null)
                img.SetActive(false);
        }

        // Play images one by one
        for (int i = 0; i < storyImages.Length; i++)
        {
            if (storyImages[i] != null)
                storyImages[i].SetActive(true);

            yield return new WaitForSeconds(interval);

            if (storyImages[i] != null)
                storyImages[i].SetActive(false);
        }

        isPlaying = false;
    }
}
