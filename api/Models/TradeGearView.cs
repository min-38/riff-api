using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace api.Models;

[Table("trade_gear_views")]
public partial class TradeGearView
{
    [Column("id")]
    public long Id { get; set; }

    [Column("gear_id")]
    public long GearId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("ip_address")]
    public IPAddress? IpAddress { get; set; }

    [Column("viewed_at")]
    public DateTime ViewedAt { get; set; }

    // Navigation properties
    [ForeignKey("GearId")]
    public virtual TradeGear TradeGear { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
