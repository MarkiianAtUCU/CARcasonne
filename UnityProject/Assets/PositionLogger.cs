using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionLogger : MonoBehaviour
{
    public string prefix;


    void Update()
    {
        Debug.Log(prefix + " P:" + transform.position + " R:" + transform.rotation + " S:" + transform.localScale);        
    }
}
