
# Media Pi Core

[![ci](https://github.com/sw-consulting/media-pi.core/actions/workflows/ci.yml/badge.svg)](https://github.com/sw-consulting/media-pi.core/actions/workflows/ci.yml)
[![publish](https://github.com/sw-consulting/media-pi.core/actions/workflows/publish.yml/badge.svg)](https://github.com/sw-consulting/media-pi.core/actions/workflows/publish.yml)
[![codecov](https://codecov.io/gh/sw-consulting/media-pi.core/graph/badge.svg?token=38PH477K08)](https://codecov.io/gh/sw-consulting/media-pi.core)

Media Pi System Core

## Инструкция по развёртыванию

1) Скопировать на сервер в произвольный каталог файлы сертификаты сайта (нужны файлы crt, key, pfx, а также файл pwd, в котором лежит пароль для pfx). Все файлы должны иметь имя media-pi, то есть ожидаются файлы media-pi.crt (сертификат), media-pi.key (приватный ключ сертификата), media-pi.pfx (PKCS #12), media-pi.pwd (пароль к PKCS #12)

2) Скачать и установить на сервер Docker Desktop [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/)

3) Скопировать на сервер файл docker-compose.yml

4) Открыть файл docker-compose.yml и внести следующие изменения:
   - заменить путь, отмеченный комментарием "#полный путь к корневому каталогу базы данных", на полный путь к корневому каталогу базы данных. Каталог создавать не надо, система создаст его при запуске.
   - заменить путь, отмеченный комментарием "#полный путь к каталогу резервной копии базы данных", на полный путь к каталогу резервной копии базы данных. Каталог создавать не надо, система создаст его при запуске.
   - заменить путь, отмеченный комментарием "#полный путь к каталогу журналов резервного копирования", на полный путь к каталогу журналов резервного копирования. Каталог создавать не надо, система создаст его при запуске.
   - заменить путь, отмеченный комментарием "#полный путь к каталогу с сертификатами сайта", на полный путь к каталогу с сертификатами сайта (см. пункт 1)

5) В каталоге с файлом docker-compose.yml выполнить `docker compose build`

Система готова к запуску.
Запуск осуществляется с помощью команд докера (`docker compose up`, `docker compose start`).


