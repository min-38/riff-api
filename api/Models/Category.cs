using System;
using System.Collections.Generic;

namespace api.Models;

// 카테고리
public partial class Category
{
    // PK
    public int Id { get; set; }

    // 카테고리명
    public string Name { get; set; } = null!;

    // URL slug
    public string Slug { get; set; } = null!;

    // 생성 시간
    public DateTime CreatedAt { get; set; }

    // 수정 시간
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Gear> Gears { get; set; } = new List<Gear>();
}
