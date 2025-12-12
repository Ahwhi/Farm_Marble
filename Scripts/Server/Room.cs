using Microsoft.EntityFrameworkCore;
using Server.DB;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using static S_RoomList;
using static Server.DB.DataModel;
using static System.Collections.Specialized.BitVector32;

namespace Server {

    class Room : IJobQueue {
        List<ClientSession> _sessions = new List<ClientSession>();
        JobQueue _jobQueue = new JobQueue();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        public int roomId { get; private set; }
        public string roomTitle { get; set; }
        public string roomOwner { get; set; }
        public int playerNumInRoom {  get; private set; }
        public int maxPlayer {  get; private set; }
        public bool isGaming {  get; private set; }
        private static int _roomIdCounter { get; set; } = 0;
        public Game game { get; private set; }

        public Room() {
            roomId = _roomIdCounter++;
            roomTitle = "roomTitle : Unknown";
            roomOwner = "roomOwner : Unknown";
            playerNumInRoom = 0;
            maxPlayer = 0;
            isGaming = false;
        }

        public void Push(Action job) {
            _jobQueue.Push(job);
        }

        public void Flush() {
            foreach (ClientSession s in _sessions)
                s.Send(_pendingList);
            //Console.WriteLine($"Flushed {_pendingList.Count} items");
            _pendingList.Clear();
        }

        public void Broadcast(ArraySegment<byte> segment) {
            _pendingList.Add(segment);
        }

        public void BroadcastRoomList() {
            S_RoomList rooms = new S_RoomList();
            foreach (Room r in Program.Rooms) {
                rooms.rooms.Add(new S_RoomList.Room() {
                    roomId = r.roomId,
                    playerNumInRoom = r.playerNumInRoom,
                    roomTitle = r.roomTitle,
                    roomOwnerId = r.roomOwner,
                    maxPlayer = r.maxPlayer,
                    isGaming = r.isGaming
                });
            }
            Broadcast(rooms.Write());
        }

        public void SendPlayerList(ClientSession session) {
            S_PlayerList players = new S_PlayerList();
            foreach (ClientSession s in _sessions) {
                players.players.Add(new S_PlayerList.Player() {
                    isSelf = (s == session),
                    playerId = s.SessionId,
                    accountId = s.AccountId,
                    win = s.win,
                    lose = s.lose,
                    winP = s.winP,
                    isGameReady = s.isGameReady,
                    isCurrentTurn = s.isCurrentTurn,
                    money = s.money,
                    crops = s.crops,
                    missile = s.missile
                });
            }
            session.Send(players.Write());
        }

        public void Enter(ClientSession session) {
            // 플레이어 추가하고
            _sessions.Add(session);
            session.Room = this;
            session.Room.playerNumInRoom = session.Room._sessions.Count;
            if (this.roomId == 0) {
                Console.WriteLine($"[Enter Room] {session.AccountId} enters 로그인 룸");
            } else if (this.roomId == 1) {
                Console.WriteLine($"[Enter Room] {session.AccountId} enters 월드 룸");
            } else {
                Console.WriteLine($"[Enter Room] {session.AccountId} enters no.{this.roomId} room");
            }

            if (this.roomId == 0) { // 로그인
                return;
            }

            if (this.roomId == 1) { // 월드
                // 신입생한테 모든 룸 목록 전송
                BroadcastRoomList();
            }

            // 신입생한테 모든 플레이어 목록 전송
            SendPlayerList(session);

            // 신입생 입장을 모두에게 알린다
            S_BroadcastEnterGame enter = new S_BroadcastEnterGame();
            enter.playerId = session.SessionId;
            enter.accountId = session.AccountId;
            Broadcast(enter.Write());
        }

        public void Leave(ClientSession session) {

            if (session.Room.isGaming && !session.Room.game._isGameOver) {
                // 그 방이 게임 중이라면
                session.Room.game.setGameForceQuit(session.SessionId, session.AccountId);
            }

            // 플레이어 제거하고
            _sessions.Remove(session);
            this.playerNumInRoom = this._sessions.Count;
            if (this.roomId >= 2 && this._sessions.Count == 0) {
                Console.WriteLine($"[Delete Room] {this.roomId} is deleted");
                Program.Rooms.Remove(this);
            }

            // 월드 채널에 알린다
            //Program.Rooms[1].Push(() => Program.Rooms[1].BroadcastRoomList());
            Program.Rooms[1].BroadcastRoomList();

            // 방의 모두에게 알린다
            S_BroadcastLeaveGame leave = new S_BroadcastLeaveGame();
            leave.playerId = session.SessionId;
            Broadcast(leave.Write());
        }

