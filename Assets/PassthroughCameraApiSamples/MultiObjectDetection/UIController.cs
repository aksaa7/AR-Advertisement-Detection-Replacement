using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Text targetText;      // The text element to check
    public CanvasGroup targetGroup; // The group to hide/show
    public string triggerWord = "TV";
    
    bool isTVDetected = false;

    void LateUpdate()
    {
        if (targetText == null || targetGroup == null || string.IsNullOrEmpty(triggerWord))
            return;

        // Check if the text contains the trigger word (case-insensitive)
        if (targetText.text.IndexOf(triggerWord, System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            CancelInvoke(nameof(DisableObject));
            targetGroup.alpha = 1f;          // Makes it visible
            targetGroup.interactable = true;  // Enables clicking
            targetGroup.blocksRaycasts = true; // Allows mouse interaction
            
            isTVDetected = true;
        }
        else
        {
            if(isTVDetected)
            {
                if (!IsInvoking(nameof(DisableObject)))
                    Invoke(nameof(DisableObject), 2f); // Delay the disabling by 2 seconds
                return;
            }
            targetGroup.alpha = 0f;
            targetGroup.interactable = false;
            targetGroup.blocksRaycasts = false;
            
        }
    }
    void DisableObject()
    {
        if (targetGroup == null)
            return;

        targetGroup.alpha = 0f;
        targetGroup.interactable = false;
        targetGroup.blocksRaycasts = false;
        isTVDetected = false;
    }
}
