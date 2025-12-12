using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BuildingData {
    public int type;
    public int level;
    public int owner;

    public BuildingData(int type, int level, int owner) {
        this.type = type;
        this.level = level;
        this.owner = owner;
    }
}

public class GameManager : MonoBehaviour {
    [Header("● UI ●")]
    public TMP_InputField InputField_Chat;
    public Button Button_RollDice;
    public Button Button_Farm;
    public Button Button_Building;
    public Button Button_Army;
    public Button Button_Missile;
    public Button Button_TurnEnd;
    public ScrollRect ScrollRect_Chat;

    [Header("● DICE ●")]
    public GameObject Prefab_Dice;
    GameObject currentDice;
    Rigidbody diceRb;

    [Header("● Sound ●")]
    private AudioSource audioSource;
    public AudioClip mouseClick;
    public AudioClip build;
    public AudioClip diceComplete;

    [Header("● DATA ●")]
    public static readonly int MaxTurn = 50;
    public static readonly int Salary = 5;
    static readonly float STEP_TIME = 0.25f;         // 한 칸 이동 시간
    static readonly float STEP_HEIGHT = 0.15f;       // 살짝 점프 느낌 (원하면 0)
    static readonly AnimationCurve EASE = AnimationCurve.EaseInOut(0, 0, 1, 1);
    static readonly HashSet<int> movingPlayers = new HashSet<int>();

    static Transform FieldRoot => GameObject.Find("Field").transform;

    public static MyPlayer _myPlayer;
    public static Dictionary<int, Player> _players = new Dictionary<int, Player>();
    public static Dictionary<int, int> playerPositions = new Dictionary<int, int>();
    public static Dictionary<int, int> playerSlotPositions = new Dictionary<int, int>();
    public static Dictionary<int, BuildingData> fieldBuildings = new Dictionary<int, BuildingData>();
    public static Dictionary<int, Material> playerMaterials = new Dictionary<int, Material>();
    public static TextMeshProUGUI turnNumText;

    public static int currentField;
    public static int currentBuildingLevel;

    public static string inputChat = "";
    public static string currentPlayerAccountId = "Unknown";
    public static int serverDiceResult = 0;
    public static int turnNumber = 0;

    public static bool isMadeCharacter = false;
    public static bool isRolledDice = false;
    public static bool isMoveCompleted = false;
    public static bool isCanMakeBuilding1 = false;
    public static bool isCanMakeBuilding2 = false;
    public static bool isCanMakeBuilding3 = false;
    public static bool isTurnChanged;

    static readonly string[] materialNames = { "Materials/Red", "Materials/Blue", "Materials/Yellow", "Materials/Purple" };
    static readonly int _ColorId = Shader.PropertyToID("_Color"); // Standard RP
    static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP
    static readonly Vector3[] SLOT_OFFSETS = new Vector3[] {
    new Vector3(-0.25f, 0.4f,  0.25f), new Vector3( 0.25f, 0.4f,  0.25f), 
    new Vector3(-0.25f, 0.4f, -0.25f), new Vector3( 0.25f, 0.4f, -0.25f)};
    public static int[] movePath = {
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31};


    private static void ResetStateOnSceneEnter() {
        isMadeCharacter = false;
        isRolledDice = false;
        isMoveCompleted = false;
        isCanMakeBuilding1 = false;
        isCanMakeBuilding2 = false;
        isCanMakeBuilding3 = false;
        isTurnChanged = false;

        serverDiceResult = 0;
        turnNumber = 0;
        currentField = 0;
        currentBuildingLevel = 0;
        currentPlayerAccountId = "Unknown";

        _myPlayer = null;
        _players.Clear();
        playerPositions.Clear();
        playerSlotPositions.Clear();
        fieldBuildings.Clear();
        playerMaterials.Clear();
    }

    public static void EndGame() {
        ResetStateOnSceneEnter();
        C_LeaveRoom leaveRoomPacket = new C_LeaveRoom();
        NetworkManager.Send(leaveRoomPacket.Write());
        SceneManager.LoadScene("LobbyScene");
    }
    
