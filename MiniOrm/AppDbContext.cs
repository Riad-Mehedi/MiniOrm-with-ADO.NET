using MiniOrm.Data;
using MiniOrm.Models;

namespace MiniOrm;

/// <summary>
/// Application-specific database context.
/// Declares two DbSet properties — Products and Orders.
/// The base DbContext constructor finds them via reflection and
/// auto-initialises each one, so no manual assignment is needed.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Order>   Orders   { get; set; } = null!;

    public AppDbContext(string connStr) : base(connStr) { }
}