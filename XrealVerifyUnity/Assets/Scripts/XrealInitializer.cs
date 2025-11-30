using UnityEngine;

public class XrealInitializer : MonoBehaviour
{
    void Start()
    {
        Debug.Log("XrealInitializer: starting up. Ensure XREAL SDK 3.1.0 is imported and configured.");

        // Example pseudocode if XREAL SDK exposes a runtime init API:
        // if (XrealSDK.IsAvailable) XrealSDK.Initialize();

        // If XREAL requires permission checks for camera / passthrough, ensure they are done before drawing.
    }
}