        public void RequestPlayerList(ClientSession session) {
            SendPlayerList(session);
        }

        public void GameReady(ClientSession session) {
            if (!session.isGameReady) {
                session.isGameReady = true;
            } else {
                session.isGameReady = false;
            }
            int readyPlayers = 0;
            foreach (ClientSession s in _sessions) {
                if (s.isGameReady) {
                    readyPlayers++;
                }
            }
            if (readyPlayers >= this.maxPlayer && this.game == null) {
                S_GameStart start = new S_GameStart();
                this.isGaming = true;
                Broadcast(start.Write());
                // 월드 채널에 알린다
                //Program.Rooms[1].Push(() => Program.Rooms[1].BroadcastRoomList());
                Program.Rooms[1].BroadcastRoomList();

                // 새 게임을 만든다
                this.game = new Game(this, this._sessions);
                this.game.Init();

                Console.WriteLine($"{roomId}번방 GameId:{this.game.gameId} Started with {_sessions.Count} players");
            }
            S_BroadcastGameReady gameReady = new S_BroadcastGameReady();
            Broadcast(gameReady.Write());
        }

        public void RequestRoom(ClientSession session) {
            Program.Rooms[0].Push(() => Program.Rooms[0].Leave(session));
            Program.Rooms[1].Push(() => Program.Rooms[1].Enter(session));
        }

        public void GetRoomInformation(ClientSession session) {
            S_GiveRoomInformation infor = new S_GiveRoomInformation();
            infor.roomId = session.Room.roomId;
            infor.roomTitle = session.Room.roomTitle;
            infor.maxPlayer = session.Room.maxPlayer;
            session.Send(infor.Write());
        }

        public void Chat(ClientSession session, C_Chat packet) {
            if (this.roomId == 1) {
                Console.WriteLine("[Chat] 월드: " + session.AccountId + ": " + packet.message);
            } else {
                Console.WriteLine("[Chat] " + this.roomId + "번방: " + session.AccountId + ": " + packet.message);
            }

            // 모두에게 알린다
            S_BroadcastChat chat = new S_BroadcastChat();
            chat.playerId = session.SessionId;
            chat.accountId = session.AccountId;
            chat.message = packet.message;
            Broadcast(chat.Write());
        }

        public void Register(ClientSession session, C_Register packet) {
            string id = packet.accountId;
            string pw = packet.accountPw;

            using (var db = new AppDBContext()) {
                db.Database.Migrate();

                // 이미 있는지 체크
                bool exists = db.Accounts.Any(u => u.accountId == id);
                if (exists) {
                    // 실패 패킷 전송
                    S_RegisterAccept resp = new S_RegisterAccept();
                    resp.isSuccess = false;
                    resp.failReason = 1; // (int)RegisterFailReason.DuplicateId;
                    session.Send(resp.Write());  // Write() 호출
                    Console.WriteLine($"[Register Failed] already exist id: {id}");
                    return;
                }

                string hash = BCrypt.Net.BCrypt.HashPassword(pw, workFactor: 12);
                // 신규 등록
                Account account = new Account {
                    accountId = id,
                    //accountPw = pw,
                    passwordHash = hash,
                    recentIp = session.IPAddress,
                    accessToken = null,
                    tokenExpireAt = null
                };

                db.Accounts.Add(account);
                db.SaveChanges();

                // 성공 패킷 전송
                S_RegisterAccept success = new S_RegisterAccept();
                success.isSuccess = true;
                success.failReason = 0; // None
                session.Send(success.Write());  // Write() 호출

                Console.WriteLine($"[Register] New user created: Id={account.Id}, accountId={account.accountId}, recentIp={account.recentIp}");
            }
        }

