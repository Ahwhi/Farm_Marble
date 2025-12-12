using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterManager {
    public MyPlayer _myPlayer;
    public Dictionary<int, Player> _players = new Dictionary<int, Player>();
    Transform parentTransform;

    public static CharacterManager Instance { get; } = new CharacterManager();

    public void Chat(S_BroadcastChat packet) {
        Object obj = Resources.Load("ChatTextPrefab");
        parentTransform = GameObject.Find("ChatContent").transform;
        GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
        go.GetComponent<TextMeshProUGUI>().text = packet.accountId + ": " + packet.message;
        if (packet.accountId == DataManager.accountId) {
            go.GetComponent<TextMeshProUGUI>().color = Color.green;
        }
    }

    public void EnterGame(S_BroadcastEnterGame packet) {
        Object obj = Resources.Load("ChatTextPrefab");
        parentTransform = GameObject.Find("ChatContent").transform;
        GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;

        if (packet.playerId == DataManager.playerId) {
            go.GetComponentInChildren<TextMeshProUGUI>().color = Color.yellow;
            go.GetComponentInChildren<TextMeshProUGUI>().text = "[시스템] 채널에 입장했습니다.";
            return;
        } else {
            go.GetComponentInChildren<TextMeshProUGUI>().color = Color.yellow;
            go.GetComponentInChildren<TextMeshProUGUI>().text = "[시스템] " + packet.accountId + "님이 입장했습니다.";
            C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
            NetworkManager.Send(requestPlayerListPacket.Write());
        }
    }

    public void LeaveGame(S_BroadcastLeaveGame packet) {
        if (SceneManager.GetActiveScene().name == "LoginScene") {
            return;
        }

        if (DataManager.playerId == packet.playerId) {
            _myPlayer = null;
        } else {
            Player player = null;
            if (_players.TryGetValue(packet.playerId, out player)) {
                _players.Remove(packet.playerId);
            }
        }
        C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
        NetworkManager.Send(requestPlayerListPacket.Write());
    }

}
