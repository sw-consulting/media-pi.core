// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;
public class FuelfluxControllerPreBase(AppDbContext db, ILogger logger) : ControllerBase
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

    protected ObjectResult _400DeviceServerKeyMissing()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = "Не указан ключ сервера устройства" });
    }
    protected ObjectResult _400Ip(string ip)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Неверный формат IP адреса [{ip}]" });
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
                          new ErrMessage { Msg = $"Не удалось найти пользователя [id={id}]" });
    }
    protected ObjectResult _404Device(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти устройство [id={id}]" });
    }
    protected ObjectResult _404DeviceGroup(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти группу устройств [id={id}]" });
    }
    protected ObjectResult _404Account(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти лицевой счёт [id={id}]" });
    }
    protected ObjectResult _409Email(string email)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Пользователь с таким адресом электронной почты уже зарегистрирован [email = {email}]" });
    }

    protected ObjectResult _409Ip(string ip)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Устройство с таким IP адресом уже зарегистрировано [ip = {ip}]" });
    }

    protected ObjectResult _409Account(string name)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Лицевой счёт с таким именем уже существует [name = {name}]" });
    }

    protected ObjectResult _409DeviceGroupAccountMismatch(int deviceGroupId, int? deviceAccountId)
    {
        var deviceAccountMsg = deviceAccountId.HasValue ? $"лицевого счёта [id={deviceAccountId}]" : "не назначено лицевого счёта";
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Группа устройств [id={deviceGroupId}] не принадлежит к тому же лицевому счёту, что и устройство ({deviceAccountMsg})" });
    }

    protected ObjectResult _400ServiceUnit(string? unit)
    {
        var displayValue = string.IsNullOrWhiteSpace(unit) ? "<пусто>" : unit;
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Неверное имя сервиса [{displayValue}]" });
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
                          new ErrMessage { Msg = $"Не найдена спецификация файла реестра [имя файла = {fname}]" });
    }

    protected ObjectResult _500UploadRegister()
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = "Внутренняя ошибка при загрузке файла реестра" });
    }
}

public class MediaPiControllerBase : FuelfluxControllerPreBase
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