        public void AutoLogin(ClientSession session, C_AutoLogin packet) {
            using (var db = new AppDBContext()) {
                var account = db.Accounts.FirstOrDefault(u => u.accessToken == packet.accessToken);

                if (account != null && account.tokenExpireAt > DateTime.UtcNow) {
                    // 자동 로그인 성공
                    session.AccountId = account.accountId;
                    session.win = account.winGame;
                    session.lose = account.loseGame;
                    session.winP = account.winPercentage;

                    S_AutoLoginAccept success = new S_AutoLoginAccept();
                    success.isSuccess = true;
                    success.failReason = 0;
                    success.accountId = account.accountId;
                    success.playerId = session.SessionId;

                    success.win = session.win;
                    success.lose = session.lose;
                    success.winP = session.winP;

                    session.Send(success.Write());

                    Console.WriteLine($"[AutoLogin Success] id: {account.accountId}, token: {packet.accessToken}");
                } else {
                    // 실패 (토큰 없음 or 만료됨)
                    S_AutoLoginAccept fail = new S_AutoLoginAccept();
                    fail.accountId = "";        // null 방지
                    if (account == null) {
                        fail.failReason = 1;
                        Console.WriteLine($"[AutoLogin Failed] invalid token: {packet.accessToken}");
                    } else if (account.tokenExpireAt <= DateTime.UtcNow) {
                        fail.failReason = 2;
                        Console.WriteLine($"[AutoLogin Failed] expired token: {packet.accessToken}");
                    }
                    fail.isSuccess = false;
                    session.Send(fail.Write());
                }
            }
        }

        public void Login(ClientSession session, C_Login packet) {
            string id = packet.accountId;
            string pw = packet.accountPw;

            using (var db = new AppDBContext()) {
                // DB에 해당 아이디가 있는지 확인
                var account = db.Accounts.FirstOrDefault(u => u.accountId == id);
                
                if (account == null) {
                    // 없는 계정
                    S_LoginAccept resp = new S_LoginAccept();
                    resp.isSuccess = false;
                    resp.failReason = 1; // (int)LoginFailReason.NotFound
                    resp.accessToken = "";
                    resp.accountId = "";
                    resp.playerId = 0;
                    session.Send(resp.Write());
                    Console.WriteLine($"[Login Failed] not found id: {id}");
                    return;
                }

                bool ok = BCrypt.Net.BCrypt.Verify(pw, account.passwordHash);
                if (!ok) {
                    // 비밀번호 불일치
                    S_LoginAccept resp = new S_LoginAccept();
                    resp.isSuccess = false;
                    resp.failReason = 2; // (int)LoginFailReason.WrongPassword
                    resp.accessToken = "";
                    resp.accountId = "";
                    resp.playerId = 0;
                    session.Send(resp.Write());
                    Console.WriteLine($"[Login Failed] wrong pw for id: {id}");
                    return;
                }

                // 로그인 성공
                string token = Guid.NewGuid().ToString();
                if (packet.isAutoLogin) {
                    account.accessToken = token;
                    account.tokenExpireAt = DateTime.UtcNow.AddHours(1); // 1시간 유효
                } else {
                    account.accessToken = null;
                    account.tokenExpireAt = null; // 1시간 유효
                }
                account.recentIp = session.IPAddress; // 최근 접속 ip 업데이트

                session.win = account.winGame;
                session.lose = account.loseGame;
                session.winP = account.winPercentage;

                session.AccountId = account.accountId;
                db.SaveChanges();

                S_LoginAccept success = new S_LoginAccept();
                success.isSuccess = true;
                success.failReason = 0; // None
                success.accountId = id;
                success.playerId = session.SessionId;
                success.accessToken = packet.isAutoLogin ? token : "";

                success.win = session.win;
                success.lose = session.lose;
                success.winP = session.winP;

                session.Send(success.Write());
                Console.WriteLine($"[Login Success] accountId: {account.accountId}, dbId: {account.Id}, ip: {session.IPAddress}, autoLogin: {packet.isAutoLogin}");
                // 리퀘스트룸
            }
        }

        public void CreateRoom(ClientSession session, C_CreateRoom packet) {
            Room newRoom = Program.CreateRoom(); // 내부에서 RoomId 자동 할당
            newRoom.roomTitle = packet.roomTitle;
            newRoom.roomOwner = session.AccountId;
            newRoom.maxPlayer = packet.maxPlayer;

            // 성공 패킷 전송
            S_CreateRoomAccept success = new S_CreateRoomAccept();
            success.roomTitle = packet.roomTitle;
            success.roomId = newRoom.roomId;
            success.playerId = session.SessionId;
            success.accountId = session.AccountId;
            session.Send(success.Write());

            // 방 생성을 모두에게 알린다
            S_BroadcastRoomCreated created = new S_BroadcastRoomCreated();
            created.roomTitle = packet.roomTitle;
            created.roomId = newRoom.roomId;
            created.playerId = session.SessionId;
            created.accountId = session.AccountId;
            Broadcast(created.Write());

            Room oldRoom = session.Room; // <- 기존 방 참조 저장
            if (oldRoom != null) {
                oldRoom.Push(() => oldRoom.Leave(session));
            }

            // 새 방에 들어가기
            newRoom.Push(() => newRoom.Enter(session));

            // 신입생한테 모든 룸 목록 전송
            BroadcastRoomList();
            Console.WriteLine($"[RoomCreate] New room created: Id={success.roomId}, accountId={success.accountId}, roomTitle={success.roomTitle}");
        }

