// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.AspNetCore.Mvc;
using MediaPi.Core.RestModels;
using MediaPi.Core.Data;

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
    protected ObjectResult _400EmptyRegister()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Пустой файл реестра" });
    }
    protected ObjectResult _400NoRegister()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Файл реестра не найден в архиве" });
    }
    protected ObjectResult _400UnsupportedFileType(string ext)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = $"Файлы формата {ext} не поддерживаются. Можно загрузить .xlsx, .xls, .zip, .rar" });
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
    protected ObjectResult _404Register(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти реестр [id={id}]" });
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

    protected ObjectResult _400Ip(string ip)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage { Msg = $"Неверный формат IP адреса [{ip}]" });
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
}
