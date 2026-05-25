using MiniOrm.Attributes;

namespace MiniOrm.Models;

[Table("orders")]
public class Order
{
    [PrimaryKey]
    [Column("id")]          public int      Id        { get; set; }
    [Column("product_id")]  public int      ProductId { get; set; }
    [Column("quantity")]    public int      Quantity  { get; set; }
    [Column("ordered_at")]  public DateTime OrderedAt { get; set; }
    [Column("notes")]       public string?  Notes     { get; set; }
}