        public void JoinRoom(ClientSession session, C_JoinRoom packet) {
            bool found = false;
            foreach (Room r in Program.Rooms) {
                if (r.roomId == packet.roomId) {
                    found = true;

                    if (r.maxPlayer <= r._sessions.Count) {
                        S_JoinRoomAccept fail = new S_JoinRoomAccept();
                        fail.isSuccess = false;
                        fail.roomId = r.roomId;
                        fail.roomTitle = r.roomTitle;
                        session.Send(fail.Write());
                        break;
                    } else {
                        // 성공 패킷 전송
                        S_JoinRoomAccept success = new S_JoinRoomAccept();
                        success.isSuccess = true;
                        success.roomId = r.roomId;
                        success.roomTitle = r.roomTitle;
                        session.Send(success.Write());

                        // 들어갈 방 인원수 바꾸고
                        r.playerNumInRoom = r._sessions.Count;
                        // 로비에 뿌려준다음
                        BroadcastRoomList();

                        // 기존 방 나간다음
                        Room oldRoom = session.Room; // <- 기존 방 참조 저장
                        if (oldRoom != null) {
                            oldRoom.Push(() => oldRoom.Leave(session));
                        }

                        // 새 방에 들어가기
                        r.Push(() => r.Enter(session));
                        Console.WriteLine($"[Join Room] {packet.accountId} joins no. {packet.roomId} room");
                        break;
                    }

                }
            }

            if (!found) {
                Console.WriteLine("Room Found Error!!!!!!");
            }
        }

        public void LeaveRoom(ClientSession session, C_LeaveRoom packet) {

            // 기존 방 나간다음
            Room oldRoom = session.Room;
            if (oldRoom != null) {
                oldRoom.Push(() => oldRoom.Leave(session));
            }

            session.isGameReady = false;

            //Program.Rooms[1].Push(() => Program.Rooms[1].Enter(session));
            //Program.Rooms[1].Enter(session);
            S_LeaveRoomAccept success = new S_LeaveRoomAccept();
            session.Send(success.Write());
            Console.WriteLine($"[Leave Room] {session.AccountId} leave {this.roomId} room");
        }

        public void Game_RollDice(ClientSession session) {
            S_BroadcastRollDice accept = new S_BroadcastRollDice();
            Random rand = new Random();
            accept.result = rand.Next(1, 7);
            Broadcast(accept.Write());
        }

        public void Game_TurnEnd(ClientSession session) {
            game.NextTurn();
            S_BroadcastTurnEnd end = new S_BroadcastTurnEnd();
            Broadcast(end.Write());
        }

        public void Game_MoveCharacter(ClientSession session, C_MoveCharacter packet) {
            S_BroadcastMoveCharacter move = new S_BroadcastMoveCharacter();
            move.playerId = session.SessionId;
            move.step = packet.step;
            Broadcast(move.Write());
        }

        public void Game_Build(ClientSession session, C_Build packet) {
            S_BroadcastBuild build = new S_BroadcastBuild();
            build.buildingType = packet.buildingType;
            build.currentField = packet.currentField;
            build.ownerPlayerId = packet.ownerPlayerId;
            build.buildingLevel = packet.buildingLevel;
            Broadcast(build.Write());
        }

        public void Game_Destroy(ClientSession session, C_Destroy packet) {
            S_BroadcastDestroy dest = new S_BroadcastDestroy();
            dest.currentField = packet.currentField;
            Broadcast(dest.Write());
        }

        public void Game_SendFirstTurn(ClientSession session) {
            this.game.BroadcastTurn();
        }

        public void Game_PlayerUpdate(ClientSession session, C_PlayerUpdate packet) {
            foreach (ClientSession s in _sessions) {
                if (s.SessionId == packet.playerId) {
                    s.money += packet.money;
                    s.crops += packet.crops;
                    s.missile += packet.missile;
                }
            }

            S_BroadcastPlayerUpdate update = new S_BroadcastPlayerUpdate();
            update.playerId = packet.playerId;
            update.money = packet.money;
            update.crops = packet.crops;
            update.missile = packet.missile;
            Broadcast(update.Write());
        }
    }
}
