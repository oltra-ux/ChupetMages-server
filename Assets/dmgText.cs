using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class dmgText : MonoBehaviour
{
    public GameObject canvas;
    public TMP_Text text;
    public float dmg;
    public void StartText(float damage)
    {
        dmg=damage;
        text.text = dmg.ToString("F0");
    }
}
