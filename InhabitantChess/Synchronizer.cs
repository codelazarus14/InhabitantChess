using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Synchronizer : MonoBehaviour
{
    // https://answers.unity.com/questions/1601104/how-do-i-synchronise-a-function-called-in-multiple.html
    public UnityEvent OnLerpComplete;

    public static float t { get; private set; }

    void Start()
    {
        if (OnLerpComplete == null)
            OnLerpComplete = new UnityEvent();
    }

    void Update()
    {
        t += 0.5f * Time.deltaTime;
        if (t > 1.0f)
        {
            t = 0.0f;
            if (OnLerpComplete != null)
            {
                OnLerpComplete.Invoke();
            }
        }
    }
}