    void Start() {
        turnNumText = GameObject.Find("TurnNumText").GetComponent<TextMeshProUGUI>();
        InputField_Chat.onSubmit.AddListener(OnSubmitChat);
        C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
        NetworkManager.Send(requestPlayerListPacket.Write());

        C_RequestFirstTurn requestFirstTurn = new C_RequestFirstTurn();
        NetworkManager.Send(requestFirstTurn.Write());

        audioSource = GetComponent<AudioSource>();
        SpawnField();

        Button_Farm.GetComponentInChildren<TextMeshProUGUI>().text = $"농장 짓기";
        Button_Building.GetComponentInChildren<TextMeshProUGUI>().text = $"빌딩 짓기";
        Button_Army.GetComponentInChildren<TextMeshProUGUI>().text = $"군사 시설 짓기";
        Button_TurnEnd.GetComponentInChildren<TextMeshProUGUI>().text = $"턴 끝내기";
        Button_Missile.GetComponentInChildren<TextMeshProUGUI>().text = $"미사일 발사";
        Button_RollDice.interactable = false;
        Button_Farm.interactable = false;
        Button_Building.interactable = false;
        Button_Army.interactable = false;
        Button_TurnEnd.interactable = false;
        Button_Missile.interactable = false;
        Button_RollDice.gameObject.SetActive(false);
        Button_Farm.gameObject.SetActive(false);
        Button_Building.gameObject.SetActive(false);
        Button_Army.gameObject.SetActive(false);
        Button_TurnEnd.gameObject.SetActive(false);
        Button_Missile.gameObject.SetActive(false);
    }

    void Update() {
        ScrollRect_Chat.verticalNormalizedPosition = 0f;

        if (currentPlayerAccountId == DataManager.accountId) {
            if (isRolledDice) {
                Button_RollDice.interactable = false;
                Button_RollDice.gameObject.SetActive(false);
                if (isMoveCompleted) {
                    Button_TurnEnd.gameObject.SetActive(true);
                    Button_TurnEnd.interactable = true;
                }
            } else {
                Button_RollDice.gameObject.SetActive(true);
                Button_RollDice.interactable = true;
                Button_TurnEnd.interactable = false;
                Button_TurnEnd.gameObject.SetActive(false);
            }

            if (isCanMakeBuilding1) {
                Button_Farm.gameObject.SetActive(true);
                Button_Farm.interactable = true;
                if (currentBuildingLevel == 0) {
                    if (_myPlayer.money < 1) {
                        Button_Farm.interactable = false;
                    }
                } else if (currentBuildingLevel == 1) {
                    if (_myPlayer.money < 5) {
                        Button_Farm.interactable = false;
                    }
                } else if (currentBuildingLevel == 2) {
                    if (_myPlayer.money < 10) {
                        Button_Farm.interactable = false;
                    }
                }
            }

            if (isCanMakeBuilding2) {
                Button_Building.gameObject.SetActive(true);
                Button_Building.interactable = true;
                if (currentBuildingLevel == 0) {
                    if (_myPlayer.money < 1) {
                        Button_Building.interactable = false;
                    }
                } else if (currentBuildingLevel == 1) {
                    if (_myPlayer.money < 5) {
                        Button_Building.interactable = false;
                    }
                } else if (currentBuildingLevel == 2) {
                    if (_myPlayer.money < 10) {
                        Button_Building.interactable = false;
                    }
                }
            }

            if (isCanMakeBuilding3) {
                Button_Army.gameObject.SetActive(true);
                Button_Army.interactable = true;
                if (currentBuildingLevel == 0) {
                    if (_myPlayer.money < 1) {
                        Button_Army.interactable = false;
                    }
                } else if (currentBuildingLevel == 1) {
                    if (_myPlayer.money < 5) {
                        Button_Army.interactable = false;
                    }
                } else if (currentBuildingLevel == 2) {
                    if (_myPlayer.money < 10) {
                        Button_Army.interactable = false;
                    }
                }
            }

        } else {
            Button_RollDice.interactable = false;
            Button_Farm.interactable = false;
            Button_Building.interactable = false;
            Button_Army.interactable = false;
            Button_TurnEnd.interactable = false;
            Button_Missile.interactable = false;

            Button_RollDice.gameObject.SetActive(false);
            Button_Farm.gameObject.SetActive(false);
            Button_Building.gameObject.SetActive(false);
            Button_Army.gameObject.SetActive(false);
            Button_TurnEnd.gameObject.SetActive(false);
            Button_Missile.gameObject.SetActive(false);
        }

        if (serverDiceResult != 0 && !isRolledDice) {
            RollDice(serverDiceResult);
            isRolledDice = true;
        }
    }

