// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;
public class MediaPiControllerPreBase(AppDbContext db, ILogger logger) : ControllerBase
{
    protected readonly AppDbContext _db = db;
    protected readonly ILogger _logger = logger;

    protected ObjectResult _400()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Нарушена целостность запроса" });
    }

    protected ObjectResult _400DeviceIpMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не указан IP адрес устройства" });
    }

    protected ObjectResult _400DevicePortMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не указан порт устройства" });
    }

    protected ObjectResult _400DevicePortInvalid(ushort port)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Неверный порт устройства {port}. Порт должен быть в диапазоне 1-65535" });
    }

    protected ObjectResult _400DeviceServerKeyMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не указан ключ сервера устройства" });
    }
    protected ObjectResult _400Ip(string ip)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Неверный формат IP адреса \"{ip}\"" });
    }
    protected ObjectResult _401()
    {
        return StatusCode(StatusCodes.Status401Unauthorized,
                          new ErrMessage { Msg = "Неправильный адрес электронной почты или пароль" });
    }
    protected ObjectResult _403()
    {
        return StatusCode(StatusCodes.Status403Forbidden,
                          new ErrMessage { Msg = "Недостаточно прав для выполнения операции" });
    }
    protected ObjectResult _404User(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Пользователь с ID {id} не найден" });
    }
    protected ObjectResult _404Device(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Устройство с ID {id} не найдено" });
    }
    protected ObjectResult _404DeviceGroup(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Группа устройств с ID {id} не найдена" });
    }
    protected ObjectResult _404Account(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Лицевой счёт с ID {id} не найден" });
    }
    protected ObjectResult _404Playlist(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Плейлист с ID {id} не найден" });
    }
    protected ObjectResult _404Video(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Видеофайл с ID {id} не найден" });
    }
    protected ObjectResult _404Screenshot(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Скриншот с ID {id} не найден" });
    }
    protected ObjectResult _404Category(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Категория с ID {id} не найдена" });
    }
    protected ObjectResult _409Email(string email)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Пользователь с адресом электронной почты \"{email}\" уже зарегистрирован" });
    }

    protected ObjectResult _409Ip(string ip)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Устройство с IP адресом \"{ip}\" уже зарегистрировано" });
    }

    protected ObjectResult _409Account(string name)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Лицевой счёт с именем \"{name}\" уже существует" });
    }

    protected ObjectResult _409VideoFilename(string filename)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Видео с именем файла \"{filename}\" уже существует" });
    }

    protected ObjectResult _409VideoOriginalFilename(string originalFilename, int? accountId = null, int? categoryId = null)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage
                          {
                              Msg = VideoOriginalFilenameConflictMessage(originalFilename),
                              Reason = ConflictReasons.DuplicateOriginalFilename,
                              OriginalFilename = originalFilename,
                              AccountId = accountId,
                              CategoryId = categoryId
                          });
    }

    protected static string VideoOriginalFilenameConflictMessage(string originalFilename) =>
        $"В выбранном разделе уже есть видеофайл с именем \"{originalFilename}\"";

    protected ObjectResult _409VideoDescription(string title, int? accountId = null, int? categoryId = null)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage
                          {
                              Msg = VideoDescriptionConflictMessage(title),
                              Reason = ConflictReasons.DuplicateVideoDescription,
                              AccountId = accountId,
                              CategoryId = categoryId
                          });
    }

    protected static string VideoDescriptionConflictMessage(string title) =>
        $"В выбранном разделе уже есть видеофайл с описанием \"{title}\"";

    protected ObjectResult _409PlaylistFilename(string filename)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage
                          {
                              Msg = $"Плейлист с именем файла \"{filename}\" уже существует",
                              Reason = ConflictReasons.DuplicatePlaylistFilename
                          });
    }

    protected ObjectResult _409PlaylistDescription(string title)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage
                          {
                              Msg = $"Плейлист с описанием \"{title}\" уже существует",
                              Reason = ConflictReasons.DuplicatePlaylistDescription
                          });
    }

    protected ObjectResult _409Category(string title)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Категория с названием \"{title}\" уже существует" });
    }

    protected ObjectResult _409CategoryInUse(int id)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Невозможно удалить категорию с ID {id}, так как она используется" });
    }

    protected ObjectResult _400PlaylistVideoAccountMismatch(int videoId, int accountId)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Видео с ID {videoId} не относится к лицевому счёту с ID {accountId}" });
    }

    protected ObjectResult _400PlaylistVideoAccessDenied(int videoId, int accountId)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Видео с ID {videoId} недоступно для плейлиста лицевого счёта с ID {accountId} по условиям подписки" });
    }

    protected ObjectResult _400VideoPlaylistAccountMismatch(int playlistId, int accountId)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Плейлист с ID {playlistId} не относится к лицевому счёту с ID {accountId}" });
    }

    protected ObjectResult _400PlaylistItemPositionsNegative()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Playlist item positions must be non-negative" });
    }

    protected ObjectResult _400PlaylistItemPositionsDuplicate()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Playlist item positions must be unique" });
    }

    protected ObjectResult _409DeviceGroupAccountMismatch(int deviceGroupId, int? deviceAccountId)
    {
        var deviceAccountMsg = deviceAccountId.HasValue ? $"лицевого счёта с ID {deviceAccountId}" : "не назначено лицевого счёта";
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Группа устройств с ID {deviceGroupId} не принадлежит к тому же лицевому счёту, что и устройство ({deviceAccountMsg})" });
    }

    protected ObjectResult _400ServiceUnit(string? unit)
    {
        var displayValue = string.IsNullOrWhiteSpace(unit) ? "<пусто>" : unit;
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Неверное имя сервиса \"{displayValue}\"" });
    }

    protected ObjectResult _400VideoFileMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не удалось загрузить видеофайл: отсутствует файл" });
    }

    protected ObjectResult _400ScreenshotFileMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не удалось загрузить скриншот: отсутствует файл" });
    }

    protected ObjectResult _400VideoTitleMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не удалось загрузить видеофайл: отсутствует описание" });
    }

    protected ObjectResult _400VideoCategoryOnlyForCommon()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Категория может быть назначена только общим видеофайлам" });
    }

    protected ObjectResult _400VideoCategoryMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не указана категория видеофайла" });
    }

    protected ObjectResult _400SubscriptionDateRangeInvalid()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Дата окончания подписки не может быть раньше даты начала" });
    }

    protected ObjectResult _400SubscriptionCategoryFree(int categoryId)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Категория с ID {categoryId} доступна без подписки" });
    }

    protected ObjectResult _400RequestPayloadMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не указано содержимое запроса" });
    }

    protected ObjectResult _502Agent(string? message = null)
    {
        const string baseMessage = "Ошибка при обращении к агенту устройства";
        var finalMessage = string.IsNullOrWhiteSpace(message) ? baseMessage : $"{baseMessage}: {message}";
        return StatusCode(StatusCodes.Status502BadGateway,
                          new ErrMessage { Msg = finalMessage });
    }

    protected ObjectResult _500Mapping(string fname)
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = $"Не найдена спецификация файла реестра с именем \"{fname}\"" });
    }

    protected ObjectResult _500UploadRegister()
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = "Внутренняя ошибка при загрузке файла реестра" });
    }

    protected ObjectResult _500VideoManifestFieldMissing(int videoId, string fieldName)
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = $"Отсутствует обязательное поле \"{fieldName}\" для видео с ID {videoId}" });
    }

    protected ObjectResult _500DeviceIdMissing()
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = "Middleware авторизации устройства не установил DeviceId" });
    }

    protected ObjectResult _500ScreenshotPersistence()
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = "Внутренняя ошибка при сохранении снимка экрана" });
    }

    protected ObjectResult _500VideoStorageSaveFailed(string originalFilename, int? accountId = null, int? categoryId = null)
    {
        return VideoUploadError(
            StatusCodes.Status500InternalServerError,
            $"Не удалось сохранить видеофайл \"{originalFilename}\" на сервере",
            ConflictReasons.VideoStorageSaveFailed,
            originalFilename,
            accountId,
            categoryId);
    }

    protected ObjectResult _500VideoUploadCleanupFailed(string originalFilename, int? accountId = null, int? categoryId = null)
    {
        return VideoUploadError(
            StatusCodes.Status500InternalServerError,
            $"Не удалось очистить временный файл после ошибки загрузки видеофайла \"{originalFilename}\"",
            ConflictReasons.VideoUploadCleanupFailed,
            originalFilename,
            accountId,
            categoryId);
    }

    protected ObjectResult _500VideoUploadProcessingFailed(string originalFilename, int? accountId = null, int? categoryId = null)
    {
        return VideoUploadError(
            StatusCodes.Status500InternalServerError,
            $"Внутренняя ошибка при обработке видеофайла \"{originalFilename}\"",
            ConflictReasons.VideoUploadProcessingFailed,
            originalFilename,
            accountId,
            categoryId);
    }

    protected ObjectResult _413VideoUploadTooLarge()
    {
        return VideoUploadError(
            StatusCodes.Status413PayloadTooLarge,
            "Размер загружаемого файла превышает допустимый предел",
            ConflictReasons.VideoUploadTooLarge);
    }

    protected ObjectResult _403DeviceUnauthorizedVideo(int deviceId, int videoId)
    {
        return StatusCode(StatusCodes.Status403Forbidden,
                          new ErrMessage { Msg = $"Устройство с ID {deviceId} не имеет доступа к видео с ID {videoId}" });
    }

    protected ObjectResult VideoUploadError(
        int statusCode,
        string message,
        string reason,
        string? originalFilename = null,
        int? accountId = null,
        int? categoryId = null)
    {
        return StatusCode(statusCode,
                          new ErrMessage
                          {
                              Msg = message,
                              Reason = reason,
                              OriginalFilename = originalFilename,
                              AccountId = accountId,
                              CategoryId = categoryId
                          });
    }

    /// <summary>
    /// Computes pagination parameters. Handles pageSize = -1 (all items) and out-of-range page numbers.
    /// </summary>
    protected static (int ActualPage, int ActualPageSize, int TotalPages) ComputePagination(
        int page, int pageSize, int totalCount)
    {
        int actualPageSize = pageSize == -1 ? (totalCount == 0 ? 1 : totalCount) : pageSize;
        int totalPages = (int)Math.Ceiling(totalCount / (double)actualPageSize);
        int actualPage = (page > totalPages && totalPages > 0) ? 1 : (pageSize == -1 ? 1 : page);
        return (actualPage, actualPageSize, totalPages);
    }

    /// <summary>
    /// Creates a fully populated PaginationInfo from page/pageSize/totalCount.
    /// </summary>
    protected static PaginationInfo CreatePaginationInfo(int page, int pageSize, int totalCount)
    {
        var (actualPage, actualPageSize, totalPages) = ComputePagination(page, pageSize, totalCount);
        return new PaginationInfo
        {
            CurrentPage = actualPage,
            PageSize = actualPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNextPage = actualPage < totalPages,
            HasPreviousPage = actualPage > 1
        };
    }
}

public class MediaPiControllerBase : MediaPiControllerPreBase
{

    protected readonly int _curUserId;

    protected MediaPiControllerBase(IHttpContextAccessor httpContextAccessor, AppDbContext db, ILogger logger): base(db, logger)
    {
        _curUserId = 0;
        var htc = httpContextAccessor.HttpContext;
        if (htc != null)
        {
            var uid = htc.Items["UserId"];
            if (uid != null) _curUserId = (int)uid;
        }
    }

    protected async Task<User?> CurrentUser()
    {
        return await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserAccounts)
            .FirstOrDefaultAsync(u => u.Id == _curUserId);
    }

}
