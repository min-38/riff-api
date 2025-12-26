namespace api.Templates.Email;

// 이메일 템플릿 인터페이스
public interface IEmailTemplate
{
    // 이메일 제목
    string Subject { get; }

    // HTML 본문 생성
    string GenerateHtml();

    // Plain text 본문 생성 (스팸 필터 통과 및 HTML 미지원 클라이언트용)
    string GeneratePlainText();
}