    public static void OnSetPlayerList(S_PlayerList packet) {
        // 기존 플레이어 딕셔너리 초기화
        CharacterManager.Instance._players.Clear();

        if (!isMadeCharacter) {
            isMadeCharacter = true;
            SpawnCharacter(packet);
        }

        Object obj = Resources.Load("InGamePlayerPrefab");
        Transform parentTransform = GameObject.Find("Panel_PlayerList").transform;

        // 기존 프리팹 제거
        foreach (Transform child in parentTransform) {
            GameObject.Destroy(child.gameObject);
        }

        // 플레이어 목록 생성
        foreach (S_PlayerList.Player p in packet.players) {
            GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
            if (p.isSelf) {
                if (_myPlayer != null && _myPlayer.PlayerId == p.playerId) {
                    _myPlayer.money = p.money;
                    _myPlayer.crops = p.crops;
                    _myPlayer.missile = p.missile;
                }
                var status = GameObject.Find("Panel_Status")?.transform?? GameObject.Find("Canvas/Panel_Status")?.transform;
                if (status == null) return;

                var list = new List<TextMeshProUGUI>(3);
                for (int i = 0; i < status.childCount; i++) {
                    var tmp = status.GetChild(i).GetComponentInChildren<TextMeshProUGUI>(true);
                    if (tmp) list.Add(tmp);
                }
                if (list.Count >= 3) {
                    list[0].text = p.crops.ToString();
                    list[1].text = p.money.ToString();
                    list[2].text = p.missile.ToString();
                }
            }

            if (playerMaterials.ContainsKey(p.playerId)) {
                Color uiColor = playerMaterials[p.playerId].color;
                go.GetComponentInChildren<Image>().color = uiColor;
            }

            var turnMarker = go.transform.Find("TurnMarkerPanel");
            if (turnMarker) {
                turnMarker.gameObject.SetActive(p.isCurrentTurn);
            }

            var namePanel = go.transform.Find("NamePanel");
            var nameTMP = namePanel ? namePanel.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (nameTMP) {
                nameTMP.text = $"{p.accountId}";
                if (p.isSelf) nameTMP.color = Color.green;
            }

            TextMeshProUGUI riceTMP = go.transform.Find("StatusPanel/RicePanel")?.GetComponentInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI coinTMP = go.transform.Find("StatusPanel/CoinPanel")?.GetComponentInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI missileTMP = go.transform.Find("StatusPanel/MissilePanel")?.GetComponentInChildren<TextMeshProUGUI>(true);

            if (riceTMP) {
                riceTMP.text = p.crops.ToString();
                if (p.isSelf) riceTMP.color = Color.green;
            }

            if (coinTMP) {
                coinTMP.text = p.money.ToString();
                if (p.isSelf) coinTMP.color = Color.green;
            }

            if (missileTMP) {
                missileTMP.text = p.missile.ToString();
                if (p.isSelf) missileTMP.color = Color.green;
            }
            

        }
    }

    public static void SpawnCharacter(S_PlayerList _p) {
        Object obj = Resources.Load("CharacterPrefab");
        Transform parentTransform = GameObject.Find("Field").transform;
        playerSlotPositions.Clear();

        int index = 0;
        foreach (S_PlayerList.Player p in _p.players) {
            GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
            playerPositions[p.playerId] = 0;
            Renderer renderer = go.GetComponentInChildren<Renderer>();
            if (p.isSelf) {
                MyPlayer myPlayer = go.AddComponent<MyPlayer>();
                myPlayer.PlayerId = p.playerId;
                myPlayer.AccountId = p.accountId;
                myPlayer.money = p.money;
                myPlayer.crops = p.crops;
                myPlayer.missile = p.missile;
                _myPlayer = myPlayer;
                go.GetComponentInChildren<TextMeshProUGUI>().color = Color.green;
                go.GetComponentInChildren<TextMeshProUGUI>().text = $"{myPlayer.AccountId}";
                // 아웃라인

            } else {
                Player player = go.AddComponent<Player>();
                player.PlayerId = p.playerId;
                player.AccountId = p.accountId;
                player.money = p.money;
                player.crops = p.crops;
                player.missile = p.missile;
                _players.Add(p.playerId, player);
                go.GetComponentInChildren<TextMeshProUGUI>().text = $"{player.AccountId}";
            }

            string matPath = materialNames[index % materialNames.Length];
            Material mat = Resources.Load<Material>(matPath);
            if (renderer != null && mat != null) {
                renderer.material = mat;
            }

            // 플레이어 ID별 머터리얼 저장
            playerMaterials[p.playerId] = mat;

            playerSlotPositions[p.playerId] = index;
            var startField = GameObject.Find("Field_0");
            if (startField != null) {
                int slot = playerSlotPositions[p.playerId];
                go.transform.localPosition = startField.transform.localPosition + SLOT_OFFSETS[slot];
            }
            index++;
        }
    }

