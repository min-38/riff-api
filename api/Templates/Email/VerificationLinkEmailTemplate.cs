namespace api.Templates.Email;

// 회원가입 이메일 인증 템플릿 (URL 링크 기반)
public class VerificationLinkEmailTemplate : IEmailTemplate
{
    private readonly string _verificationUrl;

    public string Subject => "이메일 인증 - Riff";

    public VerificationLinkEmailTemplate(string verificationUrl)
    {
        _verificationUrl = verificationUrl;
    }

    public string GeneratePlainText()
    {
        return $@"Riff 이메일 인증

환영합니다!

안녕하세요 Riff 입니다.
Riff에 가입해 주셔서 감사합니다.

아래 링크를 클릭하여 이메일 인증을 완료해주세요:
{_verificationUrl}

주의사항:
• 이 인증 링크는 24시간 동안만 유효합니다.
• 메일이 도착하지 않았다면 스팸함을 확인해주세요.
• 본인이 가입하지 않았다면 이 메일을 무시하세요.
• 보안을 위해 이 링크를 타인과 공유하지 마세요.

© 2025 Riff. All rights reserved.
당신의 음악, 우리의 리프";
    }

    public string GenerateHtml()
    {
        return @$"
            <!DOCTYPE html>
            <html lang='ko'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Riff 이메일 인증</title>
                <style>
                    * {{
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }}

                    body {{
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
                        background: #f5f5f5;
                        color: #1a1a1a;
                        line-height: 1.6;
                        padding: 20px;
                    }}

                    .container {{
                        max-width: 600px;
                        margin: 0 auto;
                        background: #ffffff;
                        border-radius: 24px;
                        overflow: hidden;
                        box-shadow: 0 10px 40px rgba(0, 0, 0, 0.08);
                    }}

                    .header {{
                        text-align: center;
                        padding: 48px 32px 32px;
                        background: linear-gradient(135deg, rgba(20, 184, 166, 0.05) 0%, rgba(13, 148, 136, 0.05) 100%);
                        border-bottom: 1px solid rgba(0, 0, 0, 0.06);
                    }}

                    .logo {{
                        font-size: 32px;
                        font-weight: 700;
                        background: linear-gradient(135deg, #14b8a6, #0d9488);
                        -webkit-background-clip: text;
                        -webkit-text-fill-color: transparent;
                        background-clip: text;
                        margin-bottom: 8px;
                    }}

                    .subtitle {{
                        color: #666666;
                        font-size: 16px;
                        font-weight: 500;
                    }}

                    .content {{
                        padding: 48px 32px;
                    }}

                    .greeting {{
                        font-size: 24px;
                        font-weight: 700;
                        color: #1a1a1a;
                        margin-bottom: 16px;
                    }}

                    .message {{
                        color: #666666;
                        font-size: 15px;
                        margin-bottom: 32px;
                        line-height: 1.8;
                    }}

                    .button-container {{
                        text-align: center;
                        margin: 32px 0;
                    }}

                    .verify-button {{
                        display: inline-block;
                        padding: 16px 48px;
                        background: linear-gradient(135deg, #14b8a6, #0d9488);
                        color: #ffffff !important;
                        text-decoration: none;
                        border-radius: 12px;
                        font-weight: 700;
                        font-size: 16px;
                        box-shadow: 0 4px 12px rgba(20, 184, 166, 0.3);
                        transition: all 0.3s ease;
                    }}

                    .verify-button:hover {{
                        box-shadow: 0 6px 16px rgba(20, 184, 166, 0.4);
                        transform: translateY(-2px);
                    }}

                    .info-box {{
                        background: #f0fdfa;
                        border-left: 4px solid #14b8a6;
                        border-radius: 8px;
                        padding: 20px;
                        margin-top: 32px;
                    }}

                    .info-item {{
                        display: flex;
                        align-items: flex-start;
                        margin-bottom: 12px;
                        color: #666666;
                        font-size: 14px;
                        line-height: 1.6;
                    }}

                    .info-item:last-child {{
                        margin-bottom: 0;
                    }}

                    .info-icon {{
                        color: #14b8a6;
                        margin-right: 8px;
                        flex-shrink: 0;
                        margin-top: 2px;
                    }}

                    .url-fallback {{
                        margin-top: 24px;
                        padding: 16px;
                        background: #f8f9fa;
                        border-radius: 8px;
                        word-break: break-all;
                        font-size: 12px;
                        color: #666;
                    }}

                    .url-fallback-title {{
                        font-weight: 600;
                        color: #333;
                        margin-bottom: 8px;
                    }}

                    .url-link {{
                        color: #14b8a6;
                        word-wrap: break-word;
                    }}

                    .footer {{
                        padding: 32px;
                        text-align: center;
                        border-top: 1px solid rgba(0, 0, 0, 0.06);
                        background: #fafafa;
                    }}

                    .footer-text {{
                        color: #999999;
                        font-size: 13px;
                        line-height: 1.6;
                    }}

                    .footer-logo {{
                        color: #666666;
                        font-size: 12px;
                        margin-top: 8px;
                        font-weight: 600;
                    }}

                    /* Responsive */
                    @media (max-width: 640px) {{
                        body {{
                            padding: 12px;
                        }}

                        .container {{
                            border-radius: 16px;
                        }}

                        .header {{
                            padding: 32px 24px 24px;
                        }}

                        .logo {{
                            font-size: 28px;
                        }}

                        .content {{
                            padding: 32px 24px;
                        }}

                        .greeting {{
                            font-size: 20px;
                        }}

                        .verify-button {{
                            padding: 14px 36px;
                            font-size: 15px;
                        }}

                        .footer {{
                            padding: 24px;
                        }}
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <div class='logo'>Riff</div>
                        <div class='subtitle'>음악 장비 거래 플랫폼</div>
                    </div>

                    <div class='content'>
                        <h1 class='greeting'>환영합니다!</h1>
                        <p class='message'>
                            안녕하세요 Riff 입니다.<br>
                            Riff에 가입해 주셔서 감사합니다.<br><br>
                            아래 버튼을 클릭하여 이메일 인증을 완료해주세요.
                        </p>

                        <div class='button-container'>
                            <a href='{_verificationUrl}' class='verify-button'>이메일 인증하기</a>
                        </div>

                        <div class='info-box'>
                            <div class='info-item'>
                                <span class='info-icon'>•</span>
                                <span>이 인증 링크는 24시간 동안만 유효합니다.</span>
                            </div>
                            <div class='info-item'>
                                <span class='info-icon'>•</span>
                                <span>메일이 도착하지 않았다면 스팸함을 확인해주세요.</span>
                            </div>
                            <div class='info-item'>
                                <span class='info-icon'>•</span>
                                <span>본인이 가입하지 않았다면 이 메일을 무시하세요.</span>
                            </div>
                            <div class='info-item'>
                                <span class='info-icon'>•</span>
                                <span>보안을 위해 이 링크를 타인과 공유하지 마세요.</span>
                            </div>
                        </div>

                        <div class='url-fallback'>
                            <div class='url-fallback-title'>버튼이 작동하지 않는 경우:</div>
                            아래 링크를 복사하여 브라우저에 붙여넣으세요:<br>
                            <a href='{_verificationUrl}' class='url-link'>{_verificationUrl}</a>
                        </div>
                    </div>

                    <div class='footer'>
                        <p class='footer-text'>
                            © 2025 Riff. All rights reserved.
                        </p>
                        <p class='footer-logo'>
                            당신의 음악, 우리의 리프
                        </p>
                    </div>
                </div>
            </body>
            </html>
        ";
    }
}
