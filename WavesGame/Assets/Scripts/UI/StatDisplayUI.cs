/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using Actors;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public class StatDisplayUI : MonoBehaviour, ISelectHandler
    {
        [SerializeField, ReadOnly] private InfoPanelUI infoPanel;
        [SerializeField] private StatSO stat;
        [SerializeField] private Image statImage;
        [SerializeField] private TextMeshProUGUI statText;

        public void Initialize(InfoPanelUI newInfoPanel)
        {
            infoPanel = newInfoPanel;
        }

        public void SetInfoFromStatPair(StatValuePair statPair)
        {
            SetStatText(statPair.Two.ToString());
        }

        public void SetStatText(string statValue)
        {
            statText.text = statValue;
        }

        public void OnSelect(BaseEventData eventData)
        {
            infoPanel?.SetStatSOText(stat);
        }

        public void OnClick()
        {
            infoPanel?.SetStatSOText(stat);
        }

        public bool IsStatOfType(StatSO compareStat)
        {
            return stat == compareStat;
        }
    }
}