    public static void MoveCharacter(int playerId, int steps) {
        if (movingPlayers.Contains(playerId)) return; // 이미 이동 중
        var gm = FindObjectOfType<GameManager>();
        if (gm != null) gm.StartCoroutine(MoveAlongPath(playerId, steps));
    }

    static IEnumerator MoveAlongPath(int playerId, int steps) {
        if (!playerPositions.ContainsKey(playerId)) yield break;

        movingPlayers.Add(playerId);

        var go = GetPlayerGO(playerId);
        if (go == null) { movingPlayers.Remove(playerId); yield break; }

        // 좌표계 통일: Field 아래
        if (go.transform.parent != FieldRoot)
            go.transform.SetParent(FieldRoot, worldPositionStays: false);

        int pathLen = movePath.Length;
        int startIndex = playerPositions[playerId];
        int laps = 0;

        for (int s = 0; s < steps; s++) {
            int from = playerPositions[playerId];
            int to = (from + 1) % pathLen;
            if (to == 0) laps++; // 0을 지나치면 한 바퀴

            // 한 칸 목표 위치
            Vector3 fromPos = GetLocalPosFor(from, playerId);
            Vector3 toPos = GetLocalPosFor(to, playerId);

            float t = 0f;
            while (t < 1f) {
                t += Time.deltaTime / STEP_TIME;
                float e = EASE.Evaluate(Mathf.Clamp01(t));

                Vector3 pos = Vector3.Lerp(fromPos, toPos, e);
                pos.y += Mathf.Sin(e * Mathf.PI) * STEP_HEIGHT;

                go.transform.localPosition = pos;
                yield return null;
            }

            go.transform.localPosition = toPos;
            playerPositions[playerId] = to;
        }

        int newIndex = playerPositions[playerId];

        if (laps > 0 && currentPlayerAccountId == DataManager.accountId) {
            C_PlayerUpdate salary = new C_PlayerUpdate {
                playerId = _myPlayer.PlayerId,
                money = Salary * laps
            };
            NetworkManager.Send(salary.Write());

            Object obj = Resources.Load("ChatTextPrefab");
            Transform parent = GameObject.Find("ChatContent").transform;
            var chat = Object.Instantiate(obj, parent) as GameObject;
            chat.GetComponentInChildren<TextMeshProUGUI>().color = Color.yellow;
            chat.GetComponentInChildren<TextMeshProUGUI>().text = "[시스템] 한바퀴를 완주하여 월급을 받았습니다.";
        }

        ApplyLandingEffects(playerId, newIndex);

        movingPlayers.Remove(playerId);
    }

    static void ApplyLandingEffects(int playerId, int landedIndex) {
        currentField = playerPositions[playerId];

        GameManager gd = GameObject.FindObjectOfType<GameManager>();
        if (gd == null) { Debug.LogWarning("GameDirector을 찾을 수 없습니다."); return; }

        if (!HasBuilding(currentField)) {
            currentBuildingLevel = 0;
            isCanMakeBuilding1 = isCanMakeBuilding2 = isCanMakeBuilding3 = true;
            gd.Button_Farm.GetComponentInChildren<TextMeshProUGUI>().text = $"농장 짓기\n(1원 소모)";
            gd.Button_Building.GetComponentInChildren<TextMeshProUGUI>().text = $"빌딩 짓기\n(1원 소모)";
            gd.Button_Army.GetComponentInChildren<TextMeshProUGUI>().text = $"군사 시설 짓기\n(1원 소모)";
            gd.Button_TurnEnd.GetComponentInChildren<TextMeshProUGUI>().text = $"턴 끝내기";
        } else {
            isCanMakeBuilding1 = isCanMakeBuilding2 = isCanMakeBuilding3 = false;

            BuildingData b = fieldBuildings[landedIndex];
            currentBuildingLevel = b.level;

            if (currentPlayerAccountId == DataManager.accountId) {
                if (b.owner == _myPlayer.PlayerId) {
                    if (b.level < 3) {
                        string t = (b.level == 1) ? "5" : (b.level == 2) ? "10" : "";
                        if (b.type == 1) {
                            C_PlayerUpdate up = new C_PlayerUpdate { playerId = b.owner, crops = b.level };
                            NetworkManager.Send(up.Write());
                            isCanMakeBuilding1 = true;
                            gd.Button_Farm.GetComponentInChildren<TextMeshProUGUI>().text = $"{b.level + 1}레벨 업그레이드\n({t}원 소모)";
                        } else if (b.type == 2) {
                            isCanMakeBuilding2 = true;
                            gd.Button_Building.GetComponentInChildren<TextMeshProUGUI>().text = $"{b.level + 1}레벨 업그레이드\n({t}원 소모)";
                        } else if (b.type == 3) {
                            C_PlayerUpdate up = new C_PlayerUpdate { playerId = b.owner, missile = b.level };
                            NetworkManager.Send(up.Write());
                            isCanMakeBuilding3 = true;
                            gd.Button_Army.GetComponentInChildren<TextMeshProUGUI>().text = $"{b.level + 1}레벨 업그레이드\n({t}원 소모)";
                        }
                    }
                } else {
                    if (b.type == 2) {
                        NetworkManager.Send(new C_PlayerUpdate { playerId = b.owner, money = b.level }.Write());
                        NetworkManager.Send(new C_PlayerUpdate { playerId = _myPlayer.PlayerId, money = -b.level }.Write());
                    }
                    gd.Button_Missile.gameObject.SetActive(true);
                    gd.Button_Missile.GetComponentInChildren<TextMeshProUGUI>().text = $"건물 파괴\n(미사일 {currentBuildingLevel}개 소모)";
                    gd.Button_Missile.GetComponent<Button>().interactable = (_myPlayer.missile >= currentBuildingLevel);
                }
            }
        }

        isMoveCompleted = true;
    }

    public void OnButtonRollDice() {
        audioSource.PlayOneShot(mouseClick);
        Button_TurnEnd.interactable = false;
        Button_TurnEnd.gameObject.SetActive(false);
        serverDiceResult = 0;
        C_RollDice rollDicePacket = new C_RollDice();
        NetworkManager.Send(rollDicePacket.Write());
    }

    public void OnButton1() {
        audioSource.PlayOneShot(mouseClick);
        if (currentBuildingLevel == 0) {
            if (_myPlayer.money < 1) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -1;
            NetworkManager.Send(updatePacket.Write());
        } else if (currentBuildingLevel == 1) {
            if (_myPlayer.money < 5) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -5;
            NetworkManager.Send(updatePacket.Write());
        } else if (currentBuildingLevel == 2) {
            if (_myPlayer.money < 10) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -10;
            NetworkManager.Send(updatePacket.Write());
        }

        C_Build buildPacket = new C_Build();
        buildPacket.currentField = currentField;
        buildPacket.buildingType = 1;
        buildPacket.buildingLevel = currentBuildingLevel + 1;
        buildPacket.ownerPlayerId = _myPlayer.PlayerId;
        NetworkManager.Send(buildPacket.Write());
    }

    public void OnButton2() {
        audioSource.PlayOneShot(mouseClick);
        if (currentBuildingLevel == 0) {
            if (_myPlayer.money < 1) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -1;
            NetworkManager.Send(updatePacket.Write());
        } else if (currentBuildingLevel == 1) {
            if (_myPlayer.money < 5) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -5;
            NetworkManager.Send(updatePacket.Write());
        } else if (currentBuildingLevel == 2) {
            if (_myPlayer.money < 10) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -10;
            NetworkManager.Send(updatePacket.Write());
        }

        C_Build buildPacket = new C_Build();
        buildPacket.currentField = currentField;
        buildPacket.buildingType = 2;
        buildPacket.buildingLevel = currentBuildingLevel + 1;
        buildPacket.ownerPlayerId = _myPlayer.PlayerId;
        NetworkManager.Send(buildPacket.Write());
    }

