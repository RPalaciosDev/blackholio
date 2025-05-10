using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
	const int SEND_UPDATES_PER_SEC = 20;
	const float SEND_UPDATES_FREQUENCY = 1f / SEND_UPDATES_PER_SEC;

    public static PlayerController Local { get; private set; }

	private uint PlayerId;
    private float LastMovementSendTimestamp;
    private Vector2? LockInputPosition;
	private List<CircleController> OwnedCircles = new List<CircleController>();
    private PlayerInput playerInput;
    private InputAction mousePositionAction;
    private InputAction lockInputAction;

	public string Username => GameManager.Conn.Db.Player.PlayerId.Find(PlayerId).Name;
	public int NumberOfOwnedCircles => OwnedCircles.Count;
	public bool IsLocalPlayer => this == Local;

	private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = gameObject.AddComponent<PlayerInput>();
        }

        // Get or create input actions
        var actions = new InputActionMap("Player");
        mousePositionAction = actions.AddAction("MousePosition", binding: "<Mouse>/position");
        lockInputAction = actions.AddAction("LockInput", binding: "<Keyboard>/q");
        
        mousePositionAction.Enable();
        lockInputAction.Enable();
    }

    private void OnDestroy()
    {
        if (mousePositionAction != null)
        {
            mousePositionAction.Disable();
            mousePositionAction.Dispose();
        }
        if (lockInputAction != null)
        {
            lockInputAction.Disable();
            lockInputAction.Dispose();
        }

        // If we have any circles, destroy them
        foreach (var circle in OwnedCircles)
        {
            if (circle != null)
            {
                Destroy(circle.gameObject);
            }
        }
        OwnedCircles.Clear();
    }

    public void Initialize(Player player)
    {
        PlayerId = player.PlayerId;
        if (player.Identity == GameManager.LocalIdentity)
        {
            Local = this;
        }
	}

    public void OnCircleSpawned(CircleController circle)
    {
        OwnedCircles.Add(circle);
    }

    public void OnCircleDeleted(CircleController deletedCircle)
	{
		// This means we got eaten
		if (OwnedCircles.Remove(deletedCircle) && IsLocalPlayer && OwnedCircles.Count == 0)
		{
			// DeathScreen.Instance.SetVisible(true);
		}
	}

	public uint TotalMass()
    {
        return (uint)OwnedCircles
            .Select(circle => GameManager.Conn.Db.Entity.EntityId.Find(circle.EntityId))
			.Sum(e => e?.Mass ?? 0); //If this entity is being deleted on the same frame that we're moving, we can have a null entity here.
	}

    public Vector2? CenterOfMass()
    {
        if (OwnedCircles.Count == 0)
        {
            return null;
        }
        
        Vector2 totalPos = Vector2.zero;
        float totalMass = 0;
        foreach (var circle in OwnedCircles)
        {
            var entity = GameManager.Conn.Db.Entity.EntityId.Find(circle.EntityId);
            var position = circle.transform.position;
            totalPos += (Vector2)position * entity.Mass;
            totalMass += entity.Mass;
        }

        return totalPos / totalMass;
	}

	private void OnGUI()
	{
		if (!IsLocalPlayer || !GameManager.IsConnected())
		{
			return;
		}

		// Create a style for the text
		GUIStyle style = new GUIStyle(GUI.skin.label);
		style.alignment = TextAnchor.MiddleCenter;
		style.fontSize = 20;
		style.normal.textColor = Color.white;

		// Calculate the position for centered text
		float width = 200f;
		float height = 50f;
		float x = (Screen.width - width) / 2;
		float y = 20f; // 20 pixels from the top

		// Draw the username
		GUI.Label(new Rect(x, y, width, height), Username, style);
		
		// Draw the total mass below the username
		GUI.Label(new Rect(x, y + height, width, height), $"Total Mass: {TotalMass()}", style);
	}

	//Automated testing members
	private bool testInputEnabled;
	private Vector2 testInput;

	public void SetTestInput(Vector2 input) => testInput = input;
	public void EnableTestInput() => testInputEnabled = true;

    public void Update()
    {
        if (!IsLocalPlayer || NumberOfOwnedCircles == 0)
        {
            return;
        }

        if (lockInputAction.WasPerformedThisFrame())
        {
            if (LockInputPosition.HasValue)
            {
                LockInputPosition = null;
            }
            else
            {
                LockInputPosition = mousePositionAction.ReadValue<Vector2>();
            }
        }

        // Throttled input requests
        if (Time.time - LastMovementSendTimestamp >= SEND_UPDATES_FREQUENCY)
        {
            LastMovementSendTimestamp = Time.time;

            var mousePosition = LockInputPosition ?? mousePositionAction.ReadValue<Vector2>();
            var screenSize = new Vector2
            {
                x = Screen.width,
                y = Screen.height,
            };
            var centerOfScreen = screenSize / 2;

            var direction = (mousePosition - centerOfScreen) / (screenSize.y / 3);
            if (testInputEnabled) { direction = testInput; }
            GameManager.Conn.Reducers.UpdatePlayerInput(direction);
        }
    }
}