# Riff API 배포 가이드

## 배포 방식

이 프로젝트는 **CI/CD (GitHub Actions)** 방식으로 자동 배포됩니다.

- **자동 배포**: `develop` 또는 `main` 브랜치에 push하면 자동으로 배포
- **수동 배포**: 서버에서 `./deploy.sh` 실행 (백업 방법)

**CI/CD 설정 방법은 [CICD_SETUP.md](./CICD_SETUP.md) 문서를 참고하세요.**

## 사전 요구사항

서버에 다음 소프트웨어가 설치되어 있어야 합니다:

- Docker
- Docker Compose
- Git

## 초기 배포 (최초 1회)

### 1. 서버에 Git 저장소 클론

```bash
cd /path/to/your/projects
git clone <repository-url>
cd riff/api
```

### 2. 환경 변수 설정

`.env` 파일을 생성하고 필요한 환경 변수를 설정합니다:

```bash
# .env 파일 예시
DATABASE_HOST=10.166.244.1
DATABASE_PORT=55007
DATABASE_NAME=riff_db
DATABASE_USER=dev01
DATABASE_PASSWORD=your_password

# Email (Oracle SMTP)
SMTP_HOST=smtp.email.ap-chuncheon-1.oci.oraclecloud.com
SMTP_PORT=587
SMTP_USERNAME=your_username
SMTP_PASSWORD=your_password
SMTP_FROM_EMAIL=no_reply@oouya.xyz
SMTP_FROM_NAME=Riff

# 실제 이메일 발송 여부
SEND_ACTUAL_EMAIL=false

# JWT Config
JWT_SECRET_KEY=your_secret_key_here
JWT_ISSUER=Riff
JWT_EXPIRATION_MINUTES=60

# Test Email
TEST_EMAIL=your_test_email@example.com
```

### 3. 배포 스크립트 실행

```bash
chmod +x deploy.sh
./deploy.sh
```

## 재배포 (코드 업데이트 시)

코드가 업데이트되었을 때는 간단히 배포 스크립트만 실행하면 됩니다:

```bash
./deploy.sh
```

배포 스크립트는 다음 작업을 자동으로 수행합니다:

1. 최신 코드 가져오기 (git pull)
2. 기존 컨테이너 중지
3. Docker 이미지 빌드
4. 사용하지 않는 이미지 정리
5. 새 컨테이너 실행

## 수동 배포 (커스터마이징이 필요한 경우)

### Docker Compose 명령어

```bash
# 빌드 및 실행
docker-compose up -d --build

# 중지
docker-compose down

# 로그 확인
docker-compose logs -f

# 컨테이너 상태 확인
docker-compose ps
```

### Docker 명령어

```bash
# 이미지 빌드
docker build -t riff-api .

# 컨테이너 실행
docker run -d \
  -p 8080:8080 \
  --env-file .env \
  --name riff-api \
  riff-api

# 로그 확인
docker logs -f riff-api

# 컨테이너 중지 및 제거
docker stop riff-api
docker rm riff-api
```

## 포트 설정

- API 서버: `8080`

외부에서 접근하려면 방화벽 설정을 확인하세요:

```bash
# Oracle Cloud의 경우 iptables 설정
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 8080 -j ACCEPT
sudo netfilter-persistent save
```

## 헬스체크

API가 정상적으로 실행되고 있는지 확인:

```bash
curl http://localhost:8080/health
```

## 트러블슈팅

### 컨테이너가 시작되지 않는 경우

```bash
# 로그 확인
docker-compose logs

# 특정 서비스 로그 확인
docker-compose logs api
```

### 포트가 이미 사용중인 경우

```bash
# 포트 사용 확인
sudo lsof -i :8080

# 프로세스 종료
sudo kill -9 <PID>
```

### 데이터베이스 연결 오류

1. `.env` 파일의 데이터베이스 설정 확인
2. 네트워크 연결 확인
3. 데이터베이스 서버가 실행중인지 확인

### Docker 이미지 캐시 문제

```bash
# 캐시 없이 완전히 새로 빌드
docker-compose build --no-cache

# 사용하지 않는 이미지 모두 제거
docker image prune -a
```

## 롤백

문제가 발생했을 때 이전 버전으로 롤백:

```bash
# 이전 커밋으로 이동
git log  # 이전 커밋 해시 확인
git checkout <commit-hash>

# 재배포
./deploy.sh

# 다시 최신으로 돌아가기
git checkout main  # 또는 develop
```

## 보안 주의사항

1. `.env` 파일은 **절대 Git에 커밋하지 마세요**
2. JWT Secret은 충분히 복잡하게 설정하세요
3. Production 환경에서는 HTTPS를 사용하세요 (Nginx 리버스 프록시 권장)
4. 데이터베이스 비밀번호를 정기적으로 변경하세요

## Nginx 리버스 프록시 설정 (선택사항)

HTTPS 설정을 위한 Nginx 설정 예시:

```nginx
server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## 모니터링

컨테이너 상태를 주기적으로 확인:

```bash
# 실행중인 컨테이너 확인
docker ps

# 리소스 사용량 확인
docker stats

# 로그 실시간 확인
docker-compose logs -f --tail=100
```
