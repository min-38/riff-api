namespace api.Templates.Email;

// 이메일 템플릿 인터페이스
public interface IEmailTemplate
{
    // 이메일 제목
    string Subject { get; }

    // HTML 본문 생성
    string GenerateHtml();
}
