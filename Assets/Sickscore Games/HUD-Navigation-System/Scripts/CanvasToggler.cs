using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Switches what is shown over a detected advertisement with the Quest controller buttons:
/// A = left, B = right, X = straight ahead, Y = U-turn. Depending on <see cref="showVideo"/>
/// either the relaxing video or a direction arrow replaces the blur.
/// Pressing the same button again hides it and shows the blur again.
/// </summary>
public class CanvasToggler : MonoBehaviour
{
    [Header("Object With Image Component")]
    [Tooltip("Object with an Image component that displays the direction arrow.")]
    [SerializeField] private GameObject ObjectsToEnable;
    [Tooltip("Object that plays the replacement video (shown instead of the arrow when Show Video is on).")]
    [SerializeField] private GameObject videoObject;
    [Tooltip("Blur image that covers the advertisement while nothing else is shown.")]
    [SerializeField] private Image blurImage;
    [Tooltip("Parent of all overlay objects; hidden on start when Hide All Objects is on.")]
    [SerializeField] private GameObject parentGameobject;

    [Header("Direction Sprites")]
    [Tooltip("Arrow sprites for the four directions.")]
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;
    [SerializeField] private Sprite upSprite;
    [SerializeField] private Sprite uTurnSprite;

    [Header("Meta Quest Buttons")]
    [Tooltip("Controller buttons that select each direction.")]
    [SerializeField] private OVRInput.Button leftButton = OVRInput.Button.One;     // A
    [SerializeField] private OVRInput.Button rightButton = OVRInput.Button.Two;    // B
    [SerializeField] private OVRInput.Button upButton = OVRInput.Button.Three;     // X
    [SerializeField] private OVRInput.Button uTurnButton = OVRInput.Button.Four;   // Y

    // Image component of ObjectsToEnable, cached in Awake.
    private Image directionImage;
    private Direction currentDirection = Direction.None;
    [Tooltip("On: a button press shows the video. Off: it shows the direction arrow.")]
    public bool showVideo = true; // Public variable to control whether to show the video object
    [Tooltip("If on, the whole overlay (parentGameobject) is hidden at start.")]
    public bool HideAllObects = false; // Public variable to control whether to hide all objects


    // Currently selected direction (None = only the blur is visible).
    private enum Direction
    {
        None,
        Left,
        Right,
        Up,
        UTurn
    }

    // Validate references, cache the arrow image and start with only the blur visible.
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

    // Check the four controller buttons every frame (GetDown = only once per press).
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

    /// <summary>
    /// Shows the video or the arrow for the given direction and hides the blur.
    /// If that direction is already active, everything is hidden again.
    /// </summary>
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

    /// <summary>
    /// Hides arrow and video and shows the blur again.
    /// </summary>
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
