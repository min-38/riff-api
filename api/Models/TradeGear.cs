using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using api.Models.Enums;

namespace api.Models;

public partial class TradeGear
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int Price { get; set; }

    public GearCategory Category { get; set; }

    public GearSubCategory SubCategory { get; set; }

    public GearDetailCategory DetailCategory { get; set; }

    public GearCondition? Condition { get; set; }

    public TradeMethod TradeMethod { get; set; }

    public Region Region { get; set; }

    public GearStatus Status { get; set; }

    public ImageData? Images { get; set; }

    public int ViewCount { get; set; }

    public int LikeCount { get; set; }

    public int ChatCount { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual User Author { get; set; } = null!;
}
