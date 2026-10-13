# ExpenseTracker

## Backlog, priorytety i aktualny stan projektu

*Stan na 29 września 2026*

| Obszar | Najważniejszy temat |
|---|---|
| **Następny feature** | Majątek: wartość historyczna i zmiana majątku |
| **Najważniejszy UX** | Drill-down kategorii i projektów |
| **Najważniejszy temat techniczny** | Synchronizacja języka natywnego UI i system bars |

## Rekomendowana kolejność prac

1. Majątek
2. Drill-down kategorii
3. Drill-down projektów
4. Synchronizacja języka natywnego UI
5. Android system bars
6. Cache kursów raportów
7. Dokończenie migracji do AppPickerów
8. Logger w `DatabaseService`
9. Podział `DatabaseService`
10. Wydzielenie `ExpenseTracker.Core` lub szersza architektura warstwowa

---

# Priorytet wysoki

Najbliższe funkcje mające największy wpływ na wartość aplikacji.

## 1. Raporty: zakładka Majątek

- Tryb domyślny: historia wartości majątku w czasie, czyli odpowiedź na pytanie: „ile był wart mój majątek w danym momencie?”.
- Tryb opcjonalny: zmiana majątku względem poprzedniego punktu.
- Planowany model: `BalanceHistoryPoint`.
- Planowana metoda: `GetBalanceHistoryAsync(...)`.
- Zmianę wartości, czyli `Delta`, warto liczyć w ViewModelu na bazie jednego źródła danych.
- Historyczne wartości należy przeliczać według historycznych kursów walut.
- Nowa funkcja powinna otrzymać testy serwisu oraz ViewModelu.
- Podczas implementacji należy ponownie ocenić potrzebę cache kursów raportowych.

## 2. Kategorie: agregacja nadrzędna i drill-down

- Widok domyślny powinien agregować wydatki do kategorii głównych.
- Kliknięcie kategorii głównej powinno otwierać podział na podkategorie.
- W drill-downie należy pokazać również pozycję „Bez podkategorii”.
- Opcjonalny przełącznik: pokaż wszystkie kategorie bez agregacji.
- Wymagane testy agregacji oraz przypadków brzegowych.

Przykład:

```text
Żywność                 900
├─ Restauracje          300
├─ Zakupy spożywcze     500
└─ Bez podkategorii     100
```

Na głównym wykresie:

```text
Żywność = 900
```

W drill-downie:

```text
Restauracje          300
Zakupy spożywcze     500
Bez podkategorii     100
```

## 3. Projekty: analogiczny drill-down

- Widok projektu głównego oraz przejście do jego podprojektów.
- Wykorzystać wzorzec sprawdzony przy kategoriach.
- Model ma dwa poziomy, więc nie potrzeba ogólnego, rekurencyjnego drzewa.
- Implementować po zakończeniu drill-downu kategorii.

---

# Priorytet średni

Tematy ważne dla spójności UX i platform, ale nieblokujące dalszego rozwoju funkcjonalnego.

## 4. Synchronizacja języka aplikacji z natywną platformą

- Własne XAML-e i `AppResources` respektują języki PL, EN i DE.
- Natywne kontrolki nie zawsze podążają za językiem wybranym w ExpenseTrackerze.

### Android

- `AppDatePicker` korzysta z własnego Androidowego handlera.
- Przyciski `OK` i `Anuluj` są lokalizowane przez `AppResources`.
- Kolory przycisków są kontrolowane przez aplikację.
- Środek natywnego kalendarza reaguje na kulturę aplikacji.
- Nagłówek natywnego kalendarza nadal jest rozwiązaniem kruchym i zależnym od wewnętrznego layoutu Androida.
- Docelowo preferować oficjalny mechanizm per-app language zamiast dalszych poprawek per kontrolka.

### Windows

- `Package.appxmanifest` deklaruje języki `en`, `pl` i `de`.
- Natywny Windows DatePicker nadal używa języka systemu Windows.
- Nie tworzyć kolejnego handlera wyłącznie dla DatePickera.
- Docelowo zsynchronizować język natywnego WinUI z wyborem języka aplikacji.

