# Strona Raportów

## Opis

Strona Raportów umożliwia analizę danych finansowych zgromadzonych w aplikacji. Użytkownik może generować zestawienia wydatków, przychodów oraz zmian sald kont w wybranym przedziale czasu.

## Cel

Celem strony jest:

- analiza struktury wydatków,
- monitorowanie przychodów,
- obserwacja trendów finansowych,
- wspomaganie podejmowania decyzji budżetowych.

## Funkcjonalności

### Wybór zakresu dat

Użytkownik może określić zakres danych wykorzystywanych do generowania raportów.

Dostępne opcje:

- Predefiniowane zakresy dat:
  - Ostatni tydzień
  - miesiąc
  - kwartał
  - rok
- Własny zakres dat


### Filtrowanie danych

Raporty mogą być filtrowane według:

- konta
- kategorii
- typu transakcji (przychód / wydatek)
- waluty
- projektu

Kombinacja filtrów pozwala na szczegółową analizę danych, np. wydatków w określonej kategorii dla wybranego konta w danym okresie. 
Wyświetlane dane są aktualizowane dynamicznie w zależności od wybranych filtrów.
Domyslny okres to bieżący miesiąc.


### Podział wydatków według kategorii

Prezentacja wydatków z podziałem na kategorie.

Wyświetlane dane:

- nazwa kategorii,
- liczba transakcji,
- suma wydatków,
- procentowy udział w całkowitych wydatkach.

Przy wyborze kategorii nadrzędnej, raport prezentuje dane dla wszystkich kategorii podrzędnych i/lub (pod)projektów.

### Podział transakcji według projektów
Prezentacja transakcji z podziałem na projekty
Wyświetlane dane:

- nazwa projektu,
- liczba transakcji,
- suma wydatków,
- procentowy udział w całkowitych wydatkach.

Po wybraniu projektu nadrzędnego, raport prezentuje dane dla wszystkich projektów podrzędnych i/lub (pod)kategorii.

### Trend przychodów i wydatków

Przedstawienie zmian przychodów oraz wydatków w czasie.

Dostępne wizualizacje:

- wykres liniowy,
- wykres słupkowy.

### Historia salda

Prezentacja zmian salda wszystkich kont w czasie.

Wyświetlane dane:

- saldo początkowe,
- saldo końcowe,
- zmiana wartości.

### Największe wydatki

Lista największych wydatków w wybranym okresie:

	Dla każdej pozycji prezentowane są:

	- data,
	- kwota,
	- konto,
	- kategoria,
	- projekt,
	- opis transakcji.

## Obsługa

### Generowanie raportu

Dynamiczne generowanie raportu po wybraniu zakresu dat i filtrów. Raport jest aktualizowany automatycznie po zmianie parametrów.
Mozliwość zmiany typu wykresu np. z kołowego na słupkowy.

### Eksport

Użytkownik może wyeksportować raport do:

- PDF
- Excel
- CSV

## Źródła danych

Raporty wykorzystują dane z:

- transakcji,
- kont,
- kategorii,
- projektów,
- kursów walut.

## Uwagi techniczne

### ViewModel

`ReportsViewModel`

### Serwisy

- `IReportService`
- `ISettingService`
- `IDatabaseService`

### Wydajność

- raport powinien zostać wygenerowany w czasie krótszym niż 2 sekundy,
- ładowanie danych powinno odbywać się asynchronicznie,
- dane raportów powinny być pobierane wyłącznie dla wskazanego zakresu dat.

## Planowany rozwój

- porównanie budżetu z rzeczywistymi wydatkami,
- prognozowanie wydatków,
- harmonogram automatycznego generowania raportów,
- dashboard z najważniejszymi wskaźnikami finansowymi.
``
