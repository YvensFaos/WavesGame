/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using Core;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UUtils;

namespace UI
{
    public class EndLevelPanelUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI endLevelText;
        [SerializeField, Scene] private string mainMenuScene;
        [SerializeField] private Button firstButton;
        
        private string _nextLevelName;

        public void OpenEndLevelPanel(string nextLevel, bool victory)
        {
            _nextLevelName = nextLevel;
            endLevelText.text = victory ? "Victory" : "Defeat";
            DelayHelper.DelayOneFrame(this,
                () => { EventSystem.current.SetSelectedGameObject(firstButton.gameObject); });
        }

        public void NextLevel()
        {
            DebugUtils.DebugLogMsg($"Loading next level {_nextLevelName}.", DebugUtils.DebugType.System);
            SceneManager.LoadScene(_nextLevelName);
        }

        public void RestartLevel()
        {
            DebugUtils.DebugLogMsg("Restarting level.", DebugUtils.DebugType.System);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void BackToMainMenu()
        {
            DebugUtils.DebugLogMsg($"Loading main menu scene {mainMenuScene}.", DebugUtils.DebugType.System);
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}