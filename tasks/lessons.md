# Learned Lessons & Rules

## 1. Tooling & Validation (Strict Unity-First)
- **Rule:** Никогда не использовать команды `dotnet build` / `dotnet test` для валидации.
- **Pattern:** Всегда использовать Unity MCP:
  - `refresh_unity(compile="request", mode="force", scope="all")` для компиляции и перезагрузки домена.
  - `read_console` для проверки ошибок компиляции и предупреждений.
  - `run_tests` / `get_test_job` для запуска EditMode и PlayMode тестов.
- **Rule:** Никогда не редактировать и не создавать `.meta` файлы вручную. Позволять Unity генерировать их автоматически.

## 2. Input System (New Input System Only)
- **Rule:** Использовать исключительно `com.unity.inputsystem` (New Input System Package).
- **Rule:** Никакого устаревшего `UnityEngine.Input`.

## 3. Архитектура и Чистота Кода (VoxelCore)
- **Rule:** Избегать дублирования API и методов «про запас». Если нужен поиск/создание, использовать единый `GetOrCreateChunk`, а для чтения — `GetChunk`.
- **Rule:** Не дублировать проверки границ в нижних контейнерах данных (`SubChunk`, `Chunk`), если верхний фасад (`World`) математически гарантирует валидность входных координат.
- **Rule:** Побитовая математика (`>> 4`, `& 15`) предпочтительнее float-операций и `Mathf.FloorToInt` для воксельной сетки со степенью двойки.
- **Rule:** Ленивая аллокация (`null` подчанки) обязательна для оптимизации памяти под WebGL.
