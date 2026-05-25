using MiniOrm.Migrations.Commands;

// Read connection string from environment variable — never hardcode credentials.
// Set it in Git Bash before running:
//   export MINIORM_CONN="Host=localhost;Port=5432;Database=miniorm_db;Username=postgres;Password=yourpassword"
var connStr = Environment.GetEnvironmentVariable("MINIORM_CONN")
    ?? throw new Exception(
        "MINIORM_CONN environment variable is not set.\n" +
        "Run: export MINIORM_CONN=\"Host=localhost;Port=5432;" +
        "Database=miniorm_db;Username=postgres;Password=yourpassword\"");

if (args.Length < 2 || args[0] != "migrations")
{
    Console.WriteLine("MiniOrm Migration CLI");
    Console.WriteLine("Usage: dotnet run -- migrations <command> [name]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  add <Name>   Generate a new timestamped migration file");
    Console.WriteLine("  apply        Apply all pending migrations to the database");
    Console.WriteLine("  list         Show applied / pending status of all migrations");
    Console.WriteLine("  rollback     Revert the last applied migration");
    return;
}

var runner = new MigrationRunner(connStr, "Migrations");

switch (args[1])
{
    case "add":
        if (args.Length < 3) { Console.WriteLine("Provide a migration name. E.g: dotnet run -- migrations add InitialCreate"); return; }
        runner.Add(args[2]);
        break;
    case "apply":    runner.Apply();    break;
    case "list":     runner.List();     break;
    case "rollback": runner.Rollback(); break;
    default:
        Console.WriteLine($"Unknown command '{args[1]}'. Use: add | apply | list | rollback");
        break;
}