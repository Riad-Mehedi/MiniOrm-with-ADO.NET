using MiniOrm;
using MiniOrm.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Step 1: Read connection string from environment variable.
// Run in Git Bash before dotnet run:
//   export MINIORM_CONN="Host=localhost;Port=5432;Database=miniorm_db;Username=postgres;Password=yourpassword"
// ─────────────────────────────────────────────────────────────────────────────
var connStr = Environment.GetEnvironmentVariable("MINIORM_CONN")
    ?? throw new Exception(
        "MINIORM_CONN is not set.\n" +
        "Run: export MINIORM_CONN=\"Host=localhost;Port=5432;" +
        "Database=miniorm_db;Username=postgres;Password=yourpassword\"");

Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║     MiniOrm — Demo Walkthrough       ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.WriteLine();

// Step 2: Create the DbContext.
// Base constructor calls InitDbSets() via reflection — finds Products and
// Orders properties and assigns live DbSet<T> instances automatically.
using var db = new AppDbContext(connStr);
Console.WriteLine("✓ Step 1 — AppDbContext created\n");

// ── INSERT ────────────────────────────────────────────────────────────────────
Console.WriteLine("── Step 2: INSERT two products ─────────────────────────");

var keyboard = new Product { Name = "Keyboard", Price = 89.99m, Discount = null, InStock = true };
int id1 = db.Products.Insert(keyboard);
Console.WriteLine($"  Inserted  Id={id1}  Name=Keyboard  Discount=NULL ✓");

var mouse = new Product { Name = "Mouse", Price = 39.99m, Discount = 5.00m, InStock = true };
int id2 = db.Products.Insert(mouse);
Console.WriteLine($"  Inserted  Id={id2}  Name=Mouse     Discount=5.00 ✓");
Console.WriteLine();

// ── FIND BY ID ────────────────────────────────────────────────────────────────
Console.WriteLine("── Step 3: FIND BY ID ──────────────────────────────────");
var found = db.Products.FindById(id1);
Console.WriteLine(
    $"  FindById({id1}) → Name={found?.Name}, Price={found?.Price}, " +
    $"Discount={found?.Discount?.ToString() ?? "NULL"} ✓");
Console.WriteLine();

// ── GET ALL ───────────────────────────────────────────────────────────────────
Console.WriteLine("── Step 4: GET ALL ─────────────────────────────────────");
var all = db.Products.GetAll().ToList();
Console.WriteLine($"  Total: {all.Count} product(s)");
foreach (var p in all)
    Console.WriteLine($"    Id={p.Id,-3} {p.Name,-12} Price={p.Price,-8} Discount={p.Discount?.ToString() ?? "NULL"}");
Console.WriteLine();

// ── UPDATE ────────────────────────────────────────────────────────────────────
Console.WriteLine("── Step 5: UPDATE ──────────────────────────────────────");
found!.Price   = 79.99m;
found.Discount = 5.00m;
db.Products.Update(found);
var updated = db.Products.FindById(id1);
Console.WriteLine($"  Updated Id={id1} → Price={updated?.Price}, Discount={updated?.Discount} ✓");
Console.WriteLine();

// ── DELETE ────────────────────────────────────────────────────────────────────
Console.WriteLine("── Step 6: DELETE ──────────────────────────────────────");
db.Products.Delete(id1);
db.Products.Delete(id2);
var remaining = db.Products.GetAll().Count();
Console.WriteLine($"  Deleted Id={id1} and Id={id2} ✓ — {remaining} product(s) remaining");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║         All steps complete ✓         ║");
Console.WriteLine("╚══════════════════════════════════════╝");