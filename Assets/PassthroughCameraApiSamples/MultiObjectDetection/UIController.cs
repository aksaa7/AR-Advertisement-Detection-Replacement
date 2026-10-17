using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a UI group only while a TV / advertisement is detected.
/// It checks whether the detection label text contains <see cref="triggerWord"/>;
/// after the detection is lost, the group stays visible for 2 more seconds.
/// </summary>
public class UIController : MonoBehaviour
{
    [Tooltip("Detection label text that is checked for the trigger word.")]
    public Text targetText;      // The text element to check
    [Tooltip("UI group that is shown while the trigger word is present.")]
    public CanvasGroup targetGroup; // The group to hide/show
    [Tooltip("Word to look for in the label (case-insensitive).")]
    public string triggerWord = "TV";
    
    // True while (or shortly after) the trigger word was found.
    bool isTVDetected = false;

    // LateUpdate runs after the detection UI has updated its text this frame.
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
    // Called with a 2 s delay after the detection is lost: hide the group again.
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
