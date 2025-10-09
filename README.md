
# Media Pi Core

[![ci](https://github.com/sw-consulting/media-pi.core/actions/workflows/ci.yml/badge.svg)](https://github.com/sw-consulting/media-pi.core/actions/workflows/ci.yml)
[![publish](https://github.com/sw-consulting/media-pi.core/actions/workflows/publish.yml/badge.svg)](https://github.com/sw-consulting/media-pi.core/actions/workflows/publish.yml)
[![codecov](https://codecov.io/gh/sw-consulting/media-pi.core/graph/badge.svg?token=38PH477K08)](https://codecov.io/gh/sw-consulting/media-pi.core)

Media Pi System Core

## Инструкция по развёртыванию

1) Скопировать на сервер в произвольный каталог файлы сертификаты сайта (нужны файлы crt, key, pfx).

   - Все файлы должны иметь имя `s`, то есть ожидаются файлы `s.crt` (сертификат), `s.key` (приватный ключ сертификата), `s.pfx` (PKCS #12)

3) Скачать и установить на сервер Docker Desktop [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/)

4) Скопировать на сервер файл docker-compose-ghcr.yml

5) Открыть файл docker-compose-ghcr.yml и внести следующие изменения:
   - заменить путь, отмеченный комментарием "# Пполный путь к корневому каталогу базы данных", на полный путь к корневому каталогу базы данных. Каталог создавать не надо, система создаст его при запуске.
   - заменить путь, отмеченный комментарием "# Полный путь к каталогу резервной копии базы данных", на полный путь к каталогу резервной копии базы данных. Каталог создавать не надо, система создаст его при запуске.
   - заменить путь, отмеченный комментарием "# Полный путь к каталогу журналов резервного копирования", на полный путь к каталогу журналов резервного копирования. Каталог создавать не надо, система создаст его при запуске.
   - заменить путь, отмеченный комментарием "# Полный путь к каталогу с сертификатами сайта", на полный путь к каталогу с сертификатами сайта (см. пункт 1)


6) В строке, отмеченной #пароль к PKCS #12, заменить <password> на ваш пароль к файлу PKCS#12 (s.pfx)

7) Проверить что API URL в конфиграции UI задан правильно и указывает на публично доступный URL/port контейнера media-pi.core.  Этот URL задаётся в строке, помеченной комментарием "# URL API из Host network (а не Docker network)"

8) В каталоге с файлом docker-compose-ghcr.yml выполнить `docker compose pull -f docker-compose-ghcr.yml`

Система готова к запуску.
Запуск осуществляется с помощью команд докера (`docker compose up`, `docker compose start`).

