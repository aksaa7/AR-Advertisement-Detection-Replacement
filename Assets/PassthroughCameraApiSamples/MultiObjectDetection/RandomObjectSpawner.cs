using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Shows one randomly chosen direction object together with a random distance (e.g. "120m")
/// when the A button on the right controller is pressed. Used for the navigation part of the study.
/// </summary>
public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Objects to Spawn")]
    [Tooltip("Direction objects; exactly one of them is shown at a time.")]
    public GameObject[] directions; // Array of objects

    [Header("Distance Range")]
    [Tooltip("Smallest distance (meters) that can be displayed.")]
    public float minDistance = 30f;
    [Tooltip("Largest distance (meters) that can be displayed.")]
    public float maxDistance = 200f;
    [Tooltip("Text that shows the random distance.")]
    public Text distanceText; // UI Text to display distance
    [Tooltip("Canvas that is made visible when a direction is shown.")]
    public GameObject canvasObj;

    // Update is called once per frame
    void Update()
    {
        // Check A button press on right hand controller
        if (IsAPressed())
        {
            SpawnRandomObject();
        }
    }

    // True while the A button (primary button, right controller) is held down.
    // Note: this is checked every frame, so holding A keeps picking new directions.
    bool IsAPressed()
    {
        bool aButton = false;
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out aButton) && aButton)
        {
            return true;
        }
        return false;
    }

    // Hide all direction objects, show one random one and display a random distance.
    void SpawnRandomObject()
    {
        if (directions.Length == 0) return;

        // Pick random object
        int randomIndex = Random.Range(0, directions.Length);
        GameObject selectedObject = directions[randomIndex];

        // Pick random distance
        float distance = Random.Range(minDistance, maxDistance);
        for (int i = 0; i < directions.Length; i++)
        {
            directions[i].SetActive(false); // Disable all objects first
        }
        directions[randomIndex].SetActive(true); // Enable only the chosen object
        distanceText.text = (int)distance +"m"; // Update distance text
       canvasObj.SetActive(true);
    }
}