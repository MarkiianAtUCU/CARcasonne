using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomAnchor : MonoBehaviour
{

    public Transform[] corners;

    public Vector3[] getWorldPoints()
    {
        Vector3[] res = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            res[i] = corners[i].position;
        }

        return res;
    }
}
