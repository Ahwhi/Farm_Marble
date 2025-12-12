using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using static Server.DB.DataModel;

namespace Server.DB
{
    public class AppDBContext : DbContext
    {
        public DbSet<Account> Accounts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            var baseDir = AppContext.BaseDirectory;
            var dbPath = Path.Combine(baseDir, "Data", "GameDB.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.Entity<Account>().ToTable("Accounts").HasIndex(u => u.accountId).IsUnique();
        }
    }
}
