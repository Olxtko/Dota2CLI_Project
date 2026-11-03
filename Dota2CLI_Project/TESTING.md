# Перевірка Dota 2 CLI

1. Встановіть .NET 8 SDK.
2. Відкрийте термінал у папці проєкту.
3. Виконайте `dotnet restore`, а потім `dotnet run`.
4. Перевірте команди нижче та зробіть скриншоти для звіту. Для `refresh` потрібне інтернет-з'єднання; після команди перевірте, що список героїв оновився. Якщо API недоступне, програма має показати зрозуміле повідомлення й зберегти попередній каталог.

```text
help
refresh
heroes
hero axe
search intelligence
create
play
play axe lina
act attack
act heal
act status
status
results
exit
```

Для `create` введіть тестові значення, наприклад:
- Ім'я: Test Hero
- ID: test-hero
- Атрибут: Strength
- Тип атаки: Melee
- Максимальне HP: 100
- Сила атаки: 15
- Посилання на графіку: залишити порожнім

Після запуску перевірте, що створилися файли `Data/heroes.json`, `Data/game-state.json` та, після завершення бою, `Data/game-results.txt`.
