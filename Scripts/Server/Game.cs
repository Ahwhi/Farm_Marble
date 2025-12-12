using Server.DB;
using ServerCore;
using System.Net.Sockets;
using static S_RoomList;

namespace Server {

    class Game {
        public int gameId { get; private set; }
        private Room _room;
        private int _currentTurnIndex = 0;
        private int maxTurn = 50;
        private List<ClientSession> _players;
        public bool _isGameOver = false;
        private static int _gameIdCounter { get; set; } = 0;

        public int TurnNumber { get; private set; } = 1; // 몇 번째 턴인지
        public ClientSession CurrentPlayer => _players[_currentTurnIndex]; // 현재 턴 주인

        public Game(Room room, List<ClientSession> players) {
            gameId = _gameIdCounter++;
            _room = room;
            _players = new List<ClientSession>(players);
        }

        public void Init() {
            TurnNumber = 1;
            _currentTurnIndex = 0;
            _isGameOver = false;
            foreach (ClientSession s in _players) {
                s.money = 5;
                s.crops = 0;
                s.missile = 0;
            }
            BroadcastTurn();
        }

        public void NextTurn() {
            if (_isGameOver) return;
            CurrentPlayer.isCurrentTurn = false;
            _currentTurnIndex = (_currentTurnIndex + 1) % _players.Count;
            if (_currentTurnIndex == 0)
                TurnNumber++;

            if (TurnNumber > maxTurn) {
                setGameOver();
            } else {
                BroadcastTurn();
            }  
        }

        public void setGameForceQuit(int playerId, string accountId) {
            _isGameOver = true;

            // 1) 우선 퇴장 브로드캐스트
            S_BroadcastQuitPlayer quit = new S_BroadcastQuitPlayer {
                playerId = playerId,
                accountId = accountId
            };
            _room.Broadcast(quit.Write());

            try {
                // 2) 강종자의 accountId 보정
                if (string.IsNullOrWhiteSpace(accountId)) {
                    var target = _players.FirstOrDefault(p => p.SessionId == playerId);
                    if (target != null) accountId = target.AccountId;
                }

                if (!string.IsNullOrWhiteSpace(accountId)) {
                    using (var db = new AppDBContext()) {
                        var acc = db.Accounts.FirstOrDefault(a => a.accountId == accountId);
                        if (acc != null) {
                            // 3) 강종자만 패배 처리
                            acc.loseGame += 1;

                            int total = acc.winGame + acc.loseGame;
                            acc.winPercentage = (total > 0)
                                ? (float)Math.Round(((float)acc.winGame / total) * 100f, 2)
                                : 0f;

                            db.SaveChanges();
                            Console.WriteLine($"[ForceQuit] Account {accountId} quits gameId: {gameId}");
                        } else {
                            Console.WriteLine($"[ForceQuit] Account not found in DB: {accountId}");
                        }
                    }
                } else {
                    Console.WriteLine($"[ForceQuit] accountId is null/empty for playerId={playerId}");
                }
            } catch (Exception ex) {
                Console.WriteLine("[ForceQuit][DB Error] " + ex);
                // DB 실패해도 아래 정리는 진행
            }

            // 5) 게임 종료 후 기본 플래그 리셋
            foreach (ClientSession s in _players) {
                s.isGameReady = false;
                s.isCurrentTurn = false;
                // 돈/자원은 건드리지 않음
            }
        }

        public void setGameOver() {
            _isGameOver = true;
            ClientSession richest = _players.OrderByDescending(s => s.crops).ThenByDescending(s => s.money).First();
            Console.WriteLine($"[GameEnd]: Winner: {richest.AccountId}, crops: {richest.crops}, money: {richest.money}");

            S_GameResult result = new S_GameResult();
            result.playerId = richest.SessionId;
            result.accountId = richest.AccountId;
            _room.Broadcast(result.Write());

            try {
                using (var db = new AppDBContext()) {
                    // 이번 판에 참여한 모든 accountId
                    var participantIds = _players
                        .Select(p => p.AccountId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct()
                        .ToList();

                    // 해당 계정 로드
                    var accounts = db.Accounts
                        .Where(a => participantIds.Contains(a.accountId))
                        .ToList();

                    // 혹시 DB에 없는 계정이 있으면 로그만 남김
                    var missing = participantIds.Except(accounts.Select(a => a.accountId)).ToList();
                    if (missing.Count > 0)
                        Console.WriteLine("[GameOver] Not found in DB: " + string.Join(", ", missing));

                    foreach (var acc in accounts) {
                        if (acc.accountId == richest.AccountId)
                            acc.winGame += 1;
                        else
                            acc.loseGame += 1;

                        int total = acc.winGame + acc.loseGame;
                        if (total > 0) {
                            float percentage = (float)acc.winGame / total * 100f;
                            acc.winPercentage = (float)Math.Round(percentage, 2); // 소수점 둘째 자리까지
                        } else {
                            acc.winPercentage = 0f;
                        }
                    }

                    var byId = accounts.ToDictionary(a => a.accountId, a => a);
                    foreach (var s in _players) {
                        if (!string.IsNullOrEmpty(s.AccountId) &&
                            byId.TryGetValue(s.AccountId, out var acc)) {
                            // 세션에 최신값 반영
                            s.win = acc.winGame;
                            s.lose = acc.loseGame;
                            s.winP = acc.winPercentage;
                        }
                    }

                    db.SaveChanges();
                }
            } catch (Exception ex) {
                Console.WriteLine("[GameOver][DB Error] " + ex);
                // 실패하더라도 아래 초기화는 진행
            }

            foreach (ClientSession s in _players) {
                s.isGameReady = false;
                s.isCurrentTurn = false;
                //s.money = 5;
                //s.crops = 0;
                //s.missile = 0;
            }
        }

        public void BroadcastTurn() {
            S_BroadcastTurn turn = new S_BroadcastTurn();
            turn.turnNumber = TurnNumber;
            turn.currentPlayerId = CurrentPlayer.SessionId;
            CurrentPlayer.isCurrentTurn = true;
            turn.currentAccountId = CurrentPlayer.AccountId;
            _room.Broadcast(turn.Write());
        }
       
    }
}