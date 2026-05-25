using MiniOrm.Attributes;

namespace MiniOrm.Models;

[Table("products")]
public class Product
{
    [PrimaryKey]
    [Column("id")]       public int      Id       { get; set; }
    [Column("name")]     public string   Name     { get; set; } = "";
    [Column("price")]    public decimal  Price    { get; set; }
    [Column("discount")] public decimal? Discount { get; set; }
    [Column("in_stock")] public bool     InStock  { get; set; }
}