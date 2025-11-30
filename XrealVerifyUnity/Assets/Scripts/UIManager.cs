using UnityEngine;

public class UIManager : MonoBehaviour
{
    public WeldDrawer weldDrawer; // assign in inspector

    // Called by Start button
    public void OnStartPressed()
    {
        Debug.Log("UI: Start pressed");
        weldDrawer.BeginSession();
    }

    // Called by Contact button
    // Toggles contact on/off to simulate 'touch' events
    public void OnContactPressed()
    {
        Debug.Log("UI: Contact pressed");
        weldDrawer.ToggleContact();
    }

    // Called by End button
    public void OnEndPressed()
    {
        Debug.Log("UI: End pressed");
        weldDrawer.EndSession();
    }
}