    public void OnButton3() {
        audioSource.PlayOneShot(mouseClick);
        if (currentBuildingLevel == 0) {
            if (_myPlayer.money < 1) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -1;
            NetworkManager.Send(updatePacket.Write());
        } else if (currentBuildingLevel == 1) {
            if (_myPlayer.money < 5) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -5;
            NetworkManager.Send(updatePacket.Write());
        } else if (currentBuildingLevel == 2) {
            if (_myPlayer.money < 10) {
                return;
            }
            C_PlayerUpdate updatePacket = new C_PlayerUpdate();
            updatePacket.playerId = _myPlayer.PlayerId;
            updatePacket.money = -10;
            NetworkManager.Send(updatePacket.Write());
        }

        C_Build buildPacket = new C_Build();
        buildPacket.currentField = currentField;
        buildPacket.buildingType = 3;
        buildPacket.buildingLevel = currentBuildingLevel + 1;
        buildPacket.ownerPlayerId = _myPlayer.PlayerId;
        NetworkManager.Send(buildPacket.Write());
    }

    public void OnButton4() {
        audioSource.PlayOneShot(mouseClick);
        C_TurnEnd turnEndPacket = new C_TurnEnd();
        NetworkManager.Send(turnEndPacket.Write());
    }

    public void OnButtonMissile() {
        audioSource.PlayOneShot(mouseClick);
        C_PlayerUpdate updatePacket = new C_PlayerUpdate();
        updatePacket.playerId = _myPlayer.PlayerId;
        updatePacket.missile = -currentBuildingLevel;
        NetworkManager.Send(updatePacket.Write());

        C_Destroy destPacket = new C_Destroy();
        destPacket.currentField = currentField;
        NetworkManager.Send(destPacket.Write());
    }

    public static void RemoveBuild(int fieldIndex, bool resetMaterial = true, bool notifyTurnEnd = true) {
        if (fieldBuildings.ContainsKey(fieldIndex))
            fieldBuildings.Remove(fieldIndex);

        string fieldName = "Field_" + fieldIndex;
        GameObject field = GameObject.Find(fieldName);

        if (field != null) {
            Transform old = field.transform.Find("Building_" + fieldIndex);
            if (old != null)
                Object.Destroy(old.gameObject);

            if (resetMaterial) {
                Material originalMat = Resources.Load<Material>("Materials/Original");
                if (originalMat != null) {
                    ApplyFieldMaterial(fieldIndex, matOverride: originalMat, originalMat.color);
                } else {
                    Debug.LogWarning("[RemoveBuild] 'Materials/Original.mat' 머터리얼을 찾을 수 없습니다.");
                }
            }
        }

        if (notifyTurnEnd && DataManager.accountId == currentPlayerAccountId) {
            GameManager gd = GameObject.FindObjectOfType<GameManager>();
            if (gd != null)
                gd.OnButton4();
        }
    }



    public static void Build(int fieldIndex, int buildingType = 1, int buildingLevel = 1, int ownerPlayerId = -1) {
        if (!fieldBuildings.ContainsKey(fieldIndex)) {
            fieldBuildings[fieldIndex] = new BuildingData(buildingType, buildingLevel, ownerPlayerId);
        } else {
            // 이미 건물이 있으면 레벨 업 (최대 3까지)
            BuildingData b = fieldBuildings[fieldIndex];
            if (b.owner == ownerPlayerId && b.level < 3) {
                b.level++;
            }
        }


        //  대신 여기서 필드 머터리얼만 바꿈
        var data = fieldBuildings[fieldIndex];

        //  타입/레벨별 고정 머터리얼을 쓰고 싶으면:
        Material matByTypeLevel = GetFieldMatByTypeLevel(data.type, data.level);
        if (matByTypeLevel != null) {
            ApplyFieldMaterial(fieldIndex, matOverride: matByTypeLevel);
        }

        //  + 소유자 색으로 살짝 틴트(둘 다 하고 싶을 때):
        if (ownerPlayerId != -1) {
            var tint = GetOwnerTint(ownerPlayerId);
            ApplyFieldMaterial(fieldIndex, tint: tint);
        }

        // 필드 오브젝트 찾기
        string fieldName = "Field_" + fieldIndex;
        GameObject field = GameObject.Find(fieldName);

        if (field != null) {
            // 기존 건물 삭제
            Transform old = field.transform.Find("Building_" + fieldIndex);
            if (old != null) {
                Object.Destroy(old.gameObject);
            }

            GameObject buildingPrefab = null;

            if (buildingType == 1) {
                buildingPrefab = Resources.Load<GameObject>("FarmPrefab");
            } else if (buildingType == 2) {
                buildingPrefab = Resources.Load<GameObject>("BuildingPrefab");
            } else if (buildingType == 3) {
                buildingPrefab = Resources.Load<GameObject>("ArmyPrefab");
            }

            if (buildingPrefab != null) {
                GameObject building = Object.Instantiate(buildingPrefab, field.transform);
                building.transform.localPosition = new Vector3(0, 0.1f, 0);
                building.name = "Building_" + fieldIndex;

                // 레벨 시각화 (예: 크기 변경)
                building.GetComponentInChildren<Image>().color = playerMaterials[ownerPlayerId].color;
                building.GetComponentInChildren<TextMeshProUGUI>().text = new string('★', buildingLevel);
                //building.GetComponentInChildren<TextMeshProUGUI>().text = $"{buildingLevel}";

                // 파티클 효과
                GameObject p = Resources.Load<GameObject>("ParticlePrefab");
                Instantiate(p, field.transform);
            }
        }

        // 인스턴스를 찾아서 OnButton4 호출
        if (DataManager.accountId == currentPlayerAccountId) {
            GameManager gd = GameObject.FindObjectOfType<GameManager>();
            gd.audioSource.PlayOneShot(gd.build);
            if (gd != null) {
                gd.OnButton4();
            }
        }

        

        //Debug.Log($"{fieldIndex}번 땅에 {ownerPlayerId} 플레이어가 건물 타입 {buildingType}, 레벨 {fieldBuildings[fieldIndex].level} 지음");
    }


