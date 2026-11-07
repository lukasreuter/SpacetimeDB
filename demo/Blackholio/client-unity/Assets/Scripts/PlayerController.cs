using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController : MonoBehaviour
{
    private const int SEND_UPDATES_PER_SEC = 20;
    private const float SEND_UPDATES_FREQUENCY = 1f / SEND_UPDATES_PER_SEC;

    [AutoStaticsCleanup]
    public static PlayerController Local { get; private set; }

    private Identity playerIdentity;
    private float lastMovementSendTimestamp;
    private Vector2? lockInputPosition;
    private readonly List<CircleController> ownedCircles = new();

    public string Username => GameManager.Conn.Db.Player.Identity.Find(playerIdentity).Name;
    public int NumberOfOwnedCircles => ownedCircles.Count;
    public bool IsLocalPlayer => this == Local;

    public void Initialize(Player player)
    {
        playerIdentity = player.Identity;
        if (player.Identity == GameManager.LocalIdentity)
        {
            Local = this;
        }
    }

    private void OnDestroy()
    {
        // If we have any circles, destroy them
        foreach (var circle in ownedCircles)
        {
            if (circle != null)
            {
                Destroy(circle.gameObject);
            }
        }
        ownedCircles.Clear();
    }

    public void OnCircleSpawned(CircleController circle)
    {
        ownedCircles.Add(circle);
    }

    public void OnCircleDeleted(CircleController deletedCircle)
    {
        // This means we got eaten
        if (ownedCircles.Remove(deletedCircle) && IsLocalPlayer && ownedCircles.Count == 0)
        {
            GameManager.Instance.deathScreen.SetVisible(true);
        }
    }

    public int TotalMass()
    {
        return ownedCircles.
            Select(static circle => GameManager.Conn.Db.Entity.EntityId.Find(circle.entityId)).
            Sum(static e => e?.Mass ?? 0); //If this entity is being deleted on the same frame that we're moving, we can have a null entity here.
    }

    public Vector2? CenterOfMass()
    {
        if (ownedCircles.Count == 0)
        {
            return null;
        }

        var totalPos = Vector2.zero;
        float totalMass = 0;
        foreach (var circle in ownedCircles)
        {
            if (circle == null)
            {
                continue;
            }

            var entity = GameManager.Conn.Db.Entity.EntityId.Find(circle.entityId);
            var position = circle.transform.position;
            var entityMass = entity?.Mass ?? 0;
            totalPos += (Vector2) position * entityMass;
            totalMass += entityMass;
        }

        return totalPos / totalMass;
    }

    public void Update()
    {
        if (!IsLocalPlayer || NumberOfOwnedCircles == 0)
        {
            return;
        }

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            GameManager.Conn.Reducers.PlayerSplit();
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            if (lockInputPosition.HasValue)
            {
                lockInputPosition = null;
            }
            else
            {
                lockInputPosition = mouse.position.value;
            }
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            GameManager.Conn.Reducers.Suicide();
        }

        // Throttled input requests
        if (Time.time - lastMovementSendTimestamp >= SEND_UPDATES_FREQUENCY)
        {
            lastMovementSendTimestamp = Time.time;

            var mousePosition = lockInputPosition ?? mouse.position.value;
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

    private void OnGUI()
    {
        if (!IsLocalPlayer || !GameManager.IsConnected())
        {
            return;
        }

        GUI.Label(new Rect(0, 0, 100, 50), $"Total Mass: {TotalMass()}");
    }

    //Automated testing members
    private bool testInputEnabled;
    private Vector2 testInput;

    public void SetTestInput(Vector2 input) => testInput = input;
    public void EnableTestInput() => testInputEnabled = true;
}