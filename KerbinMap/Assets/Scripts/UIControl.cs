using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIControl : MonoBehaviour
{

    // Values made accessible
    public int mapModeValue = 0;

    public GameObject claimPanel;
    public GameObject claimContextPanel;
    public GameObject inspectContextPanel;
    public GameObject provincePanel;
    public GameObject tilePanel;

    public GameObject claimDataField;

    public GameObject fillFullBar;
    public Slider fillSlider;

    // Start is called before the first frame update
    public void Exit()
    {
        Application.Quit();
    }

    public void UpdateClaimUI(float claimBar, string displayData)
    {
        if (claimBar > 1)
        {
            fillFullBar.SetActive(true);
        }
        else
        {
            fillFullBar.SetActive(false);
            fillSlider.value = claimBar;
        }

        claimDataField.GetComponent<TextMeshProUGUI>().text = displayData;
    }

    public void onDropdownValueChanged(int value)
    {
        mapModeValue = value;
        switch(mapModeValue) 
        {
            case 0:
                tilePanel.SetActive(true);
                provincePanel.SetActive(true);
                inspectContextPanel.SetActive(true);
                claimPanel.SetActive(false);
                claimContextPanel.SetActive(false);
                break;
            case 1:
                tilePanel.SetActive(false);
                provincePanel.SetActive(false);
                inspectContextPanel.SetActive(false);
                claimPanel.SetActive(true);
                claimContextPanel.SetActive(true);
                break;
            case 3:
                tilePanel.SetActive(false);
                provincePanel.SetActive(false);
                inspectContextPanel.SetActive(false);
                claimPanel.SetActive(false);
                claimContextPanel.SetActive(false);
                break;
            default:
                tilePanel.SetActive(false);
                provincePanel.SetActive(false);
                inspectContextPanel.SetActive(false);
                claimPanel.SetActive(false);
                claimContextPanel.SetActive(false);
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
