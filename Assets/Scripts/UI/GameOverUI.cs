using UnityEngine;
using UnityEngine.UI;

namespace TripeaksSolitaire.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel;
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;
        public TMPro.TextMeshProUGUI buttonText;

        [Header("Colors")]
        public Color victoryColor = new Color(0.2f, 0.8f, 0.2f); // Green
        public Color defeatColor = new Color(0.8f, 0.2f, 0.2f);  // Red

        private void Start()
        {
            // Hide panel at start
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void ShowVictory(bool isCloseWin = false)
        {
            if (panel == null) return;

            panel.SetActive(true);

            if (titleText != null)
            {
                titleText.text = "VICTORY!";
                titleText.color = victoryColor;
            }

            if (subtitleText != null)
            {
                if (isCloseWin)
                {
                    subtitleText.text = " CLOSE WIN! ";
                    subtitleText.gameObject.SetActive(true);
                }
                else
                {
                    subtitleText.text = "";
                    subtitleText.gameObject.SetActive(false);
                }
            }

            if (buttonText != null)
            {
                buttonText.text = "Play Again";
            }
        }

        public void ShowDefeat(string reason = "")
        {
            if (panel == null) return;

            panel.SetActive(true);

            if (titleText != null)
            {
                titleText.text = "GAME OVER";
                titleText.color = defeatColor;
            }

            if (subtitleText != null)
            {
                if (!string.IsNullOrEmpty(reason))
                {
                    subtitleText.text = reason;
                    subtitleText.gameObject.SetActive(true);
                }
                else
                {
                    subtitleText.text = "";
                    subtitleText.gameObject.SetActive(false);
                }
            }

            if (buttonText != null)
            {
                buttonText.text = "Try Again";
            }
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
    }
}
