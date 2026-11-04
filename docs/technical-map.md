# Техническая карта Capri 0.8.2

Осмотр выполнен 4 ноября 2026 года. База зафиксирована на `a5bc79592995359b347075d92233d0654f84fbe2`, tag `v0.8.2`. Это последний non-draft/non-prerelease в [проверенной истории релизов](https://github.com/lachydotmcg/capri-desktop/releases). `main` уже ушла вперёд; последующие изменения автоматически не включались.

## История и сборка

| Релиз | Публикация UTC | Основное направление release notes |
|---|---|---|
| 0.8.2 | 2026-11-03 22:45 | Settings, call mode, live reactions, voice/desktop fixes |
| 0.8.1 | 2026-11-03 03:45 | Выбор инструментов моделью и изменение настроек через инструменты |
| 0.8.0 | 2026-11-02 22:59 | Броски персонажа, голос в виртуальном микрофоне, реакции на игры |
| 0.7.10 | 2026-11-02 01:15 | Исправления check-ins, taskbar и fullscreen |
| 0.7.9 | 2026-11-01 20:55 | Клонирование и chaos mode beta |
| 0.7.8 | 2026-10-31 23:43 | Streaming mode |

Release notes описывают историю автора; фактический статус ниже основан на исходниках и местных проверках, а не на одних рекламных формулировках.

Windows — C# 5, WinForms/GDI+, .NET Framework 4.8; `ci/build-windows.ps1` вызывает встроенный `csc.exe`, без Windows csproj и без NuGet restore. Используются System.Drawing, System.Windows.Forms, Runtime.Serialization, DPAPI, System.Speech, UIAutomation и WinRT metadata. P/Invoke вызывает Win32, DWM, DXGI/D3D11 и аудио-интерфейсы. PNG-полосы, курсор, шрифт и некоторые исходные ресурсы встраиваются в EXE.

`cross/Capri.Cross.csproj` — отдельный .NET 10 / Avalonia 11.3.22 клиент для Mac/Linux, связанный с исходными Brain/Engine/Hands/Voice/Presence. Современного .NET SDK в проверенном окружении нет. Этот клиент не собирался и не является поставкой CompanionLab для Linux; добавлена только ссылка на общий ActionJournal, необходимая после изменения Hands.

`site/` и `worker/` относятся к сайту/его вспомогательному сервису и не нужны для Windows-сборки. Полный системный rewrite не требуется, хотя `Capri.cs` уже содержит очень крупные Companion и smoke-сценарии. Новая логика вынесена в отдельные файлы/partial.

## Подсистемы оригинала

| Область | Исходники | Что действительно есть | Проверка и ограничения |
|---|---|---|---|
| 2D-персонаж | `Capri.cs`: Clip/Companion, `assets/sprites`, `Ledges.cs` | Полосы кадров, blink/talk/sleep/glitch/walk/perch/laydown, поведение мыши, броски, окна с per-pixel alpha | Исходные handler/smoke-проверки и локальная компиляция; полноценного формата сменных персонажей не было |
| Windows/мониторы | `Capri.cs`: Native/Companion, `Ledges.cs`, `Arrange.cs`, `Player.cs` | Перемещение, сидение на окнах/нижней границе, click-through, рабочие области нескольких экранов, fullscreen-настройки, mini-player | Набор синтетических окон и geometry-тестов; устойчивость во всех реальных играх/разных DPI не доказана |
| LLM | `Brain.cs`, `Engine.cs`, `Bolts.cs`, `Network.cs` | Ollama, свой llama-server, OpenAI-compatible endpoints, отдельные адаптеры OpenAI/Anthropic/Gemini/NVIDIA/OpenRouter и другие варианты по типу ключа; tool calls, SSE в поддерживаемых маршрутах | Помимо test servers проверен настоящий Brain с уже установленной Gemma через проверенный llama.cpp, включая русский ответ Capri/Luna и историю 2→4; облачные ключи не проверялись |
| Локальная модель | `Engine.cs`: Pick/Engine | Есть Gemma 4 E4B/E2B GGUF, закреплённые revision/hash, vision projector, загрузка и отдельный сервер | У пользователя найдены именно E4B 4.59 GB + projector 0.56 GB, SHA-256 совпадают с исходниками до/после реального теста. Веса не скачивались и не копировались. Один проверенный runtime работал на RTX 5070 и был завершён. Контекст теста 4096; vision inference и совместная нагрузка с играми/HD TTS не измерялись |
| Агент/инструменты | `Hands.cs`, `Computer.cs`: Job, `Files.cs`, `FileFinder.cs`, `Websites.cs` | Многошаговые задания, UIA-дерево, собственный указатель, click/type/scroll, открытие приложений/сайтов, окна, media/power, вопросы пользователю, Esc и release-input helper | Это уже агент, не только чат. Браузер в основном управляется через ОС/UIA; универсального изолированного browser backend нет. Чтение файлов есть, полноценная универсальная файловая редактура не завершена |
| Vision/OCR | `Frames.cs`, `Ocr.cs`, Brain/Computer | DXGI desktop duplication с fallback, JPEG/vision-запросы, Windows OCR, обработка fullscreen/device loss | Есть исходный OCR-сбой на английской HUD-фикстуре в этом окружении; автоматической гарантии удаления всех секретов со скриншота нет |
| TTS | `Voice.cs`, `VoicePack.cs`, `Mixer.cs` | ElevenLabs, Kokoro/sherpa local, Qwen HD, VoiceServer для local/cloud, эффекты, mouth animation, перебивание в voice chat | Маршруты и эффекты проверены синтетически; модели и аудио не загружались, звук/задержка/русское произношение не измерены |
| ASR/микрофон | `Ears.cs`, `Dictation.cs`, `HearingStatus.cs`, `CallEars.cs`, `GameEars.cs` | Windows speech, локальный Parakeet, wake word, выбор микрофона, уровни/прерывание, loopback для отдельных режимов | Наличие нужной ASR-модели не предполагается. Wake word ориентирован на Hey Capri; новый wake word на каждый профиль не реализован |
| Память/настройки | Brain, Preferences в `Capri.cs`, `GameNotes.cs`, `Reminders.cs`, `Care.cs`, `FollowUps.cs` | chat.json, me.md, memories.md, transcripts, игровые заметки, check-ins/reminders, JSON-настройки | Это существующая файловая память/суммаризация, не независимая vector DB; реальный долгосрочный recall не оценён без LLM |
| Discord | `Presence.cs`, `MicMix.cs`, `CableSetup.cs`, `CallMode.cs`, `CallPanel.cs` | Rich Presence через локальный IPC; виртуальный микрофон; call-mode, который слышит звук Discord через ОС | Это не официальный автономный bot-client: нет bot OAuth/allowlist-каналов/DAVE-стека/управления голосовым каналом. Ничего не отправлялось друзьям и бот не создавался |
| Разрешения | `Hands.cs`: Security, Computer/Job, Mainframe | Lockdown/Guarded/Trusted/Blackwall, per-tool gates, удержание действий до ответа, запреты ввода в password/card поля, маскирование некоторых значений | У оригинала Trusted и интрузивные привычки включались по умолчанию. Это набор прикладных проверок и prompt rules, а не OS sandbox и не доказанная защита от prompt injection |
| Секреты | `Voice.cs`: Protect/Unprotect | Windows DPAPI CurrentUser для ключей | Чат и error/transcript-файлы отдельно могут содержать текст пользователя; в cross-клиенте хранение ключей имеет другие ограничения |
| Обновления/установка | Updater в `Capri.cs`, `Setup.cs`, `Startup.cs` | GitHub releases, SHA-256/signature checks, загрузка/запуск установщика, per-user installation, startup registry | В CompanionLab внешние update entrypoints отключены, потому что upstream installer заменил бы форк. Собственный updater не реализован |
| Дополнения | игровые файлы, Clips, Minis, Chess, Ink, Clones, ChaosScreen | Игровые реакции, клипы, мини-виджеты, шахматы и эффекты | Существующие подсистемы сохранены; smoke LIMIT описывает случаи, требующие настоящей игры/оборудования. Установка сторонних модов не добавлялась |

## Минимальное изменение архитектуры

Новый Character System состоит из `Characters.cs` (JSON-каталог и проверенные data snapshots), `CharacterHost.cs` (связь с WinForms/Brain/Voice), `CharacterChecks.cs` и standalone `tests/CharacterTests.cs`. Существующий Clip продолжает рисовать прежние кадры и использовать прежнюю геометрию. Brain получает только имя/личность и путь отдельной пользовательской правки; LLM, память и history не пересоздаются.

Выбор хранится отдельно от AI-настроек. Voice-настройки сохраняются по id и имеют явного текущего владельца. Кеш спрайтов использует поколение, чтобы фоновая загрузка старого персонажа не пережила переключение. Это работающий первый адаптер; 3D, произвольные sprite layouts и новый AI Core не объявляются готовыми.

Полный исходный smoke-прогон выполнился с 528 PASS и 9 FAIL; детали и сравнение с финальным форком находятся в `ITERATION-1-REPORT.md`. Функции, отмеченные в таблице как существующие, подтверждены кодом; это не утверждение, что они все проверены на живых сервисах и реальных устройствах.
