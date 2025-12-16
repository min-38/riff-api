# 도메인 설정 가이드 (api.riff.oouya.xyz)

## 개요

이 가이드는 Docker Nginx를 사용하여 `api.riff.oouya.xyz` 도메인을 설정하는 방법을 설명합니다.

**Cloudflare Proxied 모드 사용 시:**
- ✅ Cloudflare가 자동으로 HTTPS 처리
- ✅ Let's Encrypt 설정 불필요
- ✅ 서버는 HTTP (80번 포트)만 열면 됨

## 1. Cloudflare DNS 설정

### 1.1 Cloudflare 대시보드 접속

1. https://dash.cloudflare.com 로그인
2. `oouya.xyz` 도메인 선택
3. **DNS** → **Records** 클릭

### 1.2 A 레코드 추가

"Add record" 버튼을 클릭하고 다음과 같이 입력:

```
Type: A
Name: api.riff
IPv4 address: [오라클 클라우드 서버 IP 주소]
Proxy status: Proxied (🟠 주황색 구름)
TTL: Auto
```

**중요:** Proxy status를 **Proxied (주황색)**로 설정하면 Cloudflare가 자동으로 SSL을 처리합니다!

### 1.3 SSL/TLS 모드 확인

1. Cloudflare 대시보드 → **SSL/TLS** 메뉴
2. **Overview** 탭
3. 다음 중 하나로 설정:
   - ✅ **Flexible** (권장) - 브라우저 ↔ Cloudflare만 HTTPS, Cloudflare ↔ 서버는 HTTP
   - ✅ **Full** - Cloudflare ↔ 서버도 HTTPS (자체 서명 인증서 허용)

**Flexible 모드 권장:** 서버는 HTTP만 사용하면 되므로 설정이 가장 간단합니다.

### 1.4 DNS 전파 확인

```bash
# DNS 확인
nslookup api.riff.oouya.xyz

# 또는
dig api.riff.oouya.xyz
```

## 2. 서버 설정

### 2.1 방화벽 설정 (오라클 클라우드)

```bash
# SSH 접속
ssh your-username@your-server-ip

# 80번 포트 열기
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT

# 설정 저장
sudo netfilter-persistent save
```

### 2.2 오라클 클라우드 VCN 보안 목록

Oracle Cloud 콘솔에서:

1. **Networking** → **Virtual Cloud Networks**
2. 사용 중인 VCN 선택
3. **Security Lists** → **Default Security List**
4. **Add Ingress Rules**

**규칙 추가:**
```
Source CIDR: 0.0.0.0/0
IP Protocol: TCP
Destination Port Range: 80
```

## 3. 배포 및 실행

### 3.1 프로젝트 클론 (최초 1회)

```bash
# 프로젝트 디렉토리로 이동
cd ~

# Git 저장소 클론
git clone https://github.com/min-38/riff.git
cd riff/api

# .env 파일 생성 및 환경 변수 설정
nano .env
```

### 3.2 Docker Compose로 실행

```bash
# 빌드 및 실행 (Nginx + API)
docker-compose up -d

# 로그 확인
docker-compose logs -f

# 컨테이너 상태 확인
docker-compose ps
```

**실행되는 컨테이너:**
- `riff-nginx` - Nginx (80번 포트, 외부 노출)
- `riff-api` - ASP.NET Core API (8080번 포트, 내부만)

### 3.3 접속 테스트

```bash
# 로컬에서 테스트
curl http://localhost

# 외부에서 테스트
curl http://api.riff.oouya.xyz

# HTTPS 테스트 (Cloudflare가 자동 처리)
curl https://api.riff.oouya.xyz
```

## 4. 파일 구조

```
api/
├── nginx.conf              # Nginx 설정 (Docker용)
├── docker-compose.yml      # Nginx + API 컨테이너 정의
├── Dockerfile              # API 이미지 빌드
├── .env                    # 환경 변수 (수동 생성)
└── ...
```

## 5. 트러블슈팅

### 502 Bad Gateway

```bash
# API 컨테이너 상태 확인
docker-compose ps

# API 로그 확인
docker-compose logs api

# API 컨테이너 재시작
docker-compose restart api
```

### Nginx 설정 오류

```bash
# Nginx 컨테이너 로그 확인
docker-compose logs nginx

# Nginx 설정 파일 문법 검사
docker-compose exec nginx nginx -t

# Nginx 재시작
docker-compose restart nginx
```

### 포트 충돌

```bash
# 80번 포트 사용 확인
sudo lsof -i :80

# 기존 프로세스 종료
sudo kill -9 <PID>
```

### DNS가 안 되는 경우

```bash
# 로컬 DNS 캐시 초기화
# macOS
sudo dscacheutil -flushcache

# Windows
ipconfig /flushdns

# Linux
sudo systemd-resolve --flush-caches
```

## 6. 업데이트 및 재배포

### 수동 배포

```bash
# 최신 코드 가져오기
git pull origin develop

# 컨테이너 재시작
docker-compose down
docker-compose up -d --build
```

### CI/CD 자동 배포

`develop` 브랜치에 push하면 GitHub Actions가 자동으로 배포합니다.

자세한 내용은 [CICD_SETUP.md](./CICD_SETUP.md)를 참고하세요.

## 7. 도메인 추가 (다른 서브도메인)

같은 서버에서 다른 서비스도 실행한다면:

### 옵션 1: Nginx 설정에 server 블록 추가

`nginx.conf`에 다른 서버 블록 추가:

```nginx
http {
    server {
        listen 80;
        server_name api.riff.oouya.xyz;
        location / {
            proxy_pass http://api:8080;
        }
    }

    server {
        listen 80;
        server_name another.oouya.xyz;
        location / {
            proxy_pass http://another-service:3000;
        }
    }
}
```

### 옵션 2: 별도 docker-compose로 분리

각 서비스마다 독립적인 docker-compose 사용

## 8. 보안 권장사항

1. ✅ Cloudflare Proxied 모드 사용 (DDoS 방어)
2. ✅ `.env` 파일 권한 설정: `chmod 600 .env`
3. ✅ 정기적인 업데이트: `docker-compose pull && docker-compose up -d`
4. ✅ 방화벽에서 불필요한 포트 차단
5. ✅ SSH 포트 변경 (기본 22번 대신)

## 완료!

이제 다음 URL로 API에 접속할 수 있습니다:

- **HTTP**: `http://api.riff.oouya.xyz` (Cloudflare가 HTTPS로 리다이렉트)
- **HTTPS**: `https://api.riff.oouya.xyz` ✅

Cloudflare가 자동으로 SSL을 처리하므로 추가 설정이 필요 없습니다!