### Proponowany kierunek

Rozważyć wspólną abstrakcję:

```text
IPlatformLanguageService
├─ AndroidPlatformLanguageService
└─ WindowsPlatformLanguageService
```

Celem jest centralna synchronizacja:

```text
język ustawiony w ExpenseTrackerze
        ↓
.NET Culture + AppResources
        ↓
natywne UI Androida i Windows
```

## 5. Android: system status bar i edge-to-edge

- Tło status bara reaguje częściowo na motyw aplikacji.
- Jasność ikon nie zawsze odpowiada aktualnemu tłu.
- Przy jasnym tle mogą pozostać jasne ikony, przez co zawartość paska jest niewidoczna.
- Na Androidzie 15/16 nie opierać rozwiązania na starym `SetStatusBarColor()`.
- Rozwiązać centralnie dla motywów Light, Dark i High Contrast.
- Uwzględnić `SafeAreaEdges` w .NET 10 oraz zachowanie edge-to-edge.
- Nie mieszać logiki system bars z handlerem DatePickera.

## 6. Dokończenie systemu wspólnych kontrolek

Dostępne kontrolki:

- `AppPicker`
- `AppCurrencyPicker`
- `AppDatePicker`

Zasady dalszej migracji:

- Podmieniać stare zestawy `Grid + Picker + ikona` stopniowo.
- Wykonywać migrację przy okazji pracy nad konkretną stroną.
- Nie robić jednorazowej przebudowy wszystkich XAML-i.
- `AppPicker` odpowiada za wspólny wygląd i `ItemDisplayPath`.
- `AppCurrencyPicker` łączy wspólny wygląd z logiką `CurrencyPicker`.
- `AppDatePicker` zapewnia ikonę na Androidzie oraz platformowy handler dialogu daty.
- Windowsowy natywny chevron Pickera pozostaje świadomie odłożony.

Potencjalne miejsca dalszej migracji:

- `AddTransactionPage`
- `ExchangeRatesPage`
- ustawienia
- transakcje cykliczne
- pozostałe formularze korzystające ze zwykłych Pickerów i DatePickerów

---

# Priorytet niższy

Optymalizacje i poprawki, które warto wykonać po najważniejszych funkcjach.

## 7. ReportService: cache kursów

- Raport może wykonywać wiele powtarzalnych odczytów historycznych kursów.
- Rozważyć cache na czas pojedynczego raportu.

Przykładowy klucz:

```csharp
Dictionary<(string Source, string Target, DateTime Date), decimal?>
```

- Priorytet może wzrosnąć podczas implementacji Majątku, jeśli liczba przeliczeń stanie się zauważalna.
- Nie implementować prewencyjnie bez potwierdzenia rzeczywistego problemu.

## 8. Wspólny konwerter walutowy raportów

- Logika `waluta konta → kurs historyczny → waluta domyślna` pojawia się w wielu raportach.
- Wraz z zakładką Majątek pojawi się kolejny rzeczywisty przypadek użycia.
- Wydzielić dopiero po potwierdzeniu korzyści.
- Możliwa nazwa: `ReportCurrencyConverter`.

## 9. Trend: dynamiczna skala Y przy zakresie samych zer

- Przy autoskalowaniu i przesunięciu wykresu nad zakres zawierający same zera LiveCharts może zgubić czytelną skalę Y.
- Użytkownik może przełączyć się na stałą skalę Y.
- Problem nie blokuje dalszego rozwoju.
- Pozostawić jako świadomy UX improvement.

---

# Backlog techniczny

Porządki architektoniczne i techniczne bez bezpośredniego wpływu na bieżące funkcje użytkownika.

## 10. DatabaseService

- Największa pojedyncza klasa projektu.
- Obsługuje wiele odpowiedzialności:
  - konta,
  - transakcje,
  - kursy walut,
  - operacje cykliczne,
  - inicjalizację bazy,
  - wsparcie raportów.
- Nie dzielić wyłącznie z powodu rozmiaru.
- Najpierw ustabilizować domenę i przypadki użycia.
- Podział powinien wynikać z realnych granic odpowiedzialności.

