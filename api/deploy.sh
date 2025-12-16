#!/bin/bash

# Riff API 배포 스크립트
# Usage: ./deploy.sh

set -e  # 에러 발생시 스크립트 중단

echo "========================================="
echo "  Riff API 배포 시작"
echo "========================================="

# 현재 브랜치 확인
CURRENT_BRANCH=$(git branch --show-current)
echo "현재 브랜치: $CURRENT_BRANCH"

# Git Pull
echo ""
echo "[1/5] 최신 코드 가져오기..."
git pull origin $CURRENT_BRANCH

# 기존 컨테이너 중지 및 제거
echo ""
echo "[2/5] 기존 컨테이너 중지..."
docker-compose down || true

# Docker 이미지 빌드
echo ""
echo "[3/5] Docker 이미지 빌드..."
docker-compose build --no-cache

# 사용하지 않는 이미지 정리
echo ""
echo "[4/5] 사용하지 않는 이미지 정리..."
docker image prune -f

# 컨테이너 실행
echo ""
echo "[5/5] 컨테이너 실행..."
docker-compose up -d

# 상태 확인
echo ""
echo "========================================="
echo "  배포 완료!"
echo "========================================="
echo ""
docker-compose ps
echo ""
echo "로그 확인: docker-compose logs -f"
echo "컨테이너 중지: docker-compose down"