    public void SpawnField() {
        Object obj = Resources.Load("FieldPrefab");
        Transform parentTransform = GameObject.Find("Field").transform;

        int size = 9; // 한 변당 칸 수
        float gap = 1.5f; // 칸 사이 거리
        int total = (size * 4) - 4; // 총 칸 개수 = 28

        int index = 0;
        for (int side = 0; side < 4; side++) {
            for (int i = 0; i < size; i++) {
                // 꼭짓점 중복 제거
                if ((i == size - 1) && (side != 3)) // 기존 로직
                    continue;

                // 마지막 칸(Field_28) 중복 제거
                if (side == 3 && i == size - 1)
                    continue;

                Vector3 pos = Vector3.zero;
                switch (side) {
                    case 0: // 왼쪽 하단 → 오른쪽 하단
                        pos = new Vector3(i * gap, 0, 0);
                        break;
                    case 1: // 오른쪽 하단 → 오른쪽 상단
                        pos = new Vector3((size - 1) * gap, 0, i * gap);
                        break;
                    case 2: // 오른쪽 상단 → 왼쪽 상단
                        pos = new Vector3((size - 1 - i) * gap, 0, (size - 1) * gap);
                        break;
                    case 3: // 왼쪽 상단 → 왼쪽 하단
                        pos = new Vector3(0, 0, (size - 1 - i) * gap);
                        break;
                }

                GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
                go.transform.localPosition = pos;
                go.name = $"Field_{index}";
                index++;
            }
        }

        // 모든 필드 건물 상태 초기화
        fieldBuildings.Clear();

    }

    #region 코어
    public static bool HasBuilding(int fieldIndex) {
        return fieldBuildings.ContainsKey(fieldIndex) && fieldBuildings[fieldIndex] != null;
    }

    private void OnSubmitChat(string text) {
        if (!string.IsNullOrEmpty(text)) {
            inputChat = text;
            OnButtonSend();

            // 다시 입력할 수 있도록 포커스 유지
            InputField_Chat.ActivateInputField();
        }
    }

    public void OnButtonSend() {
        audioSource.PlayOneShot(mouseClick);
        C_Chat chatPacket = new C_Chat();
        chatPacket.message = inputChat;
        NetworkManager.Send(chatPacket.Write());

        InputField_Chat.text = null;
        inputChat = null;
    }

