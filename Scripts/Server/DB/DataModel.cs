using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.DB
{
    public class DataModel
    {
        public class Account {
            [Key]
            public int Id { get; set; }

            [Required]
            public string accountId { get; set; }
            public string passwordHash { get; set; }
            public string recentIp { get; set; }
            public string? accessToken { get; set; }
            public DateTime? tokenExpireAt { get; set; }
            public int winGame {  get; set; }
            public int loseGame { get; set; }
            public float winPercentage { get; set; }
        }
    }
}
