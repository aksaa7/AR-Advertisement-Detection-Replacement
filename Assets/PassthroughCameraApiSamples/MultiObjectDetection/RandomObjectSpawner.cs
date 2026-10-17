using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Objects to Spawn")]
    public GameObject[] directions; // Array of objects

    [Header("Distance Range")]
    public float minDistance = 30f;
    public float maxDistance = 200f;
    public Text distanceText; // UI Text to display distance
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
        directions[randomIndex].SetActive(true); // Disable all objects first
        distanceText.text = (int)distance +"m"; // Update distance text
       canvasObj.SetActive(true);
    }
}