## 11. ExpenseTracker.Core lub warstwa aplikacyjna

- Obecny układ testów działa.
- Wielotargetowy projekt MAUI utrudnia czyste testowanie.
- W przyszłości rozważyć:

```text
ExpenseTracker
ExpenseTracker.Core
ExpenseTracker.Tests
```

lub pełniejszy podział:

```text
Domain
Application
Infrastructure
Presentation
Tests
```

- Nie wykonywać dużej migracji do Clean Architecture podczas intensywnego rozwoju funkcji.
- Preferować prostotę i stopniowe wydzielanie stabilnych elementów.

## 12. Logger w DatabaseService

- Zastąpić `Console.WriteLine` przez `ILogger<DatabaseService>`.
- Dodać spójne zdarzenia dla:
  - awarii bazy,
  - problemów z kursami,
  - migracji,
  - silnika raportów.
- Rozważyć telemetrię dopiero po ustaleniu docelowego sposobu logowania.

---

# Tematy zamknięte

## Kursy walut

- unikalność pary walutowej i dnia,
- edycja bez fałszywego duplikatu,
- `SaveOrUpdateDailyExchangeRateAsync`,
- historia kursów,
- testy SQLite.

## AddTransaction

- race condition dla sprawdzania przewalutowania,
- anulowanie starych zapytań,
- ochrona przed nadpisaniem aktualnego wyboru,
- testy sukcesu i błędu.

## RecurringTransactionEngine

- walidacja interwału i `EndDate`,
- zaległe wystąpienia,
- transfery walutowe,
- logger i notifier,
- ochrona `SemaphoreSlim`,
- testy SQLite i współbieżności.

## Trend

- grupowanie dzień, tydzień i miesiąc,
- Income, Expense i Net,
- historyczne przewalutowanie,
- checkboxy serii i osi,
- zoom i pan,
- stała oraz dynamiczna skala Y,
- testy.

## ReportsPage

- własne zakładki,
- usunięcie globalnego `ScrollView`,
- brak konfliktu gestów LiveCharts z przewijaniem strony,
- zwijanie zakresu Custom,
- walidacja `StartDate <= EndDate`,
- osobne empty states.

## Wspólne kontrolki

- `AppPicker`,
- `AppCurrencyPicker`,
- `AppDatePicker`,
- dynamiczne kolory motywów,
- `ItemDisplayPath`,
- opcjonalny `SelectedTextColor`,
- opcjonalny `SelectedFontAttributes`,
- Androidowy handler DatePickera.

---

# Decyzje i zasady na dalszą pracę

- Najbliższy duży temat funkcjonalny: **Majątek**.
- Nie rozbudowywać architektury prewencyjnie.
- Wydzielać wspólne elementy po potwierdzeniu kolejnych przypadków użycia.
- Logiki biznesowej nie przenosić do Code Behind.
- Kontrolki platformowe i handlery są uzasadnionym wyjątkiem technicznym.
- Zmiany platformowe izolować przez handlery, implementacje platformowe lub interfejsy.
- XAML utrzymywać kompaktowo: właściwości tagu w jednym wierszu.
- Nowe funkcje raportowe powinny dostawać testy serwisu i ViewModelu.
- Po Majątku ponownie ocenić priorytet cache kursów.
- Nie kopiować logiki `CurrencyPicker` do ViewModeli.
- Nie tworzyć osobnych hacków językowych dla każdej natywnej kontrolki.
- Dla natywnej lokalizacji docelowo zastosować mechanizmy platformowe na poziomie całej aplikacji.

---

# Najbliższy plan

1. Zaimplementować zakładkę Majątek.
2. Dodać model `BalanceHistoryPoint` i metodę `GetBalanceHistoryAsync(...)`.
3. Dodać historię wartości majątku jako tryb domyślny.
4. Dodać zmianę majątku jako tryb opcjonalny.
5. Dodać testy serwisu i ViewModelu.
6. Ocenić liczbę odczytów kursów i ewentualnie dodać cache raportowy.
7. Następnie przejść do drill-downu kategorii i projektów.
