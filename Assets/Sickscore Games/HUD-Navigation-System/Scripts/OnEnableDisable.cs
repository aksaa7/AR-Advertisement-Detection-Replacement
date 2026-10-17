using UnityEngine;
using UnityEngine.Events;

public class OnEnableDisable : MonoBehaviour
{
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
