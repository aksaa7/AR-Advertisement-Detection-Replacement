using UnityEngine;
using UnityEngine.UI;

public class CanvasToggler : MonoBehaviour
{
    [Header("Object With Image Component")]
    [SerializeField] private GameObject ObjectsToEnable;
    [SerializeField] private GameObject videoObject;
    [SerializeField] private Image blurImage;
    [SerializeField] private GameObject parentGameobject;

    [Header("Direction Sprites")]
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;
    [SerializeField] private Sprite upSprite;
    [SerializeField] private Sprite uTurnSprite;

    [Header("Meta Quest Buttons")]
    [SerializeField] private OVRInput.Button leftButton = OVRInput.Button.One;     // A
    [SerializeField] private OVRInput.Button rightButton = OVRInput.Button.Two;    // B
    [SerializeField] private OVRInput.Button upButton = OVRInput.Button.Three;     // X
    [SerializeField] private OVRInput.Button uTurnButton = OVRInput.Button.Four;   // Y

    private Image directionImage;
    private Direction currentDirection = Direction.None;
    public bool showVideo = true; // Public variable to control whether to show the video object
    public bool HideAllObects = false; // Public variable to control whether to hide all objects


    private enum Direction
    {
        None,
        Left,
        Right,
        Up,
        UTurn
    }

    void Awake()
    {
        if (ObjectsToEnable == null)
        {
            Debug.LogError("CanvasToggler requires an ObjectsToEnable reference.", this);
            enabled = false;
            return;
        }

        directionImage = ObjectsToEnable.GetComponent<Image>();

        if (directionImage == null)
        {
            Debug.LogError("CanvasToggler's ObjectsToEnable must have an Image component.", this);
            enabled = false;
            return;
        }

        if (showVideo && videoObject == null)
        {
            Debug.LogWarning("CanvasToggler has showVideo enabled but no videoObject reference. The direction image will be shown instead.", this);
        }

        HideDirection();
        if(HideAllObects)
        {
            if (parentGameobject != null)
            {
                parentGameobject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("CanvasToggler has HideAllObects enabled but no parentGameobject reference.", this);
            }
        }
    }

    void Update()
    {
        if (OVRInput.GetDown(leftButton))
        {
            ToggleDirection(Direction.Left, leftSprite);
        }

        if (OVRInput.GetDown(rightButton))
        {
            ToggleDirection(Direction.Right, rightSprite);
        }

        if (OVRInput.GetDown(upButton))
        {
            ToggleDirection(Direction.Up, upSprite);
        }

        if (OVRInput.GetDown(uTurnButton))
        {
            ToggleDirection(Direction.UTurn, uTurnSprite);
        }
    }

    private void ToggleDirection(Direction direction, Sprite sprite)
    {
        if (directionImage == null || ObjectsToEnable == null)
        {
            return;
        }

        // Press same direction again = hide image
        if (currentDirection == direction)
        {
            HideDirection();
            return;
        }

        // Press different direction = switch image
        currentDirection = direction;
        directionImage.sprite = sprite;
        ObjectsToEnable.SetActive(true);
        if (showVideo && videoObject != null)
        {
            videoObject.SetActive(true);
            ObjectsToEnable.SetActive(false);
        }
        else
        {
            if (videoObject != null)
            {
                videoObject.SetActive(false);
            }
            ObjectsToEnable.SetActive(true);
            directionImage.enabled = true;

        }
        if (blurImage != null)
            blurImage.enabled = false;
    }

    private void HideDirection()
    {
        currentDirection = Direction.None;

        if (ObjectsToEnable != null)
        {
            ObjectsToEnable.SetActive(false);
        }
        if (videoObject != null)
        {
            videoObject.SetActive(false);
        }
        if (blurImage != null)
        {
            blurImage.enabled = true;
        }
    }
}
