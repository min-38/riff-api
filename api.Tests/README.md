# api.Tests

## 테스트 전 확인 사항

### .env.test 파일 생성
```bash
cd api.Tests
```

`api/.env`의 설정값을 `.env.test`에 복사:

### 이메일 테스트 설정
```env
# 실제 이메일을 보내고 싶으면 true로 변경
SEND_ACTUAL_EMAIL=true
```

## 테스트 실행

전체 테스트:
```bash
dotnet test
```

Integration 제외:
```bash
dotnet test --filter "Category!=Integration"
```

특정 파일만:
```bash
dotnet test --filter "FullyQualifiedName~EmailServiceTests"
```
