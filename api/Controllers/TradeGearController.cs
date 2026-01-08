using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using api.Constants;
using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("trade/gears")]
public class TradeGearController : ControllerBase
{
    private readonly ITradeGearService _tradeGearService;
    private readonly ILogger<TradeGearController> _logger;

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var userIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out userId);
    }

    public TradeGearController(ITradeGearService tradeGearService, ILogger<TradeGearController> logger)
    {
        _tradeGearService = tradeGearService;
        _logger = logger;
    }

    // 판매글 작성
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<GearResponse>> CreateGear([FromForm] CreateGearRequest request, [FromForm] List<IFormFile>? images = null)
    {
        try
        {
            // JWT 토큰에서 사용자 ID 추출 -> 미인증 사용자 접근 차단
            if (!TryGetUserId(User, out var userId))
                return Unauthorized(new { code = ErrorCodes.INVALID_TOKEN });

            if (images != null && images.Count < 1)
                return BadRequest(new { code = ErrorCodes.IMAGE_REQUIRED });
            if (images != null && images.Count > ImageConstants.MaxImageCount)
                return BadRequest(new { code = ErrorCodes.TOO_MANY_IMAGES, data = new { max = ImageConstants.MaxImageCount } });

            // 이미지 파일 검증 및 Stream 리스트로 변환
            List<Stream>? imageStreams = null;
            if (images != null && images.Count > 0)
            {
                imageStreams = new List<Stream>();
                foreach (var image in images)
                {
                    if (image.Length > 0)
                    {
                        // 파일 크기 체크
                        if (image.Length > ImageConstants.MaxImageSizeBytes)
                        {
                            _logger.LogWarning("Image too large: {Size}MB (max: {MaxSize}MB)",
                                image.Length / 1024 / 1024, ImageConstants.MaxImageSizeMB);
                            continue;
                        }

                        // 이미지가 아닌 파일은 스킵
                        if (!ImageConstants.AllowedImageTypes.Contains(image.ContentType.ToLower()))
                        {
                            _logger.LogWarning("Invalid file type rejected: {ContentType} for file {FileName}",
                                image.ContentType, image.FileName);
                            continue;
                        }

                        // Stream으로 변환
                        var stream = new MemoryStream();
                        await image.CopyToAsync(stream);
                        stream.Position = 0;
                        imageStreams.Add(stream);
                    }
                }

                // 유효한 이미지가 하나도 없으면 에러 반환
                if (imageStreams.Count == 0)
                    return BadRequest(new { code = ErrorCodes.NO_VALID_IMAGES });
            }

            // 판매글 생성
            var gear = await _tradeGearService.CreateGearAsync(userId, request, imageStreams);

            // Stream 정리
            if (imageStreams != null)
                foreach (var stream in imageStreams)
                    await stream.DisposeAsync();

            return CreatedAtAction(nameof(GetGearById), new { id = gear.Id }, gear);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating gear");
            return BadRequest(new { code = ErrorCodes.INVALID_OPERATION, details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating gear");
            return StatusCode(500, new { code = ErrorCodes.INTERNAL_SERVER_ERROR });
        }
    }

    // 판매글 목록 조회
    [HttpGet]
    public async Task<ActionResult<GearListResponse>> GetGears([FromQuery] GetGearsRequest request)
    {
        try
        {
            var response = await _tradeGearService.GetGearsAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving gear list");
            return StatusCode(500, new { code = ErrorCodes.INTERNAL_SERVER_ERROR });
        }
    }

    // 판매글 상세 조회
    [HttpGet("{id}")]
    public async Task<ActionResult<GearResponse>> GetGearById(long id)
    {
        try
        {
            // 조회수 증가를 위한 사용자 정보 추출
            Guid? userId = null;
            if (TryGetUserId(User, out var parsedUserId))
                userId = parsedUserId;

            // IP 주소 추출
            var ipAddress = HttpContext.Connection.RemoteIpAddress;

            var gear = await _tradeGearService.GetGearByIdAsync(id, incrementViews: true, userId, ipAddress);
            return Ok(gear);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Gear not found: {GearId}", id);
            return NotFound(new { code = ErrorCodes.NOT_FOUND });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving gear: {GearId}", id);
            return StatusCode(500, new { code = ErrorCodes.INTERNAL_SERVER_ERROR });
        }
    }

    // 판매글 수정
    [HttpPatch("{id}")]
    [Authorize]
    public async Task<ActionResult<GearResponse>> UpdateGear(long id, [FromForm] UpdateGearRequest request, [FromForm] List<IFormFile>? images = null)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
                return Unauthorized(new { code = ErrorCodes.INVALID_TOKEN });

            // 이미지가 제공된 경우 검증 및 처리
            List<Stream>? imageStreams = null;
            if (images != null && images.Count > 0)
            {
                // 이미지 개수 제한 체크
                if (images.Count > ImageConstants.MaxImageCount)
                    return BadRequest(new { code = ErrorCodes.TOO_MANY_IMAGES, data = new { max = ImageConstants.MaxImageCount } });

                imageStreams = new List<Stream>();
                foreach (var image in images)
                {
                    if (image.Length > 0)
                    {
                        // 파일 크기 체크
                        if (image.Length > ImageConstants.MaxImageSizeBytes)
                        {
                            _logger.LogWarning("Image too large: {Size}MB (max: {MaxSize}MB)",
                                image.Length / 1024 / 1024, ImageConstants.MaxImageSizeMB);
                            continue;
                        }

                        // 이미지가 아닌 파일은 스킵
                        if (!ImageConstants.AllowedImageTypes.Contains(image.ContentType.ToLower()))
                        {
                            _logger.LogWarning("Invalid file type rejected: {ContentType} for file {FileName}",
                                image.ContentType, image.FileName);
                            continue;
                        }

                        // Stream으로 변환
                        var stream = new MemoryStream();
                        await image.CopyToAsync(stream);
                        stream.Position = 0;
                        imageStreams.Add(stream);
                    }
                }

                // 유효한 이미지가 하나도 없으면 에러 반환
                if (imageStreams.Count == 0)
                    return BadRequest(new { code = ErrorCodes.NO_VALID_IMAGES });
            }

            var gear = await _tradeGearService.UpdateGearAsync(id, userId, request, imageStreams);

            // Stream 정리
            if (imageStreams != null)
                foreach (var stream in imageStreams)
                    await stream.DisposeAsync();

            return Ok(gear);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized gear update attempt: {GearId}", id);
            return StatusCode(403, new { code = ErrorCodes.FORBIDDEN });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Gear not found: {GearId}", id);
            return NotFound(new { code = ErrorCodes.NOT_FOUND });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating gear: {GearId}", id);
            return StatusCode(500, new { code = ErrorCodes.INTERNAL_SERVER_ERROR });
        }
    }

    // 판매글 삭제
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteGear(long id)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
                return Unauthorized(new { code = ErrorCodes.INVALID_TOKEN });

            await _tradeGearService.DeleteGearAsync(id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized gear delete attempt: {GearId}", id);
            return StatusCode(403, new { code = ErrorCodes.FORBIDDEN });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Gear not found: {GearId}", id);
            return NotFound(new { code = ErrorCodes.NOT_FOUND });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting gear: {GearId}", id);
            return StatusCode(500, new { code = ErrorCodes.INTERNAL_SERVER_ERROR });
        }
    }
}

