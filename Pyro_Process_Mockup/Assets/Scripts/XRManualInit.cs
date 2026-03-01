using UnityEngine;
using UnityEngine.XR.Management;
using System.Collections;

//XR 연결되었을때만 XR on Startup
public class XRManualInit : MonoBehaviour
{
    IEnumerator Start()
    {
        var m = XRGeneralSettings.Instance.Manager;
        if (m.activeLoader == null)
        {
            yield return m.InitializeLoader();
        }
        m.StartSubsystems();
    }
}
