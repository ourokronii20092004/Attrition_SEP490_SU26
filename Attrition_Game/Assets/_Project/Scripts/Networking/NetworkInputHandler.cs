using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using Attrition.Persistence;

public class NetworkInputHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    private bool _jumpPressed;
    private bool _attackPressed;
    private bool _dashPressed;
    private bool _healthPotionPressed;
    private bool _manaPotionPressed;
    private bool _restPressed;
    private bool _inventoryToggled;

    // Đọc phím đã đổi từ GameSettings (lưu local). Dash chấp nhận cả phím phụ Shift phải.
    private static KeyCode K(GameSettings.InputAction a) => GameSettings.GetKey(a);

    private void Update()
    {
        // One-shot inputs (chỉ bắt lúc nhấn xuống)
        if (Input.GetKeyDown(K(GameSettings.InputAction.Jump)))
            _jumpPressed = true;

        if (Input.GetKeyDown(K(GameSettings.InputAction.LightAttack)))
            _attackPressed = true;

        if (Input.GetKeyDown(K(GameSettings.InputAction.Dash)) || Input.GetKeyDown(KeyCode.RightShift))
            _dashPressed = true;

        if (Input.GetKeyDown(K(GameSettings.InputAction.HealthFlask)))
            _healthPotionPressed = true;

        if (Input.GetKeyDown(K(GameSettings.InputAction.ManaFlask)))
            _manaPotionPressed = true;

        if (Input.GetKeyDown(K(GameSettings.InputAction.Interact)))
            _restPressed = true;

        if (Input.GetKeyDown(K(GameSettings.InputAction.Inventory)))
            _inventoryToggled = true;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // Horizontal: A/D hoặc Arrow keys
        data.horizontalInput = Input.GetAxisRaw("Horizontal");

        // One-shot buttons
        data.buttons.Set(MyButtons.Jump, _jumpPressed);
        data.buttons.Set(MyButtons.Attack, _attackPressed);
        data.buttons.Set(MyButtons.Dash, _dashPressed);
        data.buttons.Set(MyButtons.HealthPotion, _healthPotionPressed);
        data.buttons.Set(MyButtons.ManaPotion, _manaPotionPressed);
        data.buttons.Set(MyButtons.Rest, _restPressed);
        data.buttons.Set(MyButtons.ToggleInventory, _inventoryToggled);

        // Continuous buttons (giữ liên tục)
        data.buttons.Set(MyButtons.Crouch, Input.GetKey(KeyCode.S));
        data.buttons.Set(MyButtons.AttackHold, Input.GetKey(K(GameSettings.InputAction.LightAttack)));
        data.buttons.Set(MyButtons.JumpHeld, Input.GetKey(K(GameSettings.InputAction.Jump)));

        input.Set(data);

        // Reset one-shot flags
        _jumpPressed = false;
        _attackPressed = false;
        _dashPressed = false;
        _healthPotionPressed = false;
        _manaPotionPressed = false;
        _restPressed = false;
        _inventoryToggled = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] payload) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}