using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Helper that fires UnityEvents when this GameObject is enabled or disabled,
/// so other actions can be wired up in the Inspector without extra code.
/// </summary>
public class OnEnableDisable : MonoBehaviour
{
    [Tooltip("Events invoked when this GameObject is enabled / disabled.")]
    public UnityEvent onEnableEvent, onDisableEvent;
    void OnEnable()
    {
        onEnableEvent?.Invoke();
    }
    void OnDisable()
    {
        onDisableEvent?.Invoke();
    }
}
