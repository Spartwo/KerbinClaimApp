using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIControl : MonoBehaviour
{

    // Values made accessible
    public int mapModeValue = 0;

    // Start is called before the first frame update
    void Start()
    {


    }

    public void onDropdownValueChanged(int value)
    {
        mapModeValue = value;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
