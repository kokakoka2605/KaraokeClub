using KaraokeClub.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;

        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<MenuType> MenuTypes { get; set; } = null!;
        public DbSet<KaraokeOption> KaraokeOptions { get; set; } = null!;
        public DbSet<Worker> Workers { get; set; } = null!;
        public DbSet<MenuItem> MenuItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Bill> Bills { get; set; } = null!;

        public DbSet<AppUser> AppUsers { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlServer(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>()
                .ToTable(tb => tb.UseSqlOutputClause(false));
            modelBuilder.Entity<OrderItem>()
                .Property(e => e.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Order>()
                .ToTable(tb => tb.UseSqlOutputClause(false));
            modelBuilder.Entity<Order>()
                .Property(e => e.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Bill>()
                .ToTable(tb => tb.UseSqlOutputClause(false));
            modelBuilder.Entity<Bill>()
                .Property(e => e.Id)
                .ValueGeneratedOnAdd();
        }

        // ?? вспомогательный метод: вызвать хранимую процедуру ??????????????
        public void ExecProc(string procName, params SqlParameter[] parameters)
        {
            if (parameters.Length == 0)
            {
                Database.ExecuteSqlRaw($"EXEC {procName}");
                return;
            }

            var paramNames = string.Join(", ", parameters.Select(p => p.ParameterName));
            Database.ExecuteSqlRaw(
                $"EXEC {procName} {paramNames}",
                parameters.Cast<object>().ToArray()
            );
        }

        // ─── Резервная копия ─────────────────────────────────────
        public void BackupDatabase(string backupPath)
        {
            Database.ExecuteSqlRaw("EXEC usp_Backup_Database @p0", backupPath);
        }

        // ─── Восстановление ──────────────────────────────────────
        public void RestoreDatabase(string backupPath)
        {
            // Закрываем пул соединений нашего приложения к KaraokeClub
            Database.CloseConnection();
            SqlConnection.ClearAllPools();

            // Теперь выполняем через отдельное соединение к master
            var masterCs = _connectionString
                .Replace("Database=KaraokeClub", "Database=master")
                .Replace("Initial Catalog=KaraokeClub", "Initial Catalog=master");

            using var conn = new SqlConnection(masterCs);
            conn.Open();
            using var cmd = new SqlCommand("usp_Restore_Database", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure,
                CommandTimeout = 120
            };
            cmd.Parameters.AddWithValue("@BackupPath", backupPath);
            cmd.ExecuteNonQuery();
        }
    }
}