    public void RollDice(int serverResult) {
        if (currentDice != null)
            Destroy(currentDice);

        currentDice = Instantiate(Prefab_Dice, new Vector3(6, 4, 6), Random.rotation);
        diceRb = currentDice.GetComponent<Rigidbody>();

        // 초기 힘과 회전
        diceRb.AddForce(new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), Random.Range(-2f, 2f)), ForceMode.Impulse);
        diceRb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);

        StartCoroutine(RollAnimation(serverResult));
    }

    IEnumerator RollAnimation(int result) {
        float rollDuration = 0.6f; // 굴림 시간
        float elapsed = 0f;

        // 주사위 자연스럽게 굴리기
        while (elapsed < rollDuration) {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 마지막 0.3초 동안 스냅 처리
        float snapTime = 0.2f;
        float snapElapsed = 0f;
        Quaternion startRot = currentDice.transform.rotation;
        Quaternion targetRot = GetRotationForResult(result);

        // Rigidbody 제어 잠시 정지
        diceRb.isKinematic = true;

        while (snapElapsed < snapTime) {
            snapElapsed += Time.deltaTime;
            float t = snapElapsed / snapTime;
            currentDice.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // 최종 정확한 회전
        currentDice.transform.rotation = targetRot;
        diceRb.isKinematic = false;
        audioSource.PlayOneShot(diceComplete);

        // 캐릭터 움직여라 패킷 전송
        if (DataManager.accountId == currentPlayerAccountId) {
            C_MoveCharacter movePacket = new C_MoveCharacter();
            movePacket.step = serverDiceResult;
            NetworkManager.Send(movePacket.Write());
        }

        Object objs = Resources.Load("DiceNumTextPrefab");
        Transform parentTransform = GameObject.Find("Panel_Effect").transform;
        GameObject go = Object.Instantiate(objs, parentTransform) as GameObject;
        go.GetComponentInChildren<TextMeshProUGUI>().text = result.ToString();
    }

    Quaternion GetRotationForResult(int result) {
        switch (result) {
            case 1: return Quaternion.Euler(0, 0, 0);
            case 2: return Quaternion.Euler(0, 0, -90);
            case 3: return Quaternion.Euler(90, 0, 0);
            case 4: return Quaternion.Euler(-90, 0, 0);
            case 5: return Quaternion.Euler(0, 0, 90);
            case 6: return Quaternion.Euler(180, 0, 0);
            default: return Quaternion.identity;
        }
    }

    static void ApplyFieldMaterial(int fieldIndex, Material matOverride = null, Color? tint = null) {
        string fieldName = "Field_" + fieldIndex;
        GameObject field = GameObject.Find(fieldName);
        if (!field) return;

        // 필드가 여러 Mesh/Renderer로 구성될 수 있으니 전부 잡기
        var renderers = field.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers == null || renderers.Length == 0) return;

        if (matOverride != null) {
            foreach (var r in renderers) {
                r.sharedMaterial = matOverride;
            }
        }

        // 색상만 살짝 틴팅하고 싶을 때(머터리얼 인스턴스 생성 없이!)
        if (tint.HasValue) {
            foreach (var r in renderers) {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);

                // 파이프라인에 따라 컬러 프로퍼티가 다를 수 있어 둘 다 시도
                if (r.sharedMaterial && r.sharedMaterial.HasProperty(_BaseColorId)) {
                    mpb.SetColor(_BaseColorId, tint.Value);
                } else if (r.sharedMaterial && r.sharedMaterial.HasProperty(_ColorId)) {
                    mpb.SetColor(_ColorId, tint.Value);
                }
                r.SetPropertyBlock(mpb);
            }
        }
    }

    static Color GetOwnerTint(int ownerPlayerId) {
        if (playerMaterials.TryGetValue(ownerPlayerId, out var mat) && mat != null) {
            if (mat.HasProperty(_BaseColorId)) return mat.GetColor(_BaseColorId);
            if (mat.HasProperty(_ColorId)) return mat.GetColor(_ColorId);
        }
        return Color.white;
    }

    static Material GetFieldMatByTypeLevel(int buildingType, int buildingLevel) {
        string path = $"FieldMats/Type{buildingType}_L{Mathf.Clamp(buildingLevel, 1, 3)}";
        return Resources.Load<Material>(path);
    }


    static Transform GetField(int fieldIndex) {
        return FieldRoot.Find($"Field_{fieldIndex}");
    }

    static GameObject GetPlayerGO(int playerId) {
        if (_myPlayer != null && _myPlayer.PlayerId == playerId) return _myPlayer.gameObject;
        if (_players.TryGetValue(playerId, out var p)) return p.gameObject;
        return null;
    }

    static Vector3 GetLocalPosFor(int fieldIndex, int playerId) {
        var field = GetField(movePath[fieldIndex]);
        if (!playerSlotPositions.TryGetValue(playerId, out int slot)) slot = 0;
        return field.localPosition + SLOT_OFFSETS[slot % SLOT_OFFSETS.Length];
    }
    #endregion
}
