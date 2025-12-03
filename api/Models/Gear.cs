using System;
using System.Collections.Generic;

namespace api.Models;

// 판매 게시글
public partial class Gear
{
    // PK
    public long Id { get; set; }

    // 제목
    public string Title { get; set; } = null!;

    // 간단 설명
    public string Description { get; set; } = null!;


    // 가격

    public int Price { get; set; }


    // 모델명

    public string? Model { get; set; }


    // 제조년도

    public int? Year { get; set; }


    // 거래 지역

    public string Location { get; set; } = null!;


    // 이미지 URL 배열

    public List<string>? Images { get; set; }


    // 조회수

    public int Views { get; set; }


    // 기어 상태 (새 제품, 거의 새 것, 양호, 보통, 나쁨)

    public GearCondition Condition { get; set; }


    // 판매 상태 (판매 가능, 예약됨, 판매 완료, 숨김)

    public GearStatus Status { get; set; }


    // 거래 선호 방식 (직거래, 택배, 둘 다)

    public TransactionPreference TransactionPreference { get; set; }


    // 카테고리 ID

    public int CategoryId { get; set; }


    // 판매자 ID

    public Guid SellerId { get; set; }


    // 생성 시간

    public DateTime CreatedAt { get; set; }


    // 수정 시간

    public DateTime UpdatedAt { get; set; }


    // 삭제 시간

    public DateTime? DeletedAt { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual User Seller { get; set; } = null!;
}
