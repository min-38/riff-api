# CI/CD 설정 가이드 (GitHub Actions)

이 문서는 GitHub Actions를 사용한 자동 배포 설정 방법을 설명합니다.

## 개요

- **트리거**: `develop` 또는 `main` 브랜치에 push할 때 자동 실행
- **빌드**: GitHub Actions에서 Docker 이미지 빌드
- **배포**: SSH로 서버에 접속하여 자동 배포
- **검증**: 배포 후 헬스체크 및 상태 확인

## 1. GitHub Secrets 설정

GitHub Repository → Settings → Secrets and variables → Actions → New repository secret

다음 Secret들을 추가해야 합니다:

### 필수 Secrets

| Secret 이름 | 설명 | 예시 |
|-------------|------|------|
| `SERVER_HOST` | 서버 IP 주소 또는 도메인 | `123.456.789.012` |
| `SERVER_USERNAME` | 서버 SSH 사용자명 | `ubuntu` 또는 `root` |
| `SSH_PRIVATE_KEY` | SSH 개인키 (private key) | `-----BEGIN OPENSSH PRIVATE KEY-----...` |
| `DEPLOY_PATH` | 서버의 프로젝트 경로 | `/home/ubuntu/riff/api` |

### 선택 Secrets

| Secret 이름 | 설명 | 기본값 |
|-------------|------|--------|
| `SERVER_PORT` | SSH 포트 | `22` |
| `DOCKER_USERNAME` | Docker Hub 사용자명 (선택) | - |
| `DOCKER_PASSWORD` | Docker Hub 비밀번호 (선택) | - |

## 2. SSH 키 생성 및 설정

### 2.1 로컬에서 SSH 키 생성

```bash
# 새로운 SSH 키 생성
ssh-keygen -t ed25519 -C "github-actions-riff" -f ~/.ssh/riff_deploy_key

# 또는 RSA 방식
ssh-keygen -t rsa -b 4096 -C "github-actions-riff" -f ~/.ssh/riff_deploy_key

# 두 개의 파일이 생성됨:
# - riff_deploy_key (private key) - GitHub Secrets에 등록
# - riff_deploy_key.pub (public key) - 서버에 등록
```

### 2.2 서버에 공개키 등록

```bash
# 공개키 내용 확인
cat ~/.ssh/riff_deploy_key.pub

# 서버에 SSH 접속
ssh your-username@your-server-ip

# authorized_keys에 공개키 추가
mkdir -p ~/.ssh
chmod 700 ~/.ssh
echo "여기에 공개키 내용 붙여넣기" >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

### 2.3 GitHub Secrets에 개인키 등록

```bash
# 개인키 내용 확인 (전체 복사)
cat ~/.ssh/riff_deploy_key

# GitHub Repository 설정으로 가서:
# Settings → Secrets and variables → Actions → New repository secret
# Name: SSH_PRIVATE_KEY
# Secret: (위에서 복사한 전체 내용 붙여넣기)
```

**중요:** 개인키는 `-----BEGIN OPENSSH PRIVATE KEY-----`부터 `-----END OPENSSH PRIVATE KEY-----`까지 **전체**를 복사해야 합니다.

### 2.4 SSH 접속 테스트

```bash
# SSH 키로 접속 테스트
ssh -i ~/.ssh/riff_deploy_key your-username@your-server-ip

# 접속이 되면 성공!
```

## 3. 서버 준비

### 3.1 필수 소프트웨어 설치

```bash
# Git 설치
sudo apt update
sudo apt install git -y

# Docker 설치
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Docker Compose 설치
sudo apt install docker-compose -y

# 현재 사용자를 docker 그룹에 추가 (sudo 없이 docker 사용)
sudo usermod -aG docker $USER
newgrp docker
```

### 3.2 프로젝트 클론

```bash
# 프로젝트 디렉토리로 이동
cd ~

# Git 저장소 클론
git clone https://github.com/min-38/riff.git
cd riff/api

# develop 브랜치로 체크아웃
git checkout develop
```

### 3.3 환경 변수 설정

```bash
# .env 파일 생성
nano .env

# 아래 내용 입력 (실제 값으로 변경)
```

```env
# Database
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

# Redis (선택)
REDIS_HOST=""

# Test Email
TEST_EMAIL=your_test_email@example.com
```

### 3.4 Git 설정

CI/CD가 서버에서 git pull을 실행하므로, Git 설정이 필요합니다:

```bash
# Git 사용자 정보 설정
git config --global user.name "Your Name"
git config --global user.email "your@email.com"

# GitHub 인증 설정 (Private Repository인 경우)
# Personal Access Token 생성: GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
git config --global credential.helper store
git pull  # 첫 pull 시 username과 token 입력
```

**또는 SSH 방식:**

```bash
# GitHub에 SSH 키 등록
cat ~/.ssh/id_ed25519.pub  # 또는 id_rsa.pub

