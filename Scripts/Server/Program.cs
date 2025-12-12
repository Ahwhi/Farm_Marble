using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using ServerCore;
using static Server.DB.DataModel;

namespace Server {
    class Program {
        static Listener _listener = new Listener();
        public static List<Room> Rooms = new List<Room>();

        public static Room CreateRoom() {
            Room room = new Room();
            Rooms.Add(room);
            return room;
        }

        public static void RemoveRoom(Room room) {
            Rooms.Remove(room);
        }

        static void FlushRoom() {
            foreach (Room room in Rooms) {
                room.Push(() => room.Flush());
                //room.test_sessionprint();
            }
            JobTimer.Instance.Push(FlushRoom, 250);
        }

        static void Main(string[] args) {

            using (var db = new AppDBContext()) {
                db.Database.Migrate();
            }

            string ip = "211.216.236.101";
            IPAddress ipAddr = IPAddress.Parse(ip);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate(); });
            Console.WriteLine("Listening...");

            CreateRoom(); //로그인룸 roomid:0
            Rooms[0].roomTitle = "LoginRoom";
            Rooms[0].roomOwner = "Unset";
            CreateRoom(); //월드룸 roomid:1
            Rooms[1].roomTitle = "WorldRoom";
            Rooms[1].roomOwner = "Unset";
            JobTimer.Instance.Push(FlushRoom);

            while (true) {
                JobTimer.Instance.Flush();
            }
        }
    }
}
