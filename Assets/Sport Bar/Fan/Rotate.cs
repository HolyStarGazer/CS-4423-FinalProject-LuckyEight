using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{

    void Update()
    {
        transform.Rotate(Vector3.up * 40f * Time.deltaTime);
    }
}