# GitHub → Settings → SSH and GPG keys → New SSH key에 등록

# Git remote를 SSH로 변경
git remote set-url origin git@github.com:min-38/riff.git
```

## 4. GitHub Secrets 등록 요약

GitHub Repository → Settings → Secrets and variables → Actions에서 다음을 등록:

```
SECRET_NAME=SERVER_HOST
VALUE=your-server-ip

SECRET_NAME=SERVER_USERNAME
VALUE=ubuntu

SECRET_NAME=SSH_PRIVATE_KEY
VALUE=-----BEGIN OPENSSH PRIVATE KEY-----
(전체 키 내용)
-----END OPENSSH PRIVATE KEY-----

SECRET_NAME=DEPLOY_PATH
VALUE=/home/ubuntu/riff/api

SECRET_NAME=SERVER_PORT (선택)
VALUE=22
```

## 5. 배포 테스트

### 5.1 수동 배포 테스트

먼저 서버에서 수동으로 테스트해봅니다:

```bash
cd /home/ubuntu/riff/api
git pull origin develop
docker-compose down
docker-compose build --no-cache
docker-compose up -d
docker-compose ps
```

### 5.2 GitHub Actions 실행

1. 코드 수정 후 commit & push:
```bash
git add .
git commit -m "Test CI/CD"
git push origin develop
```

2. GitHub Repository → Actions 탭에서 워크플로우 진행 상황 확인

3. 배포 성공 시 서버에서 확인:
```bash
docker-compose ps
curl http://localhost:8080/health
```

### 5.3 수동 실행

GitHub Actions 워크플로우를 수동으로 실행할 수도 있습니다:

1. GitHub Repository → Actions
2. "Deploy to Production" 워크플로우 선택
3. "Run workflow" 버튼 클릭
4. 브랜치 선택 후 실행

## 6. 문제 해결

### SSH 연결 실패

```bash
# 서버에서 SSH 로그 확인
sudo tail -f /var/log/auth.log

# SSH 키 권한 확인
chmod 600 ~/.ssh/authorized_keys
chmod 700 ~/.ssh
```

### Git Pull 실패

```bash
# 서버의 Git 상태 확인
cd /home/ubuntu/riff/api
git status

# 로컬 변경사항이 있으면 제거
git reset --hard HEAD
git clean -fd

# 원격 저장소 확인
git remote -v
```

### Docker 권한 오류

```bash
# Docker 그룹에 사용자 추가 확인
groups $USER

# docker 그룹이 없으면
sudo usermod -aG docker $USER
newgrp docker

# 또는 재로그인
exit
ssh your-username@your-server-ip
```

### 포트 충돌

```bash
# 8080 포트 사용 중인 프로세스 확인
sudo lsof -i :8080

# 프로세스 종료
sudo kill -9 <PID>
```

## 7. 알림 설정 (선택사항)

배포 성공/실패 시 Slack, Discord, 이메일 등으로 알림을 받을 수 있습니다.

### Slack 알림 예시

`.github/workflows/deploy.yml`에 추가:

```yaml
- name: Notify Slack
  if: always()
  uses: 8398a7/action-slack@v3
  with:
    status: ${{ job.status }}
    text: 'Deployment ${{ job.status }}'
    webhook_url: ${{ secrets.SLACK_WEBHOOK }}
  env:
    SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK }}
```

## 8. 보안 권장사항

1. **SSH 키는 절대 Git에 커밋하지 마세요**
2. **GitHub Secrets에만 저장하세요**
3. **서버 SSH 포트를 기본(22)에서 변경하는 것을 권장합니다**
4. **방화벽 설정으로 특정 IP만 SSH 접속 허용**
5. **정기적으로 SSH 키 교체**

## 9. 롤백

배포 후 문제가 발생하면 이전 버전으로 롤백:

```bash
# 서버에 SSH 접속
ssh your-username@your-server-ip
cd /home/ubuntu/riff/api

# 이전 커밋으로 롤백
git log --oneline  # 이전 커밋 확인
git checkout <commit-hash>

# 재배포
docker-compose down
docker-compose build --no-cache
docker-compose up -d

# 다시 최신으로
git checkout develop
```

## 10. 다음 단계

- [ ] 자동 테스트 추가 (Unit Tests, Integration Tests)
- [ ] 스테이징 환경 추가
- [ ] Blue-Green 배포 설정
- [ ] 모니터링 도구 연동 (Prometheus, Grafana)
- [ ] 로그 수집 (ELK Stack)

## 완료!

이제 `develop` 브랜치에 push하면 자동으로 배포됩니다! 